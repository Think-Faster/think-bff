using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Predictions;
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

    public async Task<PredictionDecisionDto> DecideAsync(
        Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, CancellationToken ct)
    {
        var prediction = await _context.Predictions.SingleOrDefaultAsync(p => p.Id == predictionId, ct)
            ?? throw new NotFoundException($"Prediction {predictionId} not found.");

        var decision = new PredictionDecision
        {
            Id = Guid.NewGuid(),
            PredictionId = predictionId,
            UserId = userId,
            Action = request.Action,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            DecidedAt = DateTimeOffset.UtcNow,
        };

        prediction.Status = request.Action switch
        {
            DecisionAction.Take => PredictionStatus.Taken,
            DecisionAction.Reject => PredictionStatus.Rejected,
            DecisionAction.Mute => PredictionStatus.Muted,
            DecisionAction.Reopen => PredictionStatus.InReview,
            _ => prediction.Status,
        };

        _context.PredictionDecisions.Add(decision);
        await _context.SaveChangesAsync(ct);

        return ToDto(decision);
    }

    public async Task<PagedResult<FactAlertDto>> ListFactAlertsAsync(int? objectId, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.FactAlerts.AsNoTracking().AsQueryable();
        if (objectId is { } oid)
        {
            query = query.Where(a => a.ObjectId == oid);
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
            TriggerSensorIds = request.TriggerSensorIds.ToArray(),
            Status = "active",
        };

        _context.FactAlerts.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
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
        DecidedAt = d.DecidedAt,
    };

    private static FactAlertDto ToDto(FactAlert a) => new()
    {
        Id = a.Id,
        ObjectId = a.ObjectId,
        Type = a.Type,
        StartedAt = a.StartedAt,
        AnnouncedAt = a.AnnouncedAt,
        TriggerSensorIds = a.TriggerSensorIds,
        Status = a.Status,
    };
}
