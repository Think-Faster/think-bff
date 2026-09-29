using BFF.Models.Enums;

namespace BFF.Contracts.Tasks;

public sealed class WorkTaskListItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public TaskSourceType SourceType { get; init; }
    public int ObjectId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public WorkTaskStatus Status { get; init; }
    public Guid? DispatcherId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class WorkTaskDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public TaskSourceType SourceType { get; init; }
    public int ObjectId { get; init; }
    public long? PicketId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WorkType { get; init; }
    public string? FaultClassification { get; init; }
    public IReadOnlyList<int> SensorIds { get; init; } = Array.Empty<int>();
    public string? Comment { get; init; }
    public Guid? DispatcherId { get; init; }
    public WorkTaskStatus Status { get; init; }
    public short Priority { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? TakenAt { get; init; }
    public DateTimeOffset? AssignedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public IReadOnlyList<TaskPredictionDto> Predictions { get; init; } = Array.Empty<TaskPredictionDto>();
    public IReadOnlyList<TaskAssignmentDto> Assignments { get; init; } = Array.Empty<TaskAssignmentDto>();
    public IReadOnlyList<TaskReportDto> Reports { get; init; } = Array.Empty<TaskReportDto>();
    public IReadOnlyList<TaskReturnDto> Returns { get; init; } = Array.Empty<TaskReturnDto>();

    /// <summary>Код пикета заявки (PicketId) из справочника — чтобы диспетчер видел, куда ехать.</summary>
    public string? PicketCode { get; init; }

    /// <summary>Датчики заявки (SensorIds) из справочника при чтении: имя, тип, пикет. Нет в справочнике — только SensorId.</summary>
    public IReadOnlyList<TaskSensorDto> Sensors { get; init; } = Array.Empty<TaskSensorDto>();
}

/// <summary>Датчик заявки из справочника: чем его назвать и на каком пикете искать.</summary>
public sealed class TaskSensorDto
{
    public int SensorId { get; init; }
    public string? Name { get; init; }
    public string? SType { get; init; }
    public long? PicketId { get; init; }
    public string? PicketCode { get; init; }
}

public sealed class CreateWorkTaskRequest
{
    public string Number { get; init; } = string.Empty;
    public TaskSourceType SourceType { get; init; }
    public int ObjectId { get; init; }
    public long? PicketId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WorkType { get; init; }
    public string? FaultClassification { get; init; }
    public IReadOnlyList<int>? SensorIds { get; init; }
    public short Priority { get; init; }
}

public sealed class UpdateWorkTaskRequest
{
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WorkType { get; init; }
    public string? FaultClassification { get; init; }
    public string? Comment { get; init; }
    public short Priority { get; init; }
}

public sealed class TaskPredictionDto
{
    public Guid PredictionId { get; init; }
    public Guid AttachedBy { get; init; }
    public DateTimeOffset AttachedAt { get; init; }
    public DateTimeOffset? DetachedAt { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class AttachPredictionRequest
{
    public Guid PredictionId { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class TaskAssignmentDto
{
    public Guid Id { get; init; }
    public Guid EngineerId { get; init; }
    public Guid AssignedBy { get; init; }
    public DateTimeOffset AssignedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

public sealed class CreateTaskAssignmentRequest
{
    public Guid EngineerId { get; init; }
    public string? Comment { get; init; }
}

public sealed class TaskReportDto
{
    public Guid Id { get; init; }
    public Guid EngineerId { get; init; }
    public string? ActualState { get; init; }
    public string? WorksDone { get; init; }
    public string ResultCode { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateTaskReportRequest
{
    public string? ActualState { get; init; }
    public string? WorksDone { get; init; }
    public string ResultCode { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

public sealed class TaskReturnDto
{
    public Guid Id { get; init; }
    public Guid ReturnedBy { get; init; }
    public string TargetType { get; init; } = string.Empty;
    public Guid? TargetUserId { get; init; }
    public string? Comment { get; init; }
    public DateTimeOffset ReturnedAt { get; init; }
}

/// <summary>Кому можно назначить или вернуть заявку: участник группы engineers/dispatchers (с подгруппами).</summary>
public sealed class TaskAssigneeDto
{
    public Guid Id { get; init; }
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
}

/// <summary>Переход без данных — начать работу, закрыть, отменить; комментарий дописывается к заявке.</summary>
public sealed class TaskTransitionRequest
{
    public string? Comment { get; init; }
}

public sealed class CreateTaskReturnRequest
{
    public string TargetType { get; init; } = string.Empty;
    public Guid? TargetUserId { get; init; }
    public string? Comment { get; init; }
}
