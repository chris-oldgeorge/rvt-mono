using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rvt.Monitor.Common.Data.Entities;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public abstract class MonitorDbContextBase : DbContext
{
    protected MonitorDbContextBase(DbContextOptions options, MonitorDbOptions monitorOptions)
        : base(options)
    {
        MonitorOptions = monitorOptions;
    }

    protected MonitorDbOptions MonitorOptions { get; }

    internal MonitorDatabaseProvider ModelCacheProvider => MonitorOptions.Provider;
    internal IReadOnlyDictionary<string, string> ModelCacheIdentifierMap => MonitorOptions.IdentifierMap;

    public DbSet<MonitorEntity> Monitors => Set<MonitorEntity>();
    public DbSet<DeploymentEntity> Deployments => Set<DeploymentEntity>();
    public DbSet<ContractEntity> Contracts => Set<ContractEntity>();
    public DbSet<SiteEntity> Sites => Set<SiteEntity>();
    public DbSet<RvtAlertRuleEntity> AlertRules => Set<RvtAlertRuleEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<MonitorDeliveryOutboxEntity> DeliveryOutbox => Set<MonitorDeliveryOutboxEntity>();
    public DbSet<NotificationSentEntity> NotificationAudits => Set<NotificationSentEntity>();
    public DbSet<NotificationSettingEntity> NotificationSettings => Set<NotificationSettingEntity>();
    public DbSet<AspNetUserEntity> Users => Set<AspNetUserEntity>();
    public DbSet<SiteUserEntity> SiteUsers => Set<SiteUserEntity>();
    public DbSet<SiteAverageEntity> SiteAverages => Set<SiteAverageEntity>();
    public DbSet<ErrorMessageEntity> ErrorMessages => Set<ErrorMessageEntity>();
    public DbSet<AlertOccurrenceEntity> AlertOccurrences => Set<AlertOccurrenceEntity>();
    public DbSet<AlertDeliveryOutboxEntity> AlertDeliveryOutbox => Set<AlertDeliveryOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySharedMonitorMappings(MonitorOptions);
        OnMonitorModelCreating(modelBuilder);
    }

    protected virtual void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, MonitorModelCacheKeyFactory>();
    }
}
