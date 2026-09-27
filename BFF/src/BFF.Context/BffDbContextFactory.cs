using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BFF.Context;

/// <summary>
/// Design-time factory used by `dotnet ef migrations add`. No database connection is ever opened
/// here: the connection string below is a placeholder only used to let Npgsql generate SQL.
/// </summary>
public sealed class BffDbContextFactory : IDesignTimeDbContextFactory<BffDbContext>
{
    public BffDbContext CreateDbContext(string[] args)
    {
        var schema = Environment.GetEnvironmentVariable("DB_SCHEMA") ?? "bff";
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? $"Host=localhost;Database=tf;Username=postgres;Password=postgres;Search Path={schema}";

        var optionsBuilder = new DbContextOptionsBuilder<BffDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema));

        return new BffDbContext(optionsBuilder.Options);
    }
}
