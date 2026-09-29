using BFF.Context.Configurations;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Context;

public sealed class BffDbContext : DbContext
{
    public BffDbContext(DbContextOptions<BffDbContext> options) : base(options)
    {
    }

    // D6 — RBAC core
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupClosure> GroupClosures => Set<GroupClosure>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<RbacVersion> RbacVersions => Set<RbacVersion>();

    // D6 — additions (schedule, presence, closures, engineers)
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<AssignedObject> AssignedObjects => Set<AssignedObject>();
    public DbSet<Brigade> Brigades => Set<Brigade>();
    public DbSet<EngineerProfile> EngineerProfiles => Set<EngineerProfile>();
    public DbSet<EngineerPermit> EngineerPermits => Set<EngineerPermit>();

    // D1 — topology
    public DbSet<MonitoringObject> Objects => Set<MonitoringObject>();
    public DbSet<Picket> Pickets => Set<Picket>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorLink> SensorLinks => Set<SensorLink>();
    public DbSet<MapLayer> MapLayers => Set<MapLayer>();

    // D3 — predictions
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<PredictionFactor> PredictionFactors => Set<PredictionFactor>();
    public DbSet<PredictionEvidence> PredictionEvidence => Set<PredictionEvidence>();
    public DbSet<PredictionDecision> PredictionDecisions => Set<PredictionDecision>();
    public DbSet<FactAlert> FactAlerts => Set<FactAlert>();

    // D4 — tasks and works
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskPrediction> TaskPredictions => Set<TaskPrediction>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskReport> TaskReports => Set<TaskReport>();
    public DbSet<TaskReturn> TaskReturns => Set<TaskReturn>();
    public DbSet<Incident> Incidents => Set<Incident>();

    // D8 — admin/model settings
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<Coefficient> Coefficients => Set<Coefficient>();
    public DbSet<RetrainJob> RetrainJobs => Set<RetrainJob>();
    public DbSet<IgnoredRange> IgnoredRanges => Set<IgnoredRange>();
    public DbSet<WorkScheduleEntry> WorkSchedule => Set<WorkScheduleEntry>();

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

        modelBuilder.ApplyConfiguration(new ScheduleEntryConfiguration());
        modelBuilder.ApplyConfiguration(new UserActivityConfiguration());
        modelBuilder.ApplyConfiguration(new AssignedObjectConfiguration());
        modelBuilder.ApplyConfiguration(new BrigadeConfiguration());
        modelBuilder.ApplyConfiguration(new EngineerProfileConfiguration());
        modelBuilder.ApplyConfiguration(new EngineerPermitConfiguration());

        modelBuilder.ApplyConfiguration(new MonitoringObjectConfiguration());
        modelBuilder.ApplyConfiguration(new PicketConfiguration());
        modelBuilder.ApplyConfiguration(new SensorConfiguration());
        modelBuilder.ApplyConfiguration(new SensorLinkConfiguration());
        modelBuilder.ApplyConfiguration(new MapLayerConfiguration());

        modelBuilder.ApplyConfiguration(new PredictionConfiguration());
        modelBuilder.ApplyConfiguration(new PredictionFactorConfiguration());
        modelBuilder.ApplyConfiguration(new PredictionEvidenceConfiguration());
        modelBuilder.ApplyConfiguration(new PredictionDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new FactAlertConfiguration());

        modelBuilder.ApplyConfiguration(new WorkTaskConfiguration());
        modelBuilder.ApplyConfiguration(new TaskPredictionConfiguration());
        modelBuilder.ApplyConfiguration(new TaskAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new TaskReportConfiguration());
        modelBuilder.ApplyConfiguration(new TaskReturnConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());

        modelBuilder.ApplyConfiguration(new ModelVersionConfiguration());
        modelBuilder.ApplyConfiguration(new CoefficientConfiguration());
        modelBuilder.ApplyConfiguration(new RetrainJobConfiguration());
        modelBuilder.ApplyConfiguration(new IgnoredRangeConfiguration());
        modelBuilder.HasSequence<long>("work_schedule_work_id_seq");
        modelBuilder.ApplyConfiguration(new WorkScheduleEntryConfiguration());
    }
}
