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
        Guid? dispatcherId, Guid? engineerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Tasks.AsNoTracking().AsQueryable();

        if (dispatcherId is { } did)
        {
            query = query.Where(t => t.DispatcherId == did);
        }

        if (engineerId is { } eid)
        {
            query = query.Where(t => _context.TaskAssignments
                .Any(a => a.TaskId == t.Id && a.EngineerId == eid && a.Status == AssignmentAssigned));
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
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "assign", WorkTaskStatus.New, WorkTaskStatus.InWork, WorkTaskStatus.Assigned,
            WorkTaskStatus.EngineerWorking, WorkTaskStatus.ReturnedToWork);

        // Переназначение: прежний исполнитель снят, в истории остаётся.
        var previous = await _context.TaskAssignments
            .Where(a => a.TaskId == id && a.Status == AssignmentAssigned).ToListAsync(ct);
        foreach (var old in previous)
        {
            old.Status = AssignmentReplaced;
        }

        var assignment = new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            EngineerId = request.EngineerId,
            AssignedBy = assignedBy,
            AssignedAt = DateTimeOffset.UtcNow,
            Status = AssignmentAssigned,
            Comment = request.Comment,
        };

        _context.TaskAssignments.Add(assignment);

        // Назначил из общей очереди — заявка теперь его.
        if (task.DispatcherId is null)
        {
            task.DispatcherId = assignedBy;
            task.TakenAt ??= DateTimeOffset.UtcNow;
        }

        task.Status = WorkTaskStatus.Assigned;
        task.AssignedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(assignment);
    }

    public async Task<TaskReportDto> AddReportAsync(Guid id, Guid engineerId, CreateTaskReportRequest request, CancellationToken ct)
    {
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "report", WorkTaskStatus.Assigned, WorkTaskStatus.EngineerWorking,
            WorkTaskStatus.ReturnedToWork);

        var hasEngineer = await _context.TaskAssignments.AsNoTracking()
            .AnyAsync(a => a.TaskId == id && a.Status == AssignmentAssigned, ct);
        if (!hasEngineer)
        {
            throw new ConflictException($"Task {id} has no assigned engineer to report.", "invalid_status");
        }

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
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "return", WorkTaskStatus.InWork, WorkTaskStatus.Assigned,
            WorkTaskStatus.EngineerWorking, WorkTaskStatus.Completed, WorkTaskStatus.ReturnedToWork);

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

        switch (request.TargetType)
        {
            case "queue":
                // В общую очередь: снова New без хозяина — её возьмёт первый (take только из New).
                task.Status = WorkTaskStatus.New;
                task.DispatcherId = null;
                task.TakenAt = null;
                break;
            case "dispatcher":
                task.Status = WorkTaskStatus.ReturnedToWork;
                task.DispatcherId = request.TargetUserId;
                break;
            default:
                // "incident" — доработка тем же диспетчером.
                task.Status = WorkTaskStatus.ReturnedToWork;
                break;
        }

        task.CompletedAt = null;

        await _context.SaveChangesAsync(ct);
        return ToDto(taskReturn);
    }

    public async Task<WorkTaskDto> StartAsync(Guid id, TaskTransitionRequest request, CancellationToken ct)
    {
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "start", WorkTaskStatus.Assigned, WorkTaskStatus.ReturnedToWork);

        var hasEngineer = await _context.TaskAssignments.AsNoTracking()
            .AnyAsync(a => a.TaskId == id && a.Status == AssignmentAssigned, ct);
        if (!hasEngineer)
        {
            throw new ConflictException($"Task {id} has no assigned engineer.", "invalid_status");
        }

        task.Status = WorkTaskStatus.EngineerWorking;
        AppendComment(task, request.Comment);
        await _context.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkTaskDto> CloseAsync(Guid id, TaskTransitionRequest request, CancellationToken ct)
    {
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "close", WorkTaskStatus.Completed);

        task.Status = WorkTaskStatus.Closed;
        task.ClosedAt = DateTimeOffset.UtcNow;
        AppendComment(task, request.Comment);
        await CloseAttachedPredictionsAsync(id, ct);
        await _context.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkTaskDto> CancelAsync(Guid id, TaskTransitionRequest request, CancellationToken ct)
    {
        var task = await LoadAsync(id, ct);
        EnsureStatus(task, "cancel", WorkTaskStatus.New, WorkTaskStatus.InWork, WorkTaskStatus.Assigned,
            WorkTaskStatus.EngineerWorking, WorkTaskStatus.ReturnedToWork);

        task.Status = WorkTaskStatus.Cancelled;
        task.ClosedAt = DateTimeOffset.UtcNow;
        AppendComment(task, request.Comment);
        await CloseAttachedPredictionsAsync(id, ct);
        await _context.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Активные пользователи группы groupCode и всех её подгрупп (group_closure). Диспетчеру
    /// права users/groups не выданы, а выбрать инженера или диспетчера для заявки ему нужно.</summary>
    public async Task<IReadOnlyList<TaskAssigneeDto>> ListAssigneesAsync(string groupCode, CancellationToken ct)
    {
        var groups = _context.GroupClosures
            .Where(c => _context.Groups.Any(g => g.Id == c.AncestorId && g.Code == groupCode))
            .Select(c => c.DescendantId);
        var userIds = _context.GroupMembers
            .Where(m => m.MemberType == MemberType.User && groups.Contains(m.GroupId))
            .Select(m => m.MemberId);

        return await _context.Users.AsNoTracking()
            .Where(u => u.IsActive && userIds.Contains(u.Id))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new TaskAssigneeDto
            {
                Id = u.Id, LastName = u.LastName, FirstName = u.FirstName, MiddleName = u.MiddleName,
            })
            .ToListAsync(ct);
    }

    private const string AssignmentAssigned = "assigned";
    private const string AssignmentReplaced = "replaced";

    private async Task<WorkTask> LoadAsync(Guid id, CancellationToken ct)
        => await _context.Tasks.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Task {id} not found.");

    private static void EnsureStatus(WorkTask task, string action, params WorkTaskStatus[] allowed)
    {
        if (!allowed.Contains(task.Status))
        {
            throw new ConflictException(
                $"Task {task.Id} is {task.Status}; '{action}' is not allowed.", "invalid_status");
        }
    }

    private static void AppendComment(WorkTask task, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        task.Comment = string.IsNullOrWhiteSpace(task.Comment) ? comment.Trim() : $"{task.Comment}\n{comment.Trim()}";
    }

    /// <summary>Заявка закрыта — взятые по ней прогнозы тоже (статус Closed), чтобы не висели в работе.</summary>
    private async Task CloseAttachedPredictionsAsync(Guid taskId, CancellationToken ct)
    {
        var predictionIds = _context.TaskPredictions
            .Where(tp => tp.TaskId == taskId && tp.DetachedAt == null).Select(tp => tp.PredictionId);
        var predictions = await _context.Predictions
            .Where(p => predictionIds.Contains(p.Id) && p.Status == PredictionStatus.Taken).ToListAsync(ct);
        foreach (var prediction in predictions)
        {
            prediction.Status = PredictionStatus.Closed;
        }
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
