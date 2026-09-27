using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Tasks;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class WorkTaskService : IWorkTaskService
{
    private readonly BffDbContext _context;

    public WorkTaskService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WorkTaskListItemDto>> ListAsync(
        Guid? dispatcherId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Tasks.AsNoTracking().AsQueryable();

        if (dispatcherId is { } did)
        {
            query = query.Where(t => t.DispatcherId == did);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkTaskStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                throw new ArgumentException($"Unknown task status: {status}", nameof(status));
            }

            query = query.Where(t => t.Status == parsedStatus);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new WorkTaskListItemDto
            {
                Id = t.Id,
                Number = t.Number,
                SourceType = t.SourceType,
                ObjectId = t.ObjectId,
                Topic = t.Topic,
                Status = t.Status,
                DispatcherId = t.DispatcherId,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync(ct);

        return new PagedResult<WorkTaskListItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<WorkTaskDto> GetAsync(Guid id, CancellationToken ct)
    {
        var task = await _context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

        var predictions = await _context.TaskPredictions.AsNoTracking().Where(tp => tp.TaskId == id).ToListAsync(ct);
        var assignments = await _context.TaskAssignments.AsNoTracking().Where(a => a.TaskId == id).ToListAsync(ct);
        var reports = await _context.TaskReports.AsNoTracking().Where(r => r.TaskId == id).ToListAsync(ct);
        var returns = await _context.TaskReturns.AsNoTracking().Where(r => r.TaskId == id).ToListAsync(ct);

        return ToDto(task, predictions, assignments, reports, returns);
    }

    public async Task<WorkTaskDto> CreateAsync(CreateWorkTaskRequest request, CancellationToken ct)
    {
        var duplicateNumber = await _context.Tasks.AsNoTracking().AnyAsync(t => t.Number == request.Number, ct);
        if (duplicateNumber)
        {
            throw new ConflictException($"Task number '{request.Number}' already exists.", "duplicate_code");
        }

        var entity = new WorkTask
        {
            Id = Guid.NewGuid(),
            Number = request.Number,
            SourceType = request.SourceType,
            ObjectId = request.ObjectId,
            PicketId = request.PicketId,
            Topic = request.Topic,
            Description = request.Description,
            WorkType = request.WorkType,
            FaultClassification = request.FaultClassification,
            SensorIds = request.SensorIds?.ToArray() ?? Array.Empty<int>(),
            Priority = request.Priority,
            Status = WorkTaskStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.Tasks.Add(entity);
        await _context.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<WorkTaskDto> UpdateAsync(Guid id, UpdateWorkTaskRequest request, CancellationToken ct)
    {
        var entity = await _context.Tasks.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

        entity.Topic = request.Topic;
        entity.Description = request.Description;
        entity.WorkType = request.WorkType;
        entity.FaultClassification = request.FaultClassification;
        entity.Comment = request.Comment;
        entity.Priority = request.Priority;

        await _context.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkTaskDto> TakeAsync(Guid id, Guid dispatcherId, CancellationToken ct)
    {
        var exists = await _context.Tasks.AsNoTracking().AnyAsync(t => t.Id == id, ct);
        if (!exists)
        {
            throw new NotFoundException($"Task {id} not found.");
        }

        // Atomic "first to take it, owns it": the WHERE on Status is the guard — a second concurrent
        // TakeAsync for the same task affects 0 rows instead of racing on a read-then-write.
        var affected = await _context.Tasks
            .Where(t => t.Id == id && t.Status == WorkTaskStatus.New)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, WorkTaskStatus.InWork)
                .SetProperty(t => t.DispatcherId, dispatcherId)
                .SetProperty(t => t.TakenAt, DateTimeOffset.UtcNow), ct);

        if (affected == 0)
        {
            throw new ConflictException($"Task {id} was already taken by someone else.", "task_already_taken");
        }

        return await GetAsync(id, ct);
    }

    public async Task AttachPredictionAsync(Guid id, Guid userId, AttachPredictionRequest request, CancellationToken ct)
    {
        var taskExists = await _context.Tasks.AsNoTracking().AnyAsync(t => t.Id == id, ct);
        var predictionExists = await _context.Predictions.AsNoTracking().AnyAsync(p => p.Id == request.PredictionId, ct);
        if (!taskExists || !predictionExists)
        {
            throw new NotFoundException("Task or prediction not found.");
        }

        var alreadyAttached = await _context.TaskPredictions.AsNoTracking()
            .AnyAsync(tp => tp.TaskId == id && tp.PredictionId == request.PredictionId, ct);
        if (alreadyAttached)
        {
            return;
        }

        _context.TaskPredictions.Add(new TaskPrediction
        {
            TaskId = id,
            PredictionId = request.PredictionId,
            AttachedBy = userId,
            AttachedAt = DateTimeOffset.UtcNow,
            IsPrimary = request.IsPrimary,
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task DetachPredictionAsync(Guid id, Guid predictionId, CancellationToken ct)
    {
        var link = await _context.TaskPredictions.SingleOrDefaultAsync(
            tp => tp.TaskId == id && tp.PredictionId == predictionId, ct);

        if (link is null || link.DetachedAt is not null)
        {
            return;
        }

        // Kept for history, per section 4.2 of the source spec, not deleted.
        link.DetachedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<TaskAssignmentDto> AssignAsync(Guid id, Guid assignedBy, CreateTaskAssignmentRequest request, CancellationToken ct)
    {
        var task = await _context.Tasks.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

        var assignment = new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            EngineerId = request.EngineerId,
            AssignedBy = assignedBy,
            AssignedAt = DateTimeOffset.UtcNow,
            Status = "assigned",
            Comment = request.Comment,
        };

        _context.TaskAssignments.Add(assignment);

        if (task.Status is WorkTaskStatus.New or WorkTaskStatus.InWork)
        {
            task.Status = WorkTaskStatus.Assigned;
            task.AssignedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return ToDto(assignment);
    }

    public async Task<TaskReportDto> AddReportAsync(Guid id, Guid engineerId, CreateTaskReportRequest request, CancellationToken ct)
    {
        var task = await _context.Tasks.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

        var report = new TaskReport
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            EngineerId = engineerId,
            ActualState = request.ActualState,
            WorksDone = request.WorksDone,
            ResultCode = request.ResultCode,
            Comment = request.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.TaskReports.Add(report);
        task.Status = WorkTaskStatus.Completed;
        task.CompletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(report);
    }

    public async Task<TaskReturnDto> ReturnAsync(Guid id, Guid returnedBy, CreateTaskReturnRequest request, CancellationToken ct)
    {
        var task = await _context.Tasks.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

        var taskReturn = new TaskReturn
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            ReturnedBy = returnedBy,
            TargetType = request.TargetType,
            TargetUserId = request.TargetUserId,
            Comment = request.Comment,
            ReturnedAt = DateTimeOffset.UtcNow,
        };

        _context.TaskReturns.Add(taskReturn);
        task.Status = WorkTaskStatus.ReturnedToWork;
        task.DispatcherId = request.TargetUserId;

        await _context.SaveChangesAsync(ct);
        return ToDto(taskReturn);
    }

    private static WorkTaskDto ToDto(
        WorkTask t,
        List<TaskPrediction> predictions,
        List<TaskAssignment> assignments,
        List<TaskReport> reports,
        List<TaskReturn> returns) => new()
    {
        Id = t.Id,
        Number = t.Number,
        SourceType = t.SourceType,
        ObjectId = t.ObjectId,
        PicketId = t.PicketId,
        Topic = t.Topic,
        Description = t.Description,
        WorkType = t.WorkType,
        FaultClassification = t.FaultClassification,
        SensorIds = t.SensorIds,
        Comment = t.Comment,
        DispatcherId = t.DispatcherId,
        Status = t.Status,
        Priority = t.Priority,
        CreatedAt = t.CreatedAt,
        TakenAt = t.TakenAt,
        AssignedAt = t.AssignedAt,
        CompletedAt = t.CompletedAt,
        ClosedAt = t.ClosedAt,
        Predictions = predictions.Select(tp => new TaskPredictionDto
        {
            PredictionId = tp.PredictionId, AttachedBy = tp.AttachedBy, AttachedAt = tp.AttachedAt,
            DetachedAt = tp.DetachedAt, IsPrimary = tp.IsPrimary,
        }).ToArray(),
        Assignments = assignments.Select(ToDto).ToArray(),
        Reports = reports.Select(ToDto).ToArray(),
        Returns = returns.Select(ToDto).ToArray(),
    };

    private static TaskAssignmentDto ToDto(TaskAssignment a) => new()
    {
        Id = a.Id, EngineerId = a.EngineerId, AssignedBy = a.AssignedBy, AssignedAt = a.AssignedAt,
        Status = a.Status, Comment = a.Comment,
    };

    private static TaskReportDto ToDto(TaskReport r) => new()
    {
        Id = r.Id, EngineerId = r.EngineerId, ActualState = r.ActualState, WorksDone = r.WorksDone,
        ResultCode = r.ResultCode, Comment = r.Comment, CreatedAt = r.CreatedAt,
    };

    private static TaskReturnDto ToDto(TaskReturn r) => new()
    {
        Id = r.Id, ReturnedBy = r.ReturnedBy, TargetType = r.TargetType, TargetUserId = r.TargetUserId,
        Comment = r.Comment, ReturnedAt = r.ReturnedAt,
    };
}
