using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using System.Text.Json;
using BFF.Contracts.Predictions;
using BFF.Models.Constants;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class PredictionService : IPredictionService
{
    private readonly BffDbContext _context;

    public PredictionService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PredictionListItemDto>> ListAsync(
        int? objectId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Predictions.AsNoTracking().AsQueryable();

        if (objectId is { } oid)
        {
            query = query.Where(p => p.ObjectId == oid);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PredictionStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                throw new ArgumentException($"Unknown prediction status: {status}", nameof(status));
            }

            query = query.Where(p => p.Status == parsedStatus);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(p => p.HourEnd).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PredictionListItemDto
            {
                Id = p.Id,
                ObjectId = p.ObjectId,
                Type = p.Type,
                Topic = p.Topic,
                Probability = p.Probability,
                HourEnd = p.HourEnd,
                SinceHours = p.SinceHours,
                Status = p.Status,
                Alarm = p.Alarm,
                AlarmEndedAt = p.AlarmEndedAt,
            })
            .ToListAsync(ct);

        return new PagedResult<PredictionListItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<PredictionDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await _context.Predictions.AsNoTracking()
            .Include(p => p.Factors)
            .Include(p => p.Evidence)
            .SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Prediction {id} not found.");

        // Датчик и пикет свидетеля — из справочника при чтении (§9.7: модель пикетов не знает).
        var sensorIds = entity.Evidence.Select(e => e.SensorId).Distinct().ToArray();
        var sensors = await _context.Sensors.AsNoTracking().Where(s => sensorIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.SType, s.PicketId }, ct);
        var picketIds = entity.Evidence.Select(e => e.PicketId).Concat(sensors.Values.Select(s => s.PicketId))
            .OfType<long>().Distinct().ToArray();
        var pickets = await _context.Pickets.AsNoTracking().Where(p => picketIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        DateTimeOffset? mutedUntil = null;
        if (entity.Status == PredictionStatus.Muted)
        {
            mutedUntil = await _context.PredictionDecisions.AsNoTracking()
                .Where(d => d.PredictionId == id && d.Action == DecisionAction.Mute)
                .OrderByDescending(d => d.DecidedAt)
                .Select(d => d.MutedUntil)
                .FirstOrDefaultAsync(ct);
        }

        var dto = ToDto(entity, mutedUntil);
        return new PredictionDto
        {
            Id = dto.Id, ObjectId = dto.ObjectId, Type = dto.Type, HourEnd = dto.HourEnd,
            HorizonHours = dto.HorizonHours, Score = dto.Score, Threshold = dto.Threshold, Alarm = dto.Alarm,
            AlarmEndedAt = dto.AlarmEndedAt, Probability = dto.Probability, Confidence = dto.Confidence,
            SinceHours = dto.SinceHours, Topic = dto.Topic, Description = dto.Description,
            Classification = dto.Classification, Recommendation = dto.Recommendation,
            ModelVersionId = dto.ModelVersionId, Status = dto.Status, MutedReason = dto.MutedReason,
            MutedUntil = dto.MutedUntil, Factors = dto.Factors,
            Evidence = entity.Evidence.OrderByDescending(e => e.Ts).Select(e =>
            {
                var sensor = sensors.GetValueOrDefault(e.SensorId);
                var picketId = e.PicketId ?? sensor?.PicketId;
                return new PredictionEvidenceDto
                {
                    SensorId = e.SensorId, PicketId = picketId, Ts = e.Ts, Value = e.Value, ValueText = e.ValueText,
                    SensorName = sensor?.Name, SensorType = sensor?.SType,
                    PicketCode = picketId is { } pid ? pickets.GetValueOrDefault(pid) : null,
                };
            }).ToArray(),
        };
    }

    public async Task<PredictionStatsDto> StatsAsync(CancellationToken ct)
    {
        var dayAgo = DateTimeOffset.UtcNow.AddHours(-24);
        var lastHour = await _context.Predictions.AsNoTracking().MaxAsync(p => (DateTimeOffset?)p.HourEnd, ct);
        var staleBefore = (lastHour ?? DateTimeOffset.MinValue).AddHours(-1);
        var rows = await _context.Predictions.AsNoTracking()
            .GroupBy(p => p.Type)
            .Select(g => new
            {
                Type = g.Key,
                Active = g.Count(p => p.Alarm),
                Stale = g.Count(p => p.Alarm && p.HourEnd < staleBefore),
                Open = g.Count(p => p.Status == PredictionStatus.New || p.Status == PredictionStatus.InReview),
                Taken = g.Count(p => p.Status == PredictionStatus.Taken),
                Muted = g.Count(p => p.Status == PredictionStatus.Muted),
                Rejected = g.Count(p => p.Status == PredictionStatus.Rejected),
                Created = g.Count(p => p.CreatedAt >= dayAgo),
                Ended = g.Count(p => p.AlarmEndedAt >= dayAgo),
            })
            .ToListAsync(ct);

        return new PredictionStatsDto
        {
            LastHourEnd = lastHour,
            ActiveAlarms = rows.Sum(r => r.Active),
            StaleAlarms = rows.Sum(r => r.Stale),
            Open = rows.Sum(r => r.Open),
            CreatedLast24h = rows.Sum(r => r.Created),
            EndedLast24h = rows.Sum(r => r.Ended),
            ByType = rows.OrderBy(r => r.Type).Select(r => new PredictionTypeStatsDto
            {
                Type = r.Type, ActiveAlarms = r.Active, Open = r.Open, Taken = r.Taken, Muted = r.Muted,
                Rejected = r.Rejected, CreatedLast24h = r.Created, EndedLast24h = r.Ended,
            }).ToArray(),
        };
    }

    public async Task<int> EndAlarmsAsync(
        int objectId, IReadOnlyCollection<PredictionType> types, DateTimeOffset hourEnd, CancellationToken ct)
    {
        if (types.Count == 0)
        {
            return 0;
        }

        // Одним UPDATE на сообщение: карточки пары, у которых тревога горела часом раньше. Без решения —
        // «истёк»; взятые, заглушенные и отклонённые статус сохраняют, но видно, что тревоги больше нет.
        var list = types.ToArray();
        return await _context.Predictions
            .Where(p => p.ObjectId == objectId && list.Contains(p.Type) && p.Alarm && p.HourEnd < hourEnd)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Alarm, false)
                .SetProperty(p => p.AlarmEndedAt, hourEnd)
                .SetProperty(p => p.Status, p => p.Status == PredictionStatus.New || p.Status == PredictionStatus.InReview
                    ? PredictionStatus.Expired
                    : p.Status), ct);
    }

    public async Task<PredictionDto> CreateAsync(CreatePredictionRequest request, CancellationToken ct)
    {
        // Dedup per section 9.3 of the source spec: (object, type, hour, model version).
        var duplicate = await _context.Predictions.AsNoTracking().SingleOrDefaultAsync(
            p => p.ObjectId == request.ObjectId && p.Type == request.Type
                && p.HourEnd == request.HourEnd && p.ModelVersionId == request.ModelVersionId, ct);

        if (duplicate is not null)
        {
            return await GetAsync(duplicate.Id, ct);
        }

        var entity = new Prediction
        {
            Id = Guid.NewGuid(),
            ObjectId = request.ObjectId,
            Type = request.Type,
            HourEnd = request.HourEnd,
            HorizonHours = request.HorizonHours,
            Score = request.Score,
            Threshold = request.Threshold,
            Alarm = request.Alarm,
            Probability = request.Probability,
            Confidence = request.Confidence,
            SinceHours = request.SinceHours,
            Topic = request.Topic,
            Description = request.Description,
            Classification = request.Classification,
            Recommendation = request.Recommendation,
            ModelVersionId = request.ModelVersionId,
            Status = PredictionStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (request.Factors is not null)
        {
            entity.Factors = request.Factors.Select(f => new PredictionFactor
            {
                Id = Guid.NewGuid(),
                PredictionId = entity.Id,
                Feature = f.Feature,
                Label = f.Label,
                Value = f.Value,
                Weight = f.Weight,
                Direction = f.Direction,
            }).ToList();
        }

        if (request.Evidence is not null)
        {
            entity.Evidence = request.Evidence.Select(e => new PredictionEvidence
            {
                Id = Guid.NewGuid(),
                PredictionId = entity.Id,
                SensorId = e.SensorId,
                PicketId = e.PicketId,
                Ts = e.Ts,
                Value = e.Value,
                ValueText = e.ValueText,
            }).ToList();
        }

        _context.Predictions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<(PredictionDto Prediction, bool Created)> RecordForecastAsync(
        CreatePredictionRequest request, CancellationToken ct)
    {
        request = await WithPicketsAsync(request, ct);

        var latest = await _context.Predictions
            .Where(p => p.ObjectId == request.ObjectId && p.Type == request.Type)
            .OrderByDescending(p => p.HourEnd)
            .FirstOrDefaultAsync(ct);

        // Эпизод начался since_hours назад; прогноз за час до начала — ещё тот же ряд (склейка модели).
        var episodeStart = request.HourEnd.AddHours(-Math.Max(request.SinceHours, 0) - 1);
        // Тревога пары уже кончилась (EndAlarmsAsync) — новая тревога значит новый эпизод и новую карточку.
        if (latest is null || latest.HourEnd < episodeStart
            || (latest.AlarmEndedAt is { } ended && request.HourEnd >= ended))
        {
            return (await CreateAsync(request, ct), true);
        }

        if (latest.HourEnd > request.HourEnd)
        {
            // Запоздалое сообщение за прошлый час — карточка уже свежее.
            return (await GetAsync(latest.Id, ct), false);
        }

        latest.HourEnd = request.HourEnd;
        latest.HorizonHours = request.HorizonHours;
        latest.Score = request.Score;
        latest.Threshold = request.Threshold;
        latest.Alarm = request.Alarm;
        latest.AlarmEndedAt = null;
        latest.Probability = request.Probability;
        latest.Confidence = request.Confidence;
        latest.SinceHours = request.SinceHours;
        latest.Description = request.Description;
        latest.Recommendation = request.Recommendation ?? latest.Recommendation;
        latest.ModelVersionId = request.ModelVersionId;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _context.PredictionFactors.Where(f => f.PredictionId == latest.Id).ExecuteDeleteAsync(ct);
        await _context.PredictionEvidence.Where(e => e.PredictionId == latest.Id).ExecuteDeleteAsync(ct);
        _context.PredictionFactors.AddRange((request.Factors ?? Array.Empty<PredictionFactorDto>()).Select(f =>
            new PredictionFactor
            {
                Id = Guid.NewGuid(), PredictionId = latest.Id, Feature = f.Feature, Label = f.Label, Value = f.Value,
                Weight = f.Weight, Direction = f.Direction,
            }));
        _context.PredictionEvidence.AddRange((request.Evidence ?? Array.Empty<PredictionEvidenceDto>()).Select(e =>
            new PredictionEvidence
            {
                Id = Guid.NewGuid(), PredictionId = latest.Id, SensorId = e.SensorId, PicketId = e.PicketId,
                Ts = e.Ts, Value = e.Value, ValueText = e.ValueText,
            }));
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await GetAsync(latest.Id, ct), false);
    }

    /// <summary>Пикет свидетеля модель не знает (§9.7) — подставляем из справочника датчиков.</summary>
    private async Task<CreatePredictionRequest> WithPicketsAsync(CreatePredictionRequest request, CancellationToken ct)
    {
        if (request.Evidence is not { Count: > 0 } evidence)
        {
            return request;
        }

        var ids = evidence.Select(e => e.SensorId).Distinct().ToArray();
        var pickets = await _context.Sensors.AsNoTracking().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.PicketId, ct);

        return new CreatePredictionRequest
        {
            ObjectId = request.ObjectId, Type = request.Type, HourEnd = request.HourEnd,
            HorizonHours = request.HorizonHours, Score = request.Score, Threshold = request.Threshold,
            Alarm = request.Alarm, Probability = request.Probability, Confidence = request.Confidence,
            SinceHours = request.SinceHours, Topic = request.Topic, Description = request.Description,
            Classification = request.Classification, Recommendation = request.Recommendation,
            ModelVersionId = request.ModelVersionId, Factors = request.Factors,
            Evidence = evidence.Select(e => new PredictionEvidenceDto
            {
                SensorId = e.SensorId, Ts = e.Ts, Value = e.Value, ValueText = e.ValueText,
                PicketId = e.PicketId ?? pickets.GetValueOrDefault(e.SensorId),
            }).ToArray(),
        };
    }

    public async Task<PredictionDecisionDto> DecideAsync(
        Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, bool canManage, CancellationToken ct)
    {
        var prediction = await _context.Predictions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == predictionId, ct)
            ?? throw new NotFoundException($"Prediction {predictionId} not found.");

        // Заглушить пару объект-тип и снять заглушку — решение главного диспетчера (predictions:manage):
        // молчание прячет тревоги от всех диспетчеров смены (§13.3).
        if (!canManage && (request.Action == DecisionAction.Mute
                || (request.Action == DecisionAction.Reopen && prediction.Status == PredictionStatus.Muted)))
        {
            throw new ForbiddenException("Muting a prediction requires predictions:manage.");
        }

        // Take / reject / mute — только по открытому прогнозу, reopen — только по решённому.
        // Условие на статус внутри UPDATE: второй диспетчер, нажавший одновременно, получит 409, а не
        // вторую заявку по тому же прогнозу.
        var (from, to) = request.Action switch
        {
            DecisionAction.Take => (Open, PredictionStatus.Taken),
            DecisionAction.Reject => (Open, PredictionStatus.Rejected),
            DecisionAction.Mute => (Open, PredictionStatus.Muted),
            DecisionAction.Reopen => (Decided, PredictionStatus.InReview),
            _ => throw new ArgumentException($"Unknown decision action: {request.Action}", nameof(request)),
        };

        WorkTask? existingTask = null;
        if (request.Action == DecisionAction.Take && request.TaskId is { } taskId)
        {
            existingTask = await _context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new NotFoundException($"Task {taskId} not found.");
            if (existingTask.Status is WorkTaskStatus.Closed or WorkTaskStatus.Cancelled)
            {
                throw new ConflictException($"Task {taskId} is already {existingTask.Status}.", "invalid_status");
            }

            if (existingTask.ObjectId != prediction.ObjectId)
            {
                throw new ConflictException($"Task {taskId} is for another object.", "object_mismatch");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var guarded = _context.Predictions.Where(p => p.Id == predictionId && from.Contains(p.Status));
        var affected = request.Action switch
        {
            DecisionAction.Mute => await guarded.ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, to).SetProperty(p => p.MutedReason, "decision"), ct),
            DecisionAction.Reopen => await guarded.ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, to).SetProperty(p => p.MutedReason, (string?)null), ct),
            _ => await guarded.ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, to), ct),
        };

        if (affected == 0)
        {
            throw new ConflictException(
                $"Prediction {predictionId} is not in a state that allows '{request.Action}'.",
                request.Action == DecisionAction.Reopen ? "invalid_status" : "prediction_already_decided");
        }

        var decision = new PredictionDecision
        {
            Id = Guid.NewGuid(),
            PredictionId = predictionId,
            UserId = userId,
            Action = request.Action,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            MutedUntil = request.Action == DecisionAction.Mute ? request.Until : null,
            DecidedAt = now,
        };

        if (request.Action == DecisionAction.Take)
        {
            // «Взять» = завести заявку (домен §4.2): по прогнозу без заявки диспетчеру нечего назначать.
            var task = existingTask;
            if (task is null)
            {
                task = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    Number = await NewTaskNumberAsync(now, ct),
                    SourceType = TaskSourceType.Prediction,
                    ObjectId = prediction.ObjectId,
                    Topic = prediction.Topic,
                    Description = prediction.Recommendation ?? prediction.Description,
                    FaultClassification = prediction.Classification,
                    Priority = 3,
                    Status = WorkTaskStatus.InWork,
                    DispatcherId = userId,
                    CreatedAt = now,
                    TakenAt = now,
                };
                _context.Tasks.Add(task);
            }

            var alreadyAttached = existingTask is not null && await _context.TaskPredictions.AsNoTracking()
                .AnyAsync(tp => tp.TaskId == task.Id && tp.PredictionId == predictionId && tp.DetachedAt == null, ct);
            if (!alreadyAttached)
            {
                _context.TaskPredictions.Add(new TaskPrediction
                {
                    TaskId = task.Id,
                    PredictionId = predictionId,
                    AttachedBy = userId,
                    AttachedAt = now,
                    IsPrimary = existingTask is null,
                });
            }

            decision.TaskId = task.Id;
        }

        _context.PredictionDecisions.Add(decision);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDto(decision);
    }

    private static readonly PredictionStatus[] Open = { PredictionStatus.New, PredictionStatus.InReview };

    // Истёкшую карточку тоже можно вернуть в работу: тревога кончилась, а диспетчер хочет её разобрать.
    private static readonly PredictionStatus[] Decided =
        { PredictionStatus.Taken, PredictionStatus.Rejected, PredictionStatus.Muted, PredictionStatus.Expired };

    /// <summary>Номер заявки, заведённой по прогнозу: З-ГГММДД-ЧЧММСС-NN — тот же вид, что даёт фронт.</summary>
    private async Task<string> NewTaskNumberAsync(DateTimeOffset now, CancellationToken ct)
    {
        var stem = $"З-{now:yyMMdd-HHmmss}-";
        for (var i = 0; i < 20; i++)
        {
            var number = stem + Random.Shared.Next(10, 100);
            if (!await _context.Tasks.AsNoTracking().AnyAsync(t => t.Number == number, ct))
            {
                return number;
            }
        }

        return stem + Guid.NewGuid().ToString("N")[..8];
    }

    public async Task<PagedResult<FactAlertDto>> ListFactAlertsAsync(
        int? objectId, bool? live, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.FactAlerts.AsNoTracking().AsQueryable();
        if (objectId is { } oid)
        {
            query = query.Where(a => a.ObjectId == oid);
        }

        if (live is { } isLive)
        {
            var since = FactAlertLiveness.LiveSince(DateTimeOffset.UtcNow);
            query = isLive ? query.Where(a => a.LastAt >= since) : query.Where(a => a.LastAt < since);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.StartedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => ToDto(a)).ToListAsync(ct);

        return new PagedResult<FactAlertDto>
        {
            Items = await WithSensorsAsync(items, ct), Total = total, Page = page, PageSize = pageSize,
        };
    }

    // Датчики эпизодов страницы одним запросом к справочнику: имя, тип и код пикета.
    private async Task<IReadOnlyList<FactAlertDto>> WithSensorsAsync(IReadOnlyList<FactAlertDto> items, CancellationToken ct)
    {
        var ids = items.SelectMany(a => a.TriggerSensorIds.Concat(a.Route.Select(r => r.SensorId))).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return items;
        }

        var sensors = await _context.Sensors.AsNoTracking().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.SType, s.PicketId }, ct);
        var picketIds = sensors.Values.Select(s => s.PicketId).OfType<long>().Distinct().ToArray();
        var pickets = await _context.Pickets.AsNoTracking().Where(p => picketIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        return items.Select(a => new FactAlertDto
        {
            Id = a.Id, ObjectId = a.ObjectId, Type = a.Type, Group = a.Group, StartedAt = a.StartedAt,
            AnnouncedAt = a.AnnouncedAt, LastAt = a.LastAt, Live = a.Live, TriggerSensorIds = a.TriggerSensorIds,
            Status = a.Status, Route = a.Route, DetailsJson = a.DetailsJson,
            Sensors = a.TriggerSensorIds.Concat(a.Route.Select(r => r.SensorId)).Distinct()
                .Select(id => sensors.GetValueOrDefault(id) is { } s
                    ? new FactSensorDto
                    {
                        SensorId = id, Name = s.Name, SType = s.SType, PicketId = s.PicketId,
                        PicketCode = s.PicketId is { } pid ? pickets.GetValueOrDefault(pid) : null,
                    }
                    : new FactSensorDto { SensorId = id })
                .ToArray(),
        }).ToArray();
    }

    public async Task<FactAlertDto> CreateFactAlertAsync(CreateFactAlertRequest request, CancellationToken ct)
    {
        var entity = new FactAlert
        {
            Id = Guid.NewGuid(),
            ObjectId = request.ObjectId,
            Type = request.Type,
            StartedAt = request.StartedAt,
            AnnouncedAt = request.AnnouncedAt,
            LastAt = request.LastAt ?? request.AnnouncedAt,
            TriggerSensorIds = request.TriggerSensorIds.ToArray(),
            Status = "active",
            RouteJson = RouteJson(request.Route),
            DetailsJson = request.DetailsJson,
        };

        _context.FactAlerts.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<(FactAlertDto Alert, bool Created)> RecordFactAlertAsync(
        CreateFactAlertRequest request, bool announcement, CancellationToken ct)
    {
        var existing = await _context.FactAlerts.FirstOrDefaultAsync(
            a => a.ObjectId == request.ObjectId && a.Type == request.Type && a.StartedAt == request.StartedAt, ct);
        if (existing is null && !announcement)
        {
            // Начало эпизода у модели может сдвинуться (канал вернулся, другой замолчал) — тот же живой эпизод.
            var since = FactAlertLiveness.LiveSince(DateTimeOffset.UtcNow);
            existing = await _context.FactAlerts
                .Where(a => a.ObjectId == request.ObjectId && a.Type == request.Type && a.LastAt >= since)
                .OrderByDescending(a => a.LastAt)
                .FirstOrDefaultAsync(ct);
        }

        if (existing is null)
        {
            var created = await CreateFactAlertAsync(request, ct);
            return ((await WithSensorsAsync(new[] { created }, ct))[0], true);
        }

        if (!announcement && request.LastAt is { } lastAt && lastAt > existing.LastAt)
        {
            existing.LastAt = lastAt;
            existing.RouteJson = RouteJson(request.Route) ?? existing.RouteJson;
            existing.DetailsJson = request.DetailsJson ?? existing.DetailsJson;
            await _context.SaveChangesAsync(ct);
        }

        return ((await WithSensorsAsync(new[] { ToDto(existing) }, ct))[0], false);
    }

    private static PredictionDto ToDto(Prediction p, DateTimeOffset? mutedUntil = null) => new()
    {
        Id = p.Id,
        ObjectId = p.ObjectId,
        Type = p.Type,
        HourEnd = p.HourEnd,
        HorizonHours = p.HorizonHours,
        Score = p.Score,
        Threshold = p.Threshold,
        Alarm = p.Alarm,
        AlarmEndedAt = p.AlarmEndedAt,
        Probability = p.Probability,
        Confidence = p.Confidence,
        SinceHours = p.SinceHours,
        Topic = p.Topic,
        Description = p.Description,
        Classification = p.Classification,
        Recommendation = p.Recommendation,
        ModelVersionId = p.ModelVersionId,
        Status = p.Status,
        MutedReason = p.MutedReason,
        MutedUntil = mutedUntil,
        Factors = p.Factors.Select(f => new PredictionFactorDto
        {
            Feature = f.Feature, Label = f.Label, Value = f.Value, Weight = f.Weight, Direction = f.Direction,
        }).ToArray(),
        Evidence = p.Evidence.Select(e => new PredictionEvidenceDto
        {
            SensorId = e.SensorId, PicketId = e.PicketId, Ts = e.Ts, Value = e.Value, ValueText = e.ValueText,
        }).ToArray(),
    };

    private static PredictionDecisionDto ToDto(PredictionDecision d) => new()
    {
        Id = d.Id,
        PredictionId = d.PredictionId,
        UserId = d.UserId,
        Action = d.Action,
        ReasonCode = d.ReasonCode,
        Comment = d.Comment,
        TaskId = d.TaskId,
        MutedUntil = d.MutedUntil,
        DecidedAt = d.DecidedAt,
    };

    private static FactAlertDto ToDto(FactAlert a) => new()
    {
        Id = a.Id,
        ObjectId = a.ObjectId,
        Type = a.Type,
        Group = a.Type.Group(),
        StartedAt = a.StartedAt,
        AnnouncedAt = a.AnnouncedAt,
        LastAt = a.LastAt,
        Live = a.LastAt >= FactAlertLiveness.LiveSince(DateTimeOffset.UtcNow),
        TriggerSensorIds = a.TriggerSensorIds,
        Status = a.Status,
        Route = Route(a.RouteJson),
        DetailsJson = a.DetailsJson,
    };

    private static readonly JsonSerializerOptions RouteOptions = new(JsonSerializerDefaults.Web);

    private static string? RouteJson(IReadOnlyList<FactRoutePointDto> route) =>
        route.Count > 0 ? JsonSerializer.Serialize(route, RouteOptions) : null;

    private static IReadOnlyList<FactRoutePointDto> Route(string? json) =>
        string.IsNullOrEmpty(json)
            ? Array.Empty<FactRoutePointDto>()
            : JsonSerializer.Deserialize<FactRoutePointDto[]>(json, RouteOptions) ?? Array.Empty<FactRoutePointDto>();
}
