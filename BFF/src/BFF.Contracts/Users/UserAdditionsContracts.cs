namespace BFF.Contracts.Users;

public sealed class AssignedObjectDto
{
    public Guid UserId { get; init; }
    public int ObjectId { get; init; }
    public Guid AssignedBy { get; init; }
    public DateTimeOffset AssignedAt { get; init; }
    public string? Note { get; init; }
}

public sealed class AssignObjectRequest
{
    public int ObjectId { get; init; }
    public string? Note { get; init; }
}

public sealed class PresenceDto
{
    public Guid UserId { get; init; }
    public bool IsOnline { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
    public string? LastAction { get; init; }
}
