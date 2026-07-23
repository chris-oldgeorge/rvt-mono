// File summary: Defines Entity Framework Core context configuration for RVT domain and search data.
// Major updates:
// - 2026-07-23 Pinned every non-daily SampleTime to timestamp-without-zone and daily aggregates to date.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-06-09 pending Applied canonical EF mappings to search/report/measurement models after DBR cutover.
// - 2026-06-25 pending Added monitor measurement removal-impact view mapping for consolidated count lookups.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.EntityModels.Models;
using System;
using System.Collections.Generic;

namespace RVT.DataAccess.Context;

public partial class RVTSearchContext : DbContext
{
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public RVTSearchContext(DbContextOptions<RVTSearchContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminDashboardDatum> AdminDashboardData { get; set; }

    public virtual DbSet<CompanySearch> CompanySearches { get; set; }

    public virtual DbSet<ContractSearch> ContractSearches { get; set; }

    public virtual DbSet<CustomerDashboardMonitorDatum> CustomerDashboardMonitorData { get; set; }

    public virtual DbSet<CustomerDashboardNotificationDatum> CustomerDashboardNotificationData { get; set; }

    public virtual DbSet<MonitorCurrentSearch> MonitorCurrentSearches { get; set; }

    public virtual DbSet<MonitorSearch> MonitorSearches { get; set; }

    public virtual DbSet<MonitorUserSearch> MonitorUserSearches { get; set; }

    public virtual DbSet<MonitorMeasurementRemovalImpact> MonitorMeasurementRemovalImpacts { get; set; }

    public virtual DbSet<MyAtmDustLevel> MyAtmDustLevels { get; set; }

    public virtual DbSet<MyAtmDustLevel8hourAvg> MyAtmDustLevel8hourAvgs { get; set; }

    public virtual DbSet<NoiseLevel15minAvg> NoiseLevel15minAvgs { get; set; }

    public virtual DbSet<NoiseLevel1dayAvg> NoiseLevel1dayAvgs { get; set; }

    public virtual DbSet<NoiseLevel1hourAvg> NoiseLevel1hourAvgs { get; set; }

    public virtual DbSet<NoiseLevelSiteAvg> NoiseLevelSiteAvgs { get; set; }

    public virtual DbSet<NotificationSearch> NotificationSearches { get; set; }

    public virtual DbSet<NotificationUserSearch> NotificationUserSearches { get; set; }

    public virtual DbSet<OmnidotsMonitorStatus> OmnidotsMonitorStatuses { get; set; }

    public virtual DbSet<OmnidotsPeakLevel> OmnidotsPeakLevels { get; set; }

    public virtual DbSet<OmnidotsPeakLevel15min> OmnidotsPeakLevel15mins { get; set; }

    public virtual DbSet<OmnidotsPeakLevel1dayPeak> OmnidotsPeakLevel1dayPeaks { get; set; }

    public virtual DbSet<OmnidotsPeakLevel1min> OmnidotsPeakLevel1mins { get; set; }

    public virtual DbSet<OmnidotsPeakLevel20min> OmnidotsPeakLevel20mins { get; set; }

    public virtual DbSet<OmnidotsPeakLevel5min> OmnidotsPeakLevel5mins { get; set; }

    public virtual DbSet<OmnidotsSensor> OmnidotsSensors { get; set; }

    public virtual DbSet<OmnidotsTrace> OmnidotsTraces { get; set; }

    public virtual DbSet<OmnidotsTracesIndex> OmnidotsTracesIndices { get; set; }

    public virtual DbSet<ReportRule> ReportRules { get; set; }

    public virtual DbSet<ReportRuleSearch> ReportRuleSearches { get; set; }

    public virtual DbSet<ReportRuleUserSearch> ReportRuleUserSearches { get; set; }

    public virtual DbSet<ReportSearch> ReportSearches { get; set; }

    public virtual DbSet<ReportUser> ReportUsers { get; set; }

    public virtual DbSet<ReportUserSearch> ReportUserSearches { get; set; }

    public virtual DbSet<SiteSearch> SiteSearches { get; set; }

    public virtual DbSet<SiteUserSearch> SiteUserSearches { get; set; }

    public virtual DbSet<SvantekMonitorStatus> SvantekMonitorStatuses { get; set; }

    public virtual DbSet<UserSearch> UserSearches { get; set; }

    public virtual DbSet<UsersForReportSearch> UsersForReportSearches { get; set; }

    public virtual DbSet<UsersForSiteSearch> UsersForSiteSearches { get; set; }

    // Function summary: Handles the on configuring workflow for this module.
    // The parameterless constructor and its OnConfiguring fallback are gone; see RVTDbContext for why.
    // Function summary: Handles the on model creating workflow for this module.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeColumnType = IsPostgres() ? "timestamp without time zone" : "datetime";
        var guidDefaultSql = IsPostgres() ? "gen_random_uuid()" : "(newid())";

        modelBuilder.Entity<AdminDashboardDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("AdminDashboardData");

            entity.Property(e => e.MonitorState)
                .HasMaxLength(7)
                .IsUnicode(false);
            entity.Property(e => e.Nr).HasColumnName("nr");
        });

        modelBuilder.Entity<CompanySearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("CompanySearch");

            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Contracts)
                .HasMaxLength(4000)
                .HasColumnName("contracts");
            entity.Property(e => e.NrUsers).HasColumnName("nrUsers");
            entity.Property(e => e.Sites).HasColumnName("sites");
        });

        modelBuilder.Entity<ContractSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ContractSearch");

            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.SiteAddress).HasColumnName("siteAddress");
        });

        modelBuilder.Entity<CustomerDashboardMonitorDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("CustomerDashboardMonitorData");

            entity.Property(e => e.MonitorState)
                .HasMaxLength(7)
                .IsUnicode(false);
            entity.Property(e => e.Nr).HasColumnName("nr");
        });

        modelBuilder.Entity<CustomerDashboardNotificationDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("CustomerDashboardNotificationData");

            entity.Property(e => e.AlertState)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Nr).HasColumnName("nr");
        });

        modelBuilder.Entity<MonitorCurrentSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("MonitorCurrentSearch");

            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.FleetNr).HasMaxLength(32);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<MonitorSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("MonitorSearch");

            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.FleetNr).HasMaxLength(32);
            entity.Property(e => e.MonitorName).HasMaxLength(48);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<MonitorUserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("MonitorUserSearch");

            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.FleetNr).HasMaxLength(32);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<MonitorMeasurementRemovalImpact>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("monitor_measurement_removal_impact");

            entity.Property(e => e.SerialId).HasMaxLength(255);
            entity.Property(e => e.MeasurementTableCount).HasColumnName("measurement_table_count");
            entity.Property(e => e.MeasurementRowCount).HasColumnName("measurement_row_count");
        });

        modelBuilder.Entity<MyAtmDustLevel>(entity =>
        {
            entity.HasNoKey();

            // Pinned explicitly: without it the physical name is inferred from the DbSet name and then rewritten
            // by the canonical naming rules, so a DbSet rename would silently remap the entity.
            entity.ToTable("my_atm_dust_level");

            entity.Property(e => e.Pm25).HasColumnName("Pm2_5");
            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.WeatherP).HasColumnName("Weather_p");
            entity.Property(e => e.WeatherRh).HasColumnName("Weather_rh");
            entity.Property(e => e.WeatherT).HasColumnName("Weather_t");
        });

        modelBuilder.Entity<MyAtmDustLevel8hourAvg>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("MyAtmDustLevel8hourAvg");

            entity.Property(e => e.Pm25).HasColumnName("Pm2_5");
            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<NoiseLevel15minAvg>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NoiseLevel15minAvg");

            entity.Property(e => e.La10).HasColumnName("LA10");
            entity.Property(e => e.La90).HasColumnName("LA90");
            entity.Property(e => e.Laeq).HasColumnName("LAeq");
            entity.Property(e => e.Lamax).HasColumnName("LAmax");
            entity.Property(e => e.Lc10).HasColumnName("LC10");
            entity.Property(e => e.Lc90).HasColumnName("LC90");
            entity.Property(e => e.Lceq).HasColumnName("LCeq");
            entity.Property(e => e.Lcmax).HasColumnName("LCmax");
            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(255);
        });

        modelBuilder.Entity<NoiseLevel1dayAvg>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NoiseLevel1dayAvg");

            entity.Property(e => e.La10).HasColumnName("LA10");
            entity.Property(e => e.La90).HasColumnName("LA90");
            entity.Property(e => e.Laeq).HasColumnName("LAeq");
            entity.Property(e => e.Lamax).HasColumnName("LAmax");
            entity.Property(e => e.Lc10).HasColumnName("LC10");
            entity.Property(e => e.Lc90).HasColumnName("LC90");
            entity.Property(e => e.Lceq).HasColumnName("LCeq");
            entity.Property(e => e.Lcmax).HasColumnName("LCmax");
            entity.Property(e => e.SampleTime).HasColumnType("date");
            entity.Property(e => e.SerialId).HasMaxLength(255);
        });

        modelBuilder.Entity<NoiseLevel1hourAvg>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NoiseLevel1hourAvg");

            entity.Property(e => e.La10).HasColumnName("LA10");
            entity.Property(e => e.La90).HasColumnName("LA90");
            entity.Property(e => e.Laeq).HasColumnName("LAeq");
            entity.Property(e => e.Lamax).HasColumnName("LAmax");
            entity.Property(e => e.Lc10).HasColumnName("LC10");
            entity.Property(e => e.Lc90).HasColumnName("LC90");
            entity.Property(e => e.Lceq).HasColumnName("LCeq");
            entity.Property(e => e.Lcmax).HasColumnName("LCmax");
            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(255);
        });

        modelBuilder.Entity<NoiseLevelSiteAvg>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NoiseLevelSiteAvg");

            entity.Property(e => e.La10).HasColumnName("LA10");
            entity.Property(e => e.La90).HasColumnName("LA90");
            entity.Property(e => e.Laeq).HasColumnName("LAeq");
            entity.Property(e => e.Lamax).HasColumnName("LAmax");
            entity.Property(e => e.Lc10).HasColumnName("LC10");
            entity.Property(e => e.Lc90).HasColumnName("LC90");
            entity.Property(e => e.Lceq).HasColumnName("LCeq");
            entity.Property(e => e.Lcmax).HasColumnName("LCmax");
            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<NotificationSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NotificationSearch");

            entity.Property(e => e.AlertField).HasMaxLength(32);
            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.FleetNr).HasMaxLength(32);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<NotificationUserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NotificationUserSearch");

            entity.Property(e => e.AlertField).HasMaxLength(32);
            entity.Property(e => e.ContractNumber).HasMaxLength(20);
            entity.Property(e => e.FleetNr).HasMaxLength(32);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<OmnidotsMonitorStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Omnidots__3214EC0781E80E82");

            entity.ToTable("OmnidotsMonitorStatus");

            entity.Property(e => e.Id).HasDefaultValueSql(guidDefaultSql);
            entity.Property(e => e.BuildingLevel).HasMaxLength(32);
            entity.Property(e => e.GuideLine).HasMaxLength(32);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.VdvX).HasMaxLength(32);
            entity.Property(e => e.VdvY).HasMaxLength(32);
            entity.Property(e => e.VdvZ).HasMaxLength(32);
        });

        modelBuilder.Entity<OmnidotsPeakLevel>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("omnidots_peak_level");

            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xfdom).HasColumnName("XFdom");
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.XvtopOverflow).HasColumnName("XVtopOverflow");
            entity.Property(e => e.Yfdom).HasColumnName("YFdom");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.YvtopOverflow).HasColumnName("YVtopOverflow");
            entity.Property(e => e.Zfdom).HasColumnName("ZFdom");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
            entity.Property(e => e.ZvtopOverflow).HasColumnName("ZVtopOverflow");
        });

        modelBuilder.Entity<OmnidotsPeakLevel15min>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("OmnidotsPeakLevel15min");

            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
        });

        modelBuilder.Entity<OmnidotsPeakLevel1dayPeak>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("OmnidotsPeakLevel1dayPeak");

            entity.Property(e => e.SampleTime).HasColumnType("date");
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
        });

        modelBuilder.Entity<OmnidotsPeakLevel1min>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("OmnidotsPeakLevel1min");

            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
        });

        modelBuilder.Entity<OmnidotsPeakLevel20min>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("OmnidotsPeakLevel20min");

            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
        });

        modelBuilder.Entity<OmnidotsPeakLevel5min>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("OmnidotsPeakLevel5min");

            entity.Property(e => e.SampleTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Xvtop).HasColumnName("XVtop");
            entity.Property(e => e.Yvtop).HasColumnName("YVtop");
            entity.Property(e => e.Zvtop).HasColumnName("ZVtop");
        });

        modelBuilder.Entity<OmnidotsSensor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Omnidots__3214EC07973D8C48");

            entity.Property(e => e.Id).HasDefaultValueSql(guidDefaultSql);
            entity.Property(e => e.ConnectedUsing).HasMaxLength(32);
            entity.Property(e => e.Lastseen).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.Name).HasMaxLength(48);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<OmnidotsTrace>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("omnidots_trace");

            entity.HasOne(d => d.Trace).WithMany()
                .HasForeignKey(d => d.TraceId)
                .HasConstraintName("FK__OmnidotsT__Trace__2DE6D218");
        });

        modelBuilder.Entity<OmnidotsTracesIndex>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Omnidots__3214EC0741C1EBFD");

            entity.ToTable("OmnidotsTracesIndex");

            entity.Property(e => e.Id).HasDefaultValueSql(guidDefaultSql);
            entity.Property(e => e.StartTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.EndTime).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.SerialId).HasMaxLength(32);
        });

        modelBuilder.Entity<ReportRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ReportConfig");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.LastGenerated).HasColumnType(dateTimeColumnType);
            entity.Property(e => e.ReportName).HasMaxLength(128);
        });

        modelBuilder.Entity<ReportRuleSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ReportRuleSearch");

            entity.Property(e => e.ReportName).HasMaxLength(128);
        });

        modelBuilder.Entity<ReportRuleUserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ReportRuleUserSearch");

            entity.Property(e => e.ReportName).HasMaxLength(128);
        });

        modelBuilder.Entity<ReportSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ReportSearch");

            entity.Property(e => e.Contracts)
                .HasMaxLength(4000)
                .HasColumnName("contracts");
            entity.Property(e => e.ReportLink).HasMaxLength(256);
            entity.Property(e => e.ReportName).HasMaxLength(128);
        });

        modelBuilder.Entity<ReportUser>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ReportUserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ReportUserSearch");

            entity.Property(e => e.ReportLink).HasMaxLength(256);
            entity.Property(e => e.ReportName).HasMaxLength(128);
        });

        modelBuilder.Entity<SiteSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("SiteSearch");

            entity.Property(e => e.AddressLine1).HasMaxLength(100);
            entity.Property(e => e.AddressLine2).HasMaxLength(100);
            entity.Property(e => e.City).HasMaxLength(30);
            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Contracts)
                .HasMaxLength(4000)
                .HasColumnName("contracts");
            entity.Property(e => e.County).HasMaxLength(30);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.SiteAddress)
                .HasMaxLength(274)
                .HasColumnName("siteAddress");
            entity.Property(e => e.SiteContact).HasMaxLength(256);
        });

        modelBuilder.Entity<SiteUserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("SiteUserSearch");

            entity.Property(e => e.AddressLine1).HasMaxLength(100);
            entity.Property(e => e.AddressLine2).HasMaxLength(100);
            entity.Property(e => e.City).HasMaxLength(30);
            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Contracts)
                .HasMaxLength(4000)
                .HasColumnName("contracts");
            entity.Property(e => e.County).HasMaxLength(30);
            entity.Property(e => e.Postcode).HasMaxLength(10);
            entity.Property(e => e.SiteAddress)
                .HasMaxLength(274)
                .HasColumnName("siteAddress");
            entity.Property(e => e.SiteContact).HasMaxLength(256);
        });

        modelBuilder.Entity<SvantekMonitorStatus>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("SvantekMonitorStatus");

            entity.Property(e => e.Active)
                .HasMaxLength(4)
                .IsUnicode(false)
                .HasColumnName("active");
            entity.Property(e => e.Batterycharge).HasColumnName("batterycharge");
            entity.Property(e => e.Batterytimetoempty).HasColumnName("batterytimetoempty");
            entity.Property(e => e.Gsmsignalquality).HasColumnName("gsmsignalquality");
            entity.Property(e => e.Isbatterycharging)
                .HasMaxLength(5)
                .IsUnicode(false)
                .HasColumnName("isbatterycharging");
            entity.Property(e => e.Isonline)
                .HasMaxLength(5)
                .IsUnicode(false)
                .HasColumnName("isonline");
            entity.Property(e => e.Lastlogin)
                .HasMaxLength(19)
                .IsUnicode(false)
                .HasColumnName("lastlogin");
            entity.Property(e => e.Lastlogout)
                .HasMaxLength(19)
                .IsUnicode(false)
                .HasColumnName("lastlogout");
            entity.Property(e => e.Laststatustimestamp)
                .HasMaxLength(19)
                .IsUnicode(false)
                .HasColumnName("laststatustimestamp");
            entity.Property(e => e.Measurementstate)
                .HasMaxLength(7)
                .IsUnicode(false)
                .HasColumnName("measurementstate");
            entity.Property(e => e.Meterfirmware)
                .HasColumnType("numeric(4, 2)")
                .HasColumnName("meterfirmware");
            entity.Property(e => e.PointId).HasColumnName("point_id");
            entity.Property(e => e.Powersource)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("powersource");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SerialId).HasMaxLength(32);
            entity.Property(e => e.Type)
                .HasMaxLength(7)
                .IsUnicode(false)
                .HasColumnName("type");
        });

        modelBuilder.Entity<UserSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("UserSearch");

            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<UsersForReportSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("UsersForReportSearch");

            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<UsersForSiteSearch>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("UsersForSiteSearch");

            entity.Property(e => e.CompanyName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        OnModelCreatingPartial(modelBuilder);
        modelBuilder.ApplyRvtCanonicalDatabaseNames();
    }

    // Function summary: Evaluates postgres for the current decision point.
    private bool IsPostgres()
    {
        return Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
