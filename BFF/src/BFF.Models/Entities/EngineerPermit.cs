using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Допуск инженера: ОЗП (группа 1–3), газоопасные работы, электробезопасность (группа 2–5).
/// Просроченный допуск не удаляется — человек просто перестаёт проходить подбор звена.</summary>
public sealed class EngineerPermit
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public PermitKind Kind { get; set; }
    public short? Level { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? DocumentNo { get; set; }
    public Guid? CheckedBy { get; set; }
    public DateOnly? CheckedAt { get; set; }
}
