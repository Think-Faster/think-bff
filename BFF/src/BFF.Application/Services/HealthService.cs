using BFF.Context;

namespace BFF.Application.Services;

public sealed class HealthService : IHealthService
{
    private readonly BffDbContext _context;

    public HealthService(BffDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsDatabaseReadyAsync(CancellationToken ct) => _context.Database.CanConnectAsync(ct);
}
