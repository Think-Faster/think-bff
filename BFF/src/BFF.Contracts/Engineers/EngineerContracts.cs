using BFF.Models.Enums;

namespace BFF.Contracts.Engineers;

public sealed class BrigadeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public Guid? LeaderId { get; init; }
}

public sealed class CreateBrigadeRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public Guid? LeaderId { get; init; }
}

public sealed class EngineerProfileDto
{
    public Guid UserId { get; init; }
    public Guid? BrigadeId { get; init; }
    public string? Phone { get; init; }

    /// <summary>Telegram пользователя (users.telegram) — один на все разделы, не только у инженера.</summary>
    public string? Telegram { get; init; }
    public IReadOnlyList<string> Specialization { get; init; } = Array.Empty<string>();
    public EngineerStatus Status { get; init; }
}

public sealed class UpsertEngineerProfileRequest
{
    public Guid? BrigadeId { get; init; }
    public string? Phone { get; init; }

    /// <summary>Пишется в users.telegram. null — не менять, пустая строка — убрать.</summary>
    public string? Telegram { get; init; }
    public IReadOnlyList<string>? Specialization { get; init; }
    public EngineerStatus Status { get; init; }
}

public sealed class EngineerPermitDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public PermitKind Kind { get; init; }
    public short? Level { get; init; }
    public DateOnly ValidUntil { get; init; }
    public string? DocumentNo { get; init; }
    public Guid? CheckedBy { get; init; }
    public DateOnly? CheckedAt { get; init; }
}

public sealed class CreateEngineerPermitRequest
{
    public PermitKind Kind { get; init; }
    public short? Level { get; init; }
    public DateOnly ValidUntil { get; init; }
    public string? DocumentNo { get; init; }
    public DateOnly? CheckedAt { get; init; }
}
