namespace BFF.Contracts.Readings;

/// <summary>Whose sensor readings the current user may see in the «Логи» window. Readings themselves are
/// stored and streamed by tf-funnel (GET /api/funnel/log, /api/funnel/stream); tf-funnel asks this with the
/// user's own token and filters by <see cref="SensorIds"/>.</summary>
public sealed class ReadingsScopeDto
{
    /// <summary>True — any object (resource <c>readings</c>, read): dispatchers, chief dispatchers, admins.
    /// False — only objects of the user's tasks in progress (engineers).</summary>
    public bool All { get; init; }

    /// <summary>Requested objects the user may open. Without a request: the objects of the user's tasks in
    /// progress (empty when <see cref="All"/> is true).</summary>
    public IReadOnlyList<int> ObjectIds { get; init; } = Array.Empty<int>();

    /// <summary>Sensors of those objects and of all their descendants — ids equal
    /// <c>ид_канала_данных</c> in tf-funnel.</summary>
    public IReadOnlyList<int> SensorIds { get; init; } = Array.Empty<int>();
}
