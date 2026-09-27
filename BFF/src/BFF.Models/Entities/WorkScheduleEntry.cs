using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Строка графика плановых работ. Правка не перезаписывает строку, а добавляет версию;
/// удаление — версия с Deleted. Действует последняя версия работы, если она не удалена.</summary>
public sealed class WorkScheduleEntry
{
    public long WorkId { get; set; }
    public int Version { get; set; }
    /// <summary>Объект любого уровня; строка на коллектор накрывает все его объекты. Пусто — объект ещё не выбран.</summary>
    public int? ObjectId { get; set; }
    public string WorkKind { get; set; } = string.Empty;
    public string[] IncidentTypes { get; set; } = Array.Empty<string>();
    public string? RemovedSensor { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public WorkSource Source { get; set; }
    public string? Comment { get; set; }
    public bool Deleted { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
