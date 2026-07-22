using Microsoft.EntityFrameworkCore;
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
        using var context = ReportingContextFactory.CreatePostgreSqlContext();

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

    [Fact]
    public void Model_UsesCanonicalDeploymentAndContractOwnershipRelations()
    {
        using var context = ReportingContextFactory.CreatePostgreSqlContext();

        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "deployment");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "contract");
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.GetViewName() == "monitor_windows");
    }

    [Fact]
    public void Model_MapsIdentityColumnsUsingTheirQuotedPhysicalNames()
    {
        using var context = ReportingContextFactory.CreatePostgreSqlContext();

        var user = context.Model.FindEntityType(typeof(AspNetUserEntity))!;

        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.Equal("Id", user.FindProperty(nameof(AspNetUserEntity.Id))!.GetColumnName());
        Assert.Equal("Email", user.FindProperty(nameof(AspNetUserEntity.Email))!.GetColumnName());
    }

    private static void AssertKeylessReadModel<TEntity>(ReportingMonitorContext context, string canonicalName)
        where TEntity : class
    {
        var entity = context.Model.FindEntityType(typeof(TEntity))!;

        Assert.Null(entity.FindPrimaryKey());
        Assert.Equal(canonicalName, entity.GetViewName());
    }
}

internal static class ReportingContextFactory
{
    public static ReportingMonitorContext CreatePostgreSqlContext()
    {
        var monitorOptions = new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>());
        var options = MonitorDbContextOptionsFactory.CreateOptions<ReportingMonitorContext>(
            "Host=localhost;Database=reporting_mapping_tests;Username=reporting;Password=reporting",
            monitorOptions);

        return new ReportingMonitorContext(options, monitorOptions);
    }
}
