using BFF.Models.Enums;

namespace BFF.Contracts.Engineers;

public sealed class BrigadeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class CreateBrigadeRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed class EngineerProfileDto
{
    public Guid UserId { get; init; }
    public Guid? BrigadeId { get; init; }
    public string? Phone { get; init; }
    public string? Telegram { get; init; }
    public IReadOnlyList<string> Specialization { get; init; } = Array.Empty<string>();
    public EngineerStatus Status { get; init; }
}

public sealed class UpsertEngineerProfileRequest
{
    public Guid? BrigadeId { get; init; }
    public string? Phone { get; init; }
    public string? Telegram { get; init; }
    public IReadOnlyList<string>? Specialization { get; init; }
    public EngineerStatus Status { get; init; }
}
