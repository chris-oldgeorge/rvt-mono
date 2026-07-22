using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace ReportingMonitor.Api.Db.EntityFramework;

public sealed class ReportingMonitorContext : MonitorDbContextBase
{
    public ReportingMonitorContext(DbContextOptions<ReportingMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<ReportRuleEntity> ReportRules => Set<ReportRuleEntity>();
    public DbSet<ReportEntity> Reports => Set<ReportEntity>();
    public DbSet<ReportSentEntity> ReportSends => Set<ReportSentEntity>();
    public DbSet<SiteSearchRow> SiteSearchRows => Set<SiteSearchRow>();
    public DbSet<MonitorReportRow> MonitorReportRows => Set<MonitorReportRow>();
    public DbSet<ReportingDeploymentRow> ReportingDeploymentRows => Set<ReportingDeploymentRow>();
    public DbSet<ReportingContractRow> ReportingContractRows => Set<ReportingContractRow>();
    public DbSet<ReportRecipientRow> ReportRecipientRows => Set<ReportRecipientRow>();
    public DbSet<ReportingNotificationRow> ReportingNotificationRows => Set<ReportingNotificationRow>();
    public DbSet<ReportingAlertRuleRow> ReportingAlertRuleRows => Set<ReportingAlertRuleRow>();
    public DbSet<DustHourlyAverageRow> DustHourlyAverageRows => Set<DustHourlyAverageRow>();
    public DbSet<DustDailyAverageRow> DustDailyAverageRows => Set<DustDailyAverageRow>();
    public DbSet<NoiseHourlyAverageRow> NoiseHourlyAverageRows => Set<NoiseHourlyAverageRow>();
    public DbSet<NoiseDailyAverageRow> NoiseDailyAverageRows => Set<NoiseDailyAverageRow>();
    public DbSet<NoiseSiteAverageRow> NoiseSiteAverageRows => Set<NoiseSiteAverageRow>();
    public DbSet<VibrationDailyPeakRow> VibrationDailyPeakRows => Set<VibrationDailyPeakRow>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportRuleEntity>(entity =>
        {
            entity.ToTable("report_rule");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SiteId).HasColumnName("site_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.Frequency).HasColumnName("frequency");
            entity.Property(row => row.DayOfWeek).HasColumnName("day_of_week");
            entity.Property(row => row.DayOfMonth).HasColumnName("day_of_month");
            entity.Property(row => row.LastGenerated).HasColumnName("last_generated");
            entity.Property(row => row.ReportName).HasColumnName("report_name");
            entity.Property(row => row.Deleted).HasColumnName("deleted");
            entity.Property(row => row.IsHiddenSystemRule).HasColumnName("is_hidden_system_rule");
        });

        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.ToTable("report");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SiteId).HasColumnName("site_id");
            entity.Property(row => row.ReportRuleId).HasColumnName("report_rule_id");
            entity.Property(row => row.Frequency).HasColumnName("frequency");
            entity.Property(row => row.ReportDate).HasColumnName("report_date");
            entity.Property(row => row.ReportFrom).HasColumnName("report_from");
            entity.Property(row => row.ReportTo).HasColumnName("report_to");
            entity.Property(row => row.ReportLink).HasColumnName("report_link");
        });

        modelBuilder.Entity<ReportSentEntity>(entity =>
        {
            entity.ToTable("report_sent");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ReportId).HasColumnName("report_id");
            entity.Property(row => row.SendTime).HasColumnName("send_time");
            entity.Property(row => row.Address).HasColumnName("address");
            entity.Property(row => row.ErrorMessage).HasColumnName("error_message");
        });

        ConfigureReadModel<SiteSearchRow>(modelBuilder, "site_search", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SiteName).HasColumnName("site_name");
            entity.Property(row => row.CreateDate).HasColumnName("create_date");
            entity.Property(row => row.AddressLine1).HasColumnName("address_line_1");
            entity.Property(row => row.AddressLine2).HasColumnName("address_line_2");
            entity.Property(row => row.Postcode).HasColumnName("postcode");
            entity.Property(row => row.City).HasColumnName("city");
            entity.Property(row => row.County).HasColumnName("county");
            entity.Property(row => row.Contracts).HasColumnName("contracts");
            entity.Property(row => row.CompanyName).HasColumnName("company_name");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.Archived).HasColumnName("archived");
        });

        ConfigureReadModel<MonitorReportRow>(modelBuilder, "monitor_report", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SiteId).HasColumnName("site_id");
            entity.Property(row => row.Active).HasColumnName("active");
            entity.Property(row => row.DeploymentId).HasColumnName("deployment_id");
            entity.Property(row => row.FleetNumber).HasColumnName("fleet_row_count");
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.TypeOfMonitor).HasColumnName("type_of_monitor");
            entity.Property(row => row.Offline).HasColumnName("off_line");
            entity.Property(row => row.Alerts).HasColumnName("alerts");
            entity.Property(row => row.Cautions).HasColumnName("cautions");
            entity.Property(row => row.Latitude).HasColumnName("latitude");
            entity.Property(row => row.Longitude).HasColumnName("longitude");
            entity.Property(row => row.StartDate).HasColumnName("start_date");
            entity.Property(row => row.EndDate).HasColumnName("end_date");
            entity.Property(row => row.What3Words).HasColumnName("what_3_words");
            entity.Property(row => row.LastDataTime).HasColumnName("last_data_time");
            entity.Property(row => row.Location).HasColumnName("location");
            entity.Property(row => row.CalibrationDate).HasColumnName("calibration_date");
        });

        ConfigureReadModel<ReportingDeploymentRow>(modelBuilder, "deployment", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ContractId).HasColumnName("contract_id");
        });

        ConfigureReadModel<ReportingContractRow>(modelBuilder, "contract", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.OnHireDate).HasColumnName("on_hire_date");
            entity.Property(row => row.OffHireDate).HasColumnName("off_hire_date");
        });

        ConfigureReadModel<ReportRecipientRow>(modelBuilder, "report_user", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ReportRuleId).HasColumnName("report_rule_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
        });

        ConfigureReadModel<ReportingNotificationRow>(modelBuilder, "notification", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.MonitorId).HasColumnName("monitor_id");
            entity.Property(row => row.NotificationTime).HasColumnName("notification_time");
            entity.Property(row => row.LimitOn).HasColumnName("limit_on");
            entity.Property(row => row.AveragingPeriod).HasColumnName("averaging_period");
            entity.Property(row => row.Level).HasColumnName("level");
            entity.Property(row => row.ClosedTime).HasColumnName("closed_time");
            entity.Property(row => row.ClosedByNote).HasColumnName("closed_by_note");
            entity.Property(row => row.AlertField).HasColumnName("alert_field");
            entity.Property(row => row.AlertType).HasColumnName("alert_type");
        });

        ConfigureReadModel<ReportingAlertRuleRow>(modelBuilder, "rvt_alert_rule", entity =>
        {
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.MonitorId).HasColumnName("monitor_id");
            entity.Property(row => row.AlertField).HasColumnName("alert_field");
            entity.Property(row => row.LimitOn).HasColumnName("limit_on");
            entity.Property(row => row.AlertType).HasColumnName("alert_type");
            entity.Property(row => row.AveragingPeriod).HasColumnName("averaging_period");
            entity.Property(row => row.IsActive).HasColumnName("is_active");
            entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
        });

        ConfigureReadModel<DustHourlyAverageRow>(modelBuilder, "my_atm_dust_level", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.AveragingPeriodSeconds).HasColumnName("avrg");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Pm10).HasColumnName("pm_10");
        });

        ConfigureReadModel<DustDailyAverageRow>(modelBuilder, "my_atm_dust_level_1_day_avg", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Pm10).HasColumnName("pm_10");
        });

        ConfigureReadModel<NoiseHourlyAverageRow>(modelBuilder, "noise_level_1_hour_avg", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Laeq).HasColumnName("laeq");
        });

        ConfigureReadModel<NoiseDailyAverageRow>(modelBuilder, "noise_level_1_day_avg", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Laeq).HasColumnName("laeq");
        });

        ConfigureReadModel<NoiseSiteAverageRow>(modelBuilder, "noise_level_site_avg", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Laeq).HasColumnName("laeq");
        });

        ConfigureReadModel<VibrationDailyPeakRow>(modelBuilder, "omnidots_peak_level_1_day_peak", entity =>
        {
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.XVtop).HasColumnName("x_vtop");
            entity.Property(row => row.YVtop).HasColumnName("y_vtop");
            entity.Property(row => row.ZVtop).HasColumnName("z_vtop");
        });

    }

    private static void ConfigureReadModel<TEntity>(
        ModelBuilder modelBuilder,
        string viewName,
        Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity>> configure)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(viewName);
            configure(entity);
        });
    }
}
