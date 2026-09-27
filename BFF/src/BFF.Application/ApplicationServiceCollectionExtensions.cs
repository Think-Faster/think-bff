using BFF.Application.Services;
using BFF.Context;
using BFF.Context.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BFF.Application;

/// <summary>
/// Composition root for everything below the HTTP layer: DbContext, repositories, application services
/// and validators. BFF.WebApi only references BFF.Application/Contracts/Infrastructure (section 3's
/// dependency diagram has no WebApi -> Context edge), so DbContext and repository registration — which
/// need BFF.Context types — live here instead of in WebApi's own ServiceCollectionExtensions.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BffDbContext>(options => options.UseNpgsql(BuildConnectionString(configuration), npgsql =>
        {
            var schema = RequireEnv(configuration, "DB_SCHEMA");
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
        }));

        services.AddScoped<IGroupGraphRepository, GroupGraphRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        services.AddMemoryCache();
        services.Configure<PermissionCacheOptions>(configuration.GetSection(PermissionCacheOptions.SectionName));
        // Scoped, not singleton: it depends on IPermissionRepository, which depends on the (scoped)
        // DbContext. The cached entries themselves are still process-wide, since IMemoryCache underneath
        // is a singleton — only this thin wrapper is scoped per request.
        services.AddScoped<IPermissionCache, PermissionCache>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IHealthService, HealthService>();

        // D1 — topology
        services.AddScoped<IObjectService, ObjectService>();
        services.AddScoped<ISensorService, SensorService>();
        services.AddScoped<IReadingsScopeService, ReadingsScopeService>();

        // D3 — predictions
        services.AddScoped<IPredictionService, PredictionService>();

        // D4 — tasks and works
        services.AddScoped<IWorkTaskService, WorkTaskService>();
        services.AddScoped<IIncidentService, IncidentService>();

        // D6 — additions
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IAssignedObjectService, AssignedObjectService>();
        services.AddScoped<IEngineerService, EngineerService>();
        services.Configure<PresenceOptions>(configuration.GetSection(PresenceOptions.SectionName));
        services.AddScoped<IPresenceTracker, PresenceTracker>();

        // D8 — admin/model settings
        services.AddScoped<IModelSettingsService, ModelSettingsService>();

        services.AddValidatorsFromAssemblyContaining(typeof(ApplicationServiceCollectionExtensions));

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = RequireEnv(configuration, "DB_HOST"),
            Port = int.TryParse(configuration["DB_PORT"], out var port) ? port : 5432,
            Database = RequireEnv(configuration, "DB_NAME"),
            Username = RequireEnv(configuration, "DB_USER"),
            Password = RequireEnv(configuration, "DB_PASSWORD"),
            SearchPath = RequireEnv(configuration, "DB_SCHEMA"),
            MaxPoolSize = int.TryParse(configuration["DB_MAX_POOL_SIZE"], out var maxPool) ? maxPool : 50,
        };

        return builder.ConnectionString;
    }

    private static string RequireEnv(IConfiguration configuration, string key)
        => configuration[key] ?? throw new InvalidOperationException($"Required environment variable '{key}' is not set.");
}
