using BFF.Contracts.Common;
using BFF.Contracts.Tasks;

namespace BFF.Application.Services;

public interface IWorkTaskService
{
    Task<PagedResult<WorkTaskListItemDto>> ListAsync(
        Guid? dispatcherId, string? status, int page, int pageSize, CancellationToken ct);
    Task<WorkTaskDto> GetAsync(Guid id, CancellationToken ct);
    Task<WorkTaskDto> CreateAsync(CreateWorkTaskRequest request, CancellationToken ct);
    Task<WorkTaskDto> UpdateAsync(Guid id, UpdateWorkTaskRequest request, CancellationToken ct);

    /// <summary>Atomic "first to take it, owns it" transition NEW -> IN_WORK (section 4.1 of the source
    /// spec). Throws ConflictException if someone else already took it.</summary>
    Task<WorkTaskDto> TakeAsync(Guid id, Guid dispatcherId, CancellationToken ct);

    Task AttachPredictionAsync(Guid id, Guid userId, AttachPredictionRequest request, CancellationToken ct);
    Task DetachPredictionAsync(Guid id, Guid predictionId, CancellationToken ct);

    Task<TaskAssignmentDto> AssignAsync(Guid id, Guid assignedBy, CreateTaskAssignmentRequest request, CancellationToken ct);
    Task<TaskReportDto> AddReportAsync(Guid id, Guid engineerId, CreateTaskReportRequest request, CancellationToken ct);
    Task<TaskReturnDto> ReturnAsync(Guid id, Guid returnedBy, CreateTaskReturnRequest request, CancellationToken ct);

    /// <summary>Инженер приступил: Assigned / ReturnedToWork -> EngineerWorking.</summary>
    Task<WorkTaskDto> StartAsync(Guid id, TaskTransitionRequest request, CancellationToken ct);

    /// <summary>Диспетчер принял отчёт: Completed -> Closed, прикреплённые прогнозы закрываются.</summary>
    Task<WorkTaskDto> CloseAsync(Guid id, TaskTransitionRequest request, CancellationToken ct);

    /// <summary>Отмена из любого активного статуса -> Cancelled, прикреплённые прогнозы закрываются.</summary>
    Task<WorkTaskDto> CancelAsync(Guid id, TaskTransitionRequest request, CancellationToken ct);

    Task<IReadOnlyList<TaskAssigneeDto>> ListAssigneesAsync(string groupCode, CancellationToken ct);
}
