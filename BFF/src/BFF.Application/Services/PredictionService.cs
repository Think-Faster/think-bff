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

        return ToDto(entity);
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
        if (latest is null || latest.HourEnd < episodeStart)
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
                Id = Guid.NewGuid(), PredictionId = latest.Id, Feature = f.Feature, Value = f.Value,
                Weight = f.Weight, Direction = f.Direction,
            }));
        _context.PredictionEvidence.AddRange((request.Evidence ?? Array.Empty<PredictionEvidenceDto>()).Select(e =>
            new PredictionEvidence
            {
                Id = Guid.NewGuid(), PredictionId = latest.Id, SensorId = e.SensorId, PicketId = e.PicketId,
                Ts = e.Ts, Value = e.Value,
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
                SensorId = e.SensorId, Ts = e.Ts, Value = e.Value,
                PicketId = e.PicketId ?? pickets.GetValueOrDefault(e.SensorId),
            }).ToArray(),
        };
    }

    public async Task<PredictionDecisionDto> DecideAsync(
        Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, CancellationToken ct)
    {
        var prediction = await _context.Predictions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == predictionId, ct)
            ?? throw new NotFoundException($"Prediction {predictionId} not found.");

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

    private static readonly PredictionStatus[] Decided =
        { PredictionStatus.Taken, PredictionStatus.Rejected, PredictionStatus.Muted };

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

        return new PagedResult<FactAlertDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
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
            return (await CreateFactAlertAsync(request, ct), true);
        }

        if (!announcement && request.LastAt is { } lastAt && lastAt > existing.LastAt)
        {
            existing.LastAt = lastAt;
            existing.RouteJson = RouteJson(request.Route) ?? existing.RouteJson;
            existing.DetailsJson = request.DetailsJson ?? existing.DetailsJson;
            await _context.SaveChangesAsync(ct);
        }

        return (ToDto(existing), false);
    }

    private static PredictionDto ToDto(Prediction p) => new()
    {
        Id = p.Id,
        ObjectId = p.ObjectId,
        Type = p.Type,
        HourEnd = p.HourEnd,
        HorizonHours = p.HorizonHours,
        Score = p.Score,
        Threshold = p.Threshold,
        Alarm = p.Alarm,
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
        Factors = p.Factors.Select(f => new PredictionFactorDto
        {
            Feature = f.Feature, Value = f.Value, Weight = f.Weight, Direction = f.Direction,
        }).ToArray(),
        Evidence = p.Evidence.Select(e => new PredictionEvidenceDto
        {
            SensorId = e.SensorId, PicketId = e.PicketId, Ts = e.Ts, Value = e.Value,
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
