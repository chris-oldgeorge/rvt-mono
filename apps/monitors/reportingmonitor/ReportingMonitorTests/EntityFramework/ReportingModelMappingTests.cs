using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ReportingMonitor.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace ReportingMonitorTests.EntityFramework;

public sealed class ReportingModelMappingTests
{
    [Fact]
    public void Model_MapsReportWritesAndReadViewsToCanonicalPostgreSqlNames()
    {
        using ReportingMonitorContext context = ReportingContextFactory.CreatePostgreSqlContext();

        Assert.Equal("report_rule", context.Model.FindEntityType(typeof(ReportRuleEntity))!.GetTableName());
        Assert.Equal("report", context.Model.FindEntityType(typeof(ReportEntity))!.GetTableName());
        Assert.Equal("report_sent", context.Model.FindEntityType(typeof(ReportSentEntity))!.GetTableName());
        Assert.Equal("is_hidden_system_rule", context.Model.FindEntityType(typeof(ReportRuleEntity))!
            .FindProperty(nameof(ReportRuleEntity.IsHiddenSystemRule))!.GetColumnName());
        Assert.Equal("report_date", context.Model.FindEntityType(typeof(ReportEntity))!
            .FindProperty(nameof(ReportEntity.ReportDate))!.GetColumnName());
        Assert.Equal("send_time", context.Model.FindEntityType(typeof(ReportSentEntity))!
            .FindProperty(nameof(ReportSentEntity.SendTime))!.GetColumnName());
        Assert.Null(context.Model.FindEntityType(typeof(SiteSearchRow))!.FindPrimaryKey());
        Assert.Equal("site_search", context.Model.FindEntityType(typeof(SiteSearchRow))!.GetViewName());
        AssertKeylessReadModel<MonitorReportRow>(context, "monitor_report");
        AssertKeylessReadModel<ReportRecipientRow>(context, "report_user");
        AssertKeylessReadModel<ReportingNotificationRow>(context, "notification");
        AssertKeylessReadModel<ReportingAlertRuleRow>(context, "rvt_alert_rule");
        AssertKeylessReadModel<DustHourlyAverageRow>(context, "my_atm_dust_level");
        AssertKeylessReadModel<DustDailyAverageRow>(context, "my_atm_dust_level_1_day_avg");
        AssertKeylessReadModel<NoiseHourlyAverageRow>(context, "noise_level_1_hour_avg");
        AssertKeylessReadModel<NoiseDailyAverageRow>(context, "noise_level_1_day_avg");
        AssertKeylessReadModel<NoiseSiteAverageRow>(context, "noise_level_site_avg");
        AssertKeylessReadModel<VibrationDailyPeakRow>(context, "omnidots_peak_level_1_day_peak");

        Assert.Equal(typeof(double), context.Model.FindEntityType(typeof(ReportingNotificationRow))!
            .FindProperty(nameof(ReportingNotificationRow.LimitOn))!.ClrType);
        Assert.Equal(typeof(double), context.Model.FindEntityType(typeof(ReportingNotificationRow))!
            .FindProperty(nameof(ReportingNotificationRow.Level))!.ClrType);
        Assert.Equal(typeof(double), context.Model.FindEntityType(typeof(ReportingAlertRuleRow))!
            .FindProperty(nameof(ReportingAlertRuleRow.LimitOn))!.ClrType);
    }

    /// <summary>
    /// The model and 2026-07-31-add-unique-scheduled-report-period.sql have to describe
    /// the same index, filter included: the filter is what keeps one-time reports - which
    /// legitimately repeat over a site and period - out of the uniqueness guard.
    /// </summary>
    [Fact]
    public void Model_DeclaresTheScheduledReportPeriodUniquenessBackstop()
    {
        using ReportingMonitorContext context = ReportingContextFactory.CreatePostgreSqlContext();

        IIndex index = Assert.Single(context.Model.FindEntityType(typeof(ReportEntity))!.GetIndexes());

        Assert.Equal("ux_report_scheduled_period", index.GetDatabaseName());
        Assert.True(index.IsUnique);
        Assert.Equal("report_rule_id is not null and frequency <> 5", index.GetFilter());
        Assert.Equal(
            ["report_rule_id", "frequency", "report_from"],
            index.Properties.Select(property => property.GetColumnName()));
    }

    [Fact]
    public void Model_UsesCanonicalDeploymentAndContractOwnershipRelations()
    {
        using ReportingMonitorContext context = ReportingContextFactory.CreatePostgreSqlContext();

        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "deployment");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "contract");
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "monitor_windows");
    }

    [Fact]
    public void Model_MapsIdentityColumnsUsingTheirQuotedPhysicalNames()
    {
        using ReportingMonitorContext context = ReportingContextFactory.CreatePostgreSqlContext();

        IEntityType user = context.Model.FindEntityType(typeof(AspNetUserEntity))!;

        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.Equal("Id", user.FindProperty(nameof(AspNetUserEntity.Id))!.GetColumnName());
        Assert.Equal("Email", user.FindProperty(nameof(AspNetUserEntity.Email))!.GetColumnName());
    }

    private static void AssertKeylessReadModel<TEntity>(ReportingMonitorContext context, string canonicalName)
        where TEntity : class
    {
        IEntityType entity = context.Model.FindEntityType(typeof(TEntity))!;

        Assert.Null(entity.FindPrimaryKey());
        Assert.Equal(canonicalName, entity.GetViewName());
    }
}

internal static class ReportingContextFactory
{
    public static ReportingMonitorContext CreatePostgreSqlContext()
    {
        MonitorDbOptions monitorOptions = new(new Dictionary<string, string>());
        DbContextOptions<ReportingMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<ReportingMonitorContext>(
            "Host=localhost;Database=reporting_mapping_tests;Username=reporting;Password=reporting");

        return new ReportingMonitorContext(options, monitorOptions);
    }
}
