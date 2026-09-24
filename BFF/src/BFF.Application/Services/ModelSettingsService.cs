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
