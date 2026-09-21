namespace BFF.Application.Services;

/// <summary>Not part of the original section-3 file map — added so HealthController (which, per the
/// dependency diagram, may not reference BFF.Context directly) can still ask "is the database reachable"
/// through the Application layer for GET /health/ready (section 10.4).</summary>
public interface IHealthService
{
    Task<bool> IsDatabaseReadyAsync(CancellationToken ct);
}
