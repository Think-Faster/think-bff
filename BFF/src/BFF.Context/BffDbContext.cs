using BFF.Context.Configurations;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Context;

public sealed class BffDbContext : DbContext
{
    public BffDbContext(DbContextOptions<BffDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupClosure> GroupClosures => Set<GroupClosure>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<RbacVersion> RbacVersions => Set<RbacVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // No schema is configured here on purpose: the schema comes from the connection string's
        // Search Path so a single migration works across environments. See docs/DECISIONS.md and
        // section 5 of the spec.
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new GroupClosureConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceConfiguration());
        modelBuilder.ApplyConfiguration(new AccessGrantConfiguration());
        modelBuilder.ApplyConfiguration(new RbacVersionConfiguration());
    }
}
