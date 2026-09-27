using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.ModelSettings;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class ModelSettingsService : IModelSettingsService
{
    private readonly BffDbContext _context;

    public ModelSettingsService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ModelVersionDto>> ListModelVersionsAsync(CancellationToken ct)
    {
        return await _context.ModelVersions.AsNoTracking()
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => ToDto(v))
            .ToListAsync(ct);
    }

    public async Task<ModelVersionDto> CreateModelVersionAsync(CreateModelVersionRequest request, CancellationToken ct)
    {
        var exists = await _context.ModelVersions.AsNoTracking().AnyAsync(v => v.Id == request.Id, ct);
        if (exists)
        {
            throw new ConflictException($"Model version '{request.Id}' already exists.", "duplicate_code");
        }

        var entity = new ModelVersion
        {
            Id = request.Id,
            Name = request.Name,
            IsDefault = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.ModelVersions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ModelVersionDto> ActivateModelVersionAsync(string id, Guid switchedBy, CancellationToken ct)
    {
        var target = await _context.ModelVersions.SingleOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"Model version '{id}' not found.");

        await _context.ModelVersions
            .Where(v => v.IsDefault && v.Id != id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsDefault, false), ct);

        target.IsDefault = true;
        target.SwitchedAt = DateTimeOffset.UtcNow;
        target.SwitchedBy = switchedBy;

        await _context.SaveChangesAsync(ct);
        return ToDto(target);
    }

    public async Task<IReadOnlyList<CoefficientDto>> ListCoefficientsAsync(CancellationToken ct)
    {
        // Latest version per type — history stays in the table, per section 10.3 of the source doc.
        return await _context.Coefficients.AsNoTracking()
            .GroupBy(c => c.Type)
            .Select(g => g.OrderByDescending(c => c.Version).First())
            .Select(c => ToDto(c))
            .ToListAsync(ct);
    }

    public async Task<CoefficientDto> CreateCoefficientAsync(Guid createdBy, CreateCoefficientRequest request, CancellationToken ct)
    {
        var lastVersion = await _context.Coefficients.AsNoTracking()
            .Where(c => c.Type == request.Type)
            .Select(c => (int?)c.Version)
            .MaxAsync(ct) ?? 0;

        var entity = new Coefficient
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Share = request.Share,
            RejectK = request.RejectK,
            Version = lastVersion + 1,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            Reason = request.Reason,
        };

        _context.Coefficients.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<RetrainJobDto>> ListRetrainJobsAsync(CancellationToken ct)
    {
        return await _context.RetrainJobs.AsNoTracking()
            .OrderByDescending(j => j.RequestedAt)
            .Select(j => ToDto(j))
            .ToListAsync(ct);
    }

    public async Task<RetrainJobDto> CreateRetrainJobAsync(Guid requestedBy, CreateRetrainJobRequest request, CancellationToken ct)
    {
        var entity = new RetrainJob
        {
            Id = Guid.NewGuid(),
            RequestedBy = requestedBy,
            RequestedAt = DateTimeOffset.UtcNow,
            ParamsJson = request.ParamsJson,
            Status = "requested",
        };

        _context.RetrainJobs.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<IgnoredRangeDto>> ListIgnoredRangesAsync(CancellationToken ct)
    {
        return await _context.IgnoredRanges.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToDto(r))
            .ToListAsync(ct);
    }

    public async Task<IgnoredRangeDto> CreateIgnoredRangeAsync(Guid createdBy, CreateIgnoredRangeRequest request, CancellationToken ct)
    {
        var entity = new IgnoredRange
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            ObjectId = request.ObjectId,
            SensorId = request.SensorId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Reason = request.Reason,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.IgnoredRanges.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteIgnoredRangeAsync(Guid id, CancellationToken ct)
    {
        var entity = await _context.IgnoredRanges.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Ignored range {id} not found.");

        _context.IgnoredRanges.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkScheduleEntryDto>> ListWorkScheduleAsync(
        DateTimeOffset? from, DateTimeOffset? to, int? objectId, CancellationToken ct)
    {
        var latest = _context.WorkSchedule.AsNoTracking()
            .Where(w => w.Version == _context.WorkSchedule.Where(x => x.WorkId == w.WorkId).Max(x => x.Version))
            .Where(w => !w.Deleted);

        if (from is not null)
        {
            latest = latest.Where(w => w.EndsAt > from);
        }

        if (to is not null)
        {
            latest = latest.Where(w => w.StartsAt < to);
        }

        if (objectId is not null)
        {
            latest = latest.Where(w => w.ObjectId == objectId);
        }

        var rows = await latest.OrderBy(w => w.StartsAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<WorkScheduleEntryDto> CreateWorkAsync(Guid createdBy, UpsertWorkScheduleEntryRequest request, CancellationToken ct)
    {
        var entity = NewVersion(0, 1, createdBy, request);
        _context.WorkSchedule.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<WorkScheduleEntryDto> UpdateWorkAsync(long workId, Guid createdBy, UpsertWorkScheduleEntryRequest request, CancellationToken ct)
    {
        var last = await LastVersionAsync(workId, ct);
        var entity = NewVersion(workId, last.Version + 1, createdBy, request);
        _context.WorkSchedule.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteWorkAsync(long workId, Guid createdBy, CancellationToken ct)
    {
        var last = await LastVersionAsync(workId, ct);
        _context.WorkSchedule.Add(new WorkScheduleEntry
        {
            WorkId = workId,
            Version = last.Version + 1,
            ObjectId = last.ObjectId,
            WorkKind = last.WorkKind,
            IncidentTypes = last.IncidentTypes,
            RemovedSensor = last.RemovedSensor,
            StartsAt = last.StartsAt,
            EndsAt = last.EndsAt,
            Source = last.Source,
            Comment = last.Comment,
            Deleted = true,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync(ct);
    }

    private async Task<WorkScheduleEntry> LastVersionAsync(long workId, CancellationToken ct)
    {
        var last = await _context.WorkSchedule.AsNoTracking()
            .Where(w => w.WorkId == workId)
            .OrderByDescending(w => w.Version)
            .FirstOrDefaultAsync(ct);

        if (last is null || last.Deleted)
        {
            throw new NotFoundException($"Work {workId} not found.");
        }

        return last;
    }

    private static WorkScheduleEntry NewVersion(long workId, int version, Guid createdBy, UpsertWorkScheduleEntryRequest r) => new()
    {
        WorkId = workId,
        Version = version,
        ObjectId = r.ObjectId,
        WorkKind = r.WorkKind,
        IncidentTypes = r.IncidentTypes?.ToArray() ?? Array.Empty<string>(),
        RemovedSensor = r.RemovedSensor,
        StartsAt = r.StartsAt,
        EndsAt = r.EndsAt,
        Source = r.Source,
        Comment = r.Comment,
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static WorkScheduleEntryDto ToDto(WorkScheduleEntry w) => new()
    {
        WorkId = w.WorkId, Version = w.Version, ObjectId = w.ObjectId, WorkKind = w.WorkKind,
        IncidentTypes = w.IncidentTypes, RemovedSensor = w.RemovedSensor, StartsAt = w.StartsAt, EndsAt = w.EndsAt,
        Source = w.Source, Comment = w.Comment, CreatedBy = w.CreatedBy, CreatedAt = w.CreatedAt,
    };

    private static ModelVersionDto ToDto(ModelVersion v) => new()
    {
        Id = v.Id, Name = v.Name, IsDefault = v.IsDefault, SwitchedAt = v.SwitchedAt,
        SwitchedBy = v.SwitchedBy, CreatedAt = v.CreatedAt,
    };

    private static CoefficientDto ToDto(Coefficient c) => new()
    {
        Id = c.Id, Type = c.Type, Share = c.Share, RejectK = c.RejectK, Version = c.Version,
        CreatedBy = c.CreatedBy, CreatedAt = c.CreatedAt, Reason = c.Reason,
    };

    private static RetrainJobDto ToDto(RetrainJob j) => new()
    {
        Id = j.Id, RequestedBy = j.RequestedBy, RequestedAt = j.RequestedAt, ParamsJson = j.ParamsJson,
        Status = j.Status, StartedAt = j.StartedAt, FinishedAt = j.FinishedAt,
        ResultModelVersionId = j.ResultModelVersionId, LogRef = j.LogRef,
    };

    private static IgnoredRangeDto ToDto(IgnoredRange r) => new()
    {
        Id = r.Id, Scope = r.Scope, ObjectId = r.ObjectId, SensorId = r.SensorId,
        DateFrom = r.DateFrom, DateTo = r.DateTo, Reason = r.Reason, CreatedBy = r.CreatedBy, CreatedAt = r.CreatedAt,
    };
}
