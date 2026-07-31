using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ReportingMonitor.Api;
using ReportingMonitor.Api.Db;
using ReportingMonitor.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.IntegrationTesting;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace ReportingMonitorTests;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class TestReportingDbClient(ReportingDbFixture fixture) : IClassFixture<ReportingDbFixture>
{
    private ReportingDbFixture Fixture { get; } = fixture;

    [Fact]
    public async Task GetDueReportRulesAsync_ExcludesHiddenDeletedAndNotDueRules()
    {
        await Fixture.ResetAsync();
        await Fixture.SeedReportRulesAsync(due: true, hidden: true, deleted: true, notDue: true);

        IReadOnlyList<ReportRule> rules = await Fixture.Client.GetDueReportRulesAsync(
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        ReportRule rule = Assert.Single(rules);
        Assert.Equal(Fixture.DueRuleId, rule.Id);
        Assert.False(rule.IsHiddenSystemRule);
        Assert.Equal(["due@example.test"], rule.RecipientEmails);
    }

    [Fact]
    public async Task LoadSiteReportDataAsync_ClampsMonitorDataToEffectiveOwnershipWindow()
    {
        await Fixture.ResetAsync();
        Guid siteId = await Fixture.SeedSiteWithTransferredMonitorAsync();

        SiteReportData site = await Fixture.Client.LoadSiteReportDataAsync(siteId, Fixture.FromUtc, Fixture.ToUtc, CancellationToken.None);

        MonitorReportData monitor = Assert.Single(site.Monitors);
        Assert.All(monitor.NoiseDailyAverage, point => Assert.InRange(point.MeasuredAt, monitor.EffectiveFrom, monitor.EffectiveTo));
        MeasurementPoint point = Assert.Single(monitor.NoiseDailyAverage);
        Assert.Equal(10m, point.Value);
        Assert.Single(monitor.DustHourlyAverage);
        Assert.Single(monitor.DustDailyAverage);
        Assert.Single(monitor.NoiseHourlyAverage);
        Assert.Single(monitor.NoiseSiteAverage);
        Assert.Single(monitor.VibrationDailyPeak);
        Assert.Equal(5m, monitor.VibrationDailyPeak[0].Value);
        Assert.Equal(1, Assert.Single(monitor.AlertRules).TriggeredCount);
    }

    [Fact]
    public async Task LoadSiteReportDataAsync_MatchesVibrationNotificationsBeforeClearingDisplayPeriod()
    {
        await Fixture.ResetAsync();
        Guid siteId = await Fixture.SeedSiteWithVibrationRuleAsync();

        SiteReportData site = await Fixture.Client.LoadSiteReportDataAsync(
            siteId,
            Fixture.FromUtc,
            Fixture.ToUtc,
            CancellationToken.None);

        AlertRuleData rule = Assert.Single(Assert.Single(site.Monitors).AlertRules);
        Assert.Null(rule.AveragingPeriodSeconds);
        Assert.Equal(1, rule.TriggeredCount);
        Assert.Equal("Vibration reviewed", rule.LatestClosedNote);
    }

    [Fact]
    public void PostgreSqlFixture_DerivesOwnershipFromCanonicalRelationsWithoutMonitorWindows()
    {
        string createScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "testdata/create.postgres.sql"));

        Assert.Contains("create table deployment", createScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create table contract", createScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("monitor_windows", createScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReportRuleAsync_ReturnsNullWhenRuleDoesNotExist()
    {
        await Fixture.ResetAsync();

        ReportRule? rule = await Fixture.Client.GetReportRuleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(rule);
    }

    [Fact]
    public async Task TryAcquireAsync_ReturnsNullWhenAnotherClientOwnsTheSameRulePeriodLock()
    {
        await Fixture.ResetAsync();

        await using RuleGenerationLock? first = await Fixture.Client.TryAcquireAsync(Fixture.RuleId, Fixture.DailyPeriod, CancellationToken.None);
        await using RuleGenerationLock? second = await Fixture.SecondClient.TryAcquireAsync(Fixture.RuleId, Fixture.DailyPeriod, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task SaveGeneratedReportAsync_RollsBackRuleReportAndRecipientRowsWhenARecipientWriteFails()
    {
        await Fixture.ResetAsync();
        GeneratedReportSaveRequest request = Fixture.GeneratedReportRequest(withInvalidRecipient: true);

        await Assert.ThrowsAsync<DbUpdateException>(() => Fixture.Client.SaveGeneratedReportAsync(request, CancellationToken.None));

        Assert.Equal(0, await Fixture.CountAsync("report_rule"));
        Assert.Equal(0, await Fixture.CountAsync("report"));
        Assert.Equal(0, await Fixture.CountAsync("report_sent"));
    }

    // Backfilling a missed period must not regenerate one that already exists.
    // The advisory generation lock only serialises concurrent runs, so an
    // existing report row is the only record that a period is done.
    [Fact]
    public async Task GetGeneratedPeriodsAsync_ReturnsThisRulesPeriodsAtOrAfterTheCutoff()
    {
        await Fixture.ResetAsync();
        GeneratedReport saved = await Fixture.Client.SaveGeneratedReportAsync(
            Fixture.GeneratedReportRequest(withInvalidRecipient: false),
            CancellationToken.None);

        IReadOnlyList<GeneratedReportPeriod> atCutoff = await Fixture.Client.GetGeneratedPeriodsAsync(
            saved.ReportRuleId,
            Fixture.FromUtc.AddDays(-1),
            CancellationToken.None);
        IReadOnlyList<GeneratedReportPeriod> afterCutoff = await Fixture.Client.GetGeneratedPeriodsAsync(
            saved.ReportRuleId,
            Fixture.FromUtc,
            CancellationToken.None);
        IReadOnlyList<GeneratedReportPeriod> otherRule = await Fixture.Client.GetGeneratedPeriodsAsync(
            Guid.NewGuid(),
            Fixture.FromUtc.AddDays(-1),
            CancellationToken.None);

        GeneratedReportPeriod period = Assert.Single(atCutoff);
        Assert.Equal(FrequencyType.OneTime, period.Frequency);
        Assert.Equal(Fixture.FromUtc.AddDays(-1), period.PeriodStartUtc);
        Assert.Empty(afterCutoff);
        Assert.Empty(otherRule);
    }

    [Fact]
    public async Task SaveGeneratedReportAsync_ReusesAndReactivatesDeletedHiddenOneTimeRule()
    {
        await Fixture.ResetAsync();
        Guid deletedRuleId = await Fixture.SeedDeletedHiddenOneTimeRuleAsync();

        GeneratedReport report = await Fixture.Client.SaveGeneratedReportAsync(
            Fixture.GeneratedReportRequest(withInvalidRecipient: false),
            CancellationToken.None);

        Assert.Equal(deletedRuleId, report.ReportRuleId);
        Assert.Equal(1, await Fixture.CountAsync("report_rule"));
        Assert.False(await Fixture.IsReportRuleDeletedAsync(deletedRuleId));
    }

    [Fact]
    public async Task SaveGeneratedReportAsync_ConcurrentOneTimeRequestsReuseTheSameHiddenRule()
    {
        await Fixture.ResetAsync();
        await Fixture.DelayHiddenOneTimeRuleInsertsAsync();

        GeneratedReport[] reports = await Task.WhenAll(
            Fixture.Client.SaveGeneratedReportAsync(Fixture.GeneratedReportRequest(withInvalidRecipient: false), CancellationToken.None),
            Fixture.SecondClient.SaveGeneratedReportAsync(Fixture.GeneratedReportRequest(withInvalidRecipient: false), CancellationToken.None));

        Assert.Equal(reports[0].ReportRuleId, reports[1].ReportRuleId);
        Assert.Equal(1, await Fixture.CountAsync("report_rule"));
        Assert.Equal(2, await Fixture.CountAsync("report"));
    }

    [Fact]
    public async Task SaveGeneratedReportAsync_PersistsDeliveryErrors()
    {
        await Fixture.ResetAsync();
        GeneratedReportSaveRequest request = Fixture.GeneratedReportRequest(withInvalidRecipient: false) with
        {
            Deliveries =
            [
                new ReportDeliverySaveRequest(Fixture.FromUtc, "success@example.test", null),
                new ReportDeliverySaveRequest(Fixture.FromUtc, "failed@example.test", "SendGrid returned 503 ServiceUnavailable")
            ]
        };

        await Fixture.Client.SaveGeneratedReportAsync(request, CancellationToken.None);

        IReadOnlyDictionary<string, string?> errors = await Fixture.GetReportSentErrorsAsync();
        Assert.Null(errors["success@example.test"]);
        Assert.Equal("SendGrid returned 503 ServiceUnavailable", errors["failed@example.test"]);
    }
}

public sealed class ReportingMonitorCompositionTests
{
    private const string SafePostgreSqlOnlyMessage = "PostgreSQL is the only supported database provider";

    [Theory]
    [InlineData(null, null)]
    [InlineData("postgres", "oracle")]
    [InlineData(" POSTGRESQL ", "oracle")]
    [InlineData("NPGSQL", null)]
    [InlineData(null, "timescale")]
    [InlineData(" ", "TimescaleDB")]
    public void Composition_AcceptsOmittedAndPostgreSqlLegacyAliases(
        string? primaryProvider,
        string? fallbackProvider)
    {
        using ServiceProvider provider = CreateServiceProvider(
            primaryProvider,
            fallbackProvider,
            "Host=localhost;Database=reporting_composition_tests;Username=reporting");
        using IServiceScope scope = provider.CreateScope();

        ReportingDbClient client = scope.ServiceProvider.GetRequiredService<ReportingDbClient>();

        Assert.NotNull(client);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Composition_RejectsUnsupportedLegacyProviderBeforeDbClientResolution(
        bool usePrimaryProvider)
    {
        string rejectedValue = string.Concat("sql", "server");
        string primaryProvider = usePrimaryProvider ? rejectedValue : " ";
        string fallbackProvider = usePrimaryProvider ? "postgresql" : rejectedValue;
        const string invalidConnectionString = "not a valid connection string";
        using ServiceProvider provider = CreateServiceProvider(
            primaryProvider,
            fallbackProvider,
            invalidConnectionString);
        using IServiceScope scope = provider.CreateScope();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ReportingDbClient>());

        Assert.Equal(SafePostgreSqlOnlyMessage, exception.Message);
        Assert.DoesNotContain(invalidConnectionString, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rejectedValue, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateServiceProvider(
        string? primaryProvider,
        string? fallbackProvider,
        string connectionString)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["RVT:DATABASE_PROVIDER"] = primaryProvider,
                ["DatabaseProvider"] = fallbackProvider,
                ["RVT:EMAIL_ENABLED"] = "false"
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddReportingMonitor(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}

public sealed class ReportingDbFixture : IAsyncLifetime
{
    private const string CreateScript = "testdata/create.postgres.sql";
    private const string ResetScript = "testdata/reset.postgres.sql";
    private PostgreSqlIntegrationDatabase? database;
    private ReportingMonitorContext? context;
    private ReportingMonitorContext? secondContext;

    public Guid DueRuleId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public Guid RuleId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public DateTimeOffset FromUtc { get; } = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset ToUtc { get; } = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
    public ReportPeriod DailyPeriod => new(FrequencyType.Daily, FromUtc.AddDays(-1), FromUtc);
    public ReportingDbClient Client => new(context ?? throw new InvalidOperationException("Fixture has not been initialized."));
    public ReportingDbClient SecondClient => new(secondContext ?? throw new InvalidOperationException("Fixture has not been initialized."));

    /// <summary>
    /// A rule three days behind, so a single run backfills the maximum four periods
    /// and each one takes long enough for a competing run to overtake it.
    /// </summary>
    public async Task<Guid> SeedBackfillDueDailyRuleAsync(DateTimeOffset lastGeneratedUtc)
    {
        Guid ruleId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        const string sql = """
            insert into site_search (id, site_name, create_date, postcode)
            values (@site_id, 'Backfill race site', @created_at, 'AB1 2CD');
            insert into "AspNetUsers" ("Id", "Email") values (@user_id::text, 'backfill@example.test');
            insert into report_rule (id, site_id, user_id, frequency, last_generated, deleted, is_hidden_system_rule)
            values (@rule_id, @site_id, @user_id, 1, @last_generated, false, false);
            insert into report_user (id, report_rule_id, user_id) values (@recipient_id, @rule_id, @user_id);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("site_id", Guid.Parse("40000000-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("user_id", Guid.Parse("40000000-0000-0000-0000-000000000003"));
            command.Parameters.AddWithValue("recipient_id", Guid.Parse("40000000-0000-0000-0000-000000000004"));
            command.Parameters.AddWithValue("rule_id", ruleId);
            command.Parameters.AddWithValue("created_at", lastGeneratedUtc.AddYears(-1));
            command.Parameters.AddWithValue("last_generated", lastGeneratedUtc);
        });

        return ruleId;
    }

    public async Task<IReadOnlyList<(int Frequency, DateTimeOffset PeriodStartUtc)>> GetReportPeriodsAsync()
    {
        ReportingMonitorContext reportingContext = context ?? throw new InvalidOperationException("Fixture has not been initialized.");
        List<ReportEntity> reports = await reportingContext.Reports
            .AsNoTracking()
            .OrderBy(row => row.ReportFrom)
            .ToListAsync();

        return [.. reports.Select(row => (row.Frequency, row.ReportFrom))];
    }

    public async Task InitializeAsync()
    {
        database = await PostgreSqlIntegrationDatabase.CreateAsync(ReadTestData(CreateScript), ReadTestData(ResetScript));
        MonitorDbOptions monitorOptions = new(new Dictionary<string, string>());
        DbContextOptions<ReportingMonitorContext> options = MonitorDbContextOptionsFactory.CreateOptions<ReportingMonitorContext>(database.ConnectionString);
        context = new ReportingMonitorContext(options, monitorOptions);
        secondContext = new ReportingMonitorContext(options, monitorOptions);
    }

    public async Task DisposeAsync()
    {
        if (secondContext is not null)
        {
            await secondContext.DisposeAsync();
        }

        if (context is not null)
        {
            await context.DisposeAsync();
        }

        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    public Task ResetAsync() => (database ?? throw new InvalidOperationException("Fixture has not been initialized."))
        .ResetAsync(ReadTestData(ResetScript));

    public GeneratedReportSaveRequest GeneratedReportRequest(bool withInvalidRecipient) => new(
        Guid.Parse("30000000-0000-0000-0000-000000000002"),
        null,
        new OneTimeReportRuleSaveRequest(Guid.Parse("30000000-0000-0000-0000-000000000003"), "One-time reporting test"),
        FrequencyType.OneTime,
        FromUtc,
        FromUtc.AddDays(-1),
        FromUtc,
        new Uri("https://reports.example.test/generated.pdf"),
        [new ReportDeliverySaveRequest(FromUtc, withInvalidRecipient ? null! : "recipient@example.test", null)],
        UpdateLastGenerated: false);

    public async Task<IReadOnlyDictionary<string, string?>> GetReportSentErrorsAsync()
    {
        ReportingMonitorContext reportingContext = context ?? throw new InvalidOperationException("Fixture has not been initialized.");
        return await reportingContext.ReportSends
            .AsNoTracking()
            .ToDictionaryAsync(row => row.Address, row => row.ErrorMessage);
    }

    public async Task<int> CountAsync(string tableName)
    {
        ReportingMonitorContext reportingContext = context ?? throw new InvalidOperationException("Fixture has not been initialized.");
        return tableName switch
        {
            "report_rule" => await reportingContext.ReportRules.CountAsync(),
            "report" => await reportingContext.Reports.CountAsync(),
            "report_sent" => await reportingContext.ReportSends.CountAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unknown reporting table.")
        };
    }

    public async Task<Guid> SeedDeletedHiddenOneTimeRuleAsync()
    {
        Guid ruleId = Guid.Parse("30000000-0000-0000-0000-000000000004");
        const string sql = """
            insert into report_rule (id, site_id, user_id, frequency, report_name, deleted, is_hidden_system_rule)
            values (@rule_id, @site_id, @user_id, 5, 'Deleted one-time report', true, true);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("rule_id", ruleId);
            command.Parameters.AddWithValue("site_id", Guid.Parse("30000000-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("user_id", Guid.Parse("30000000-0000-0000-0000-000000000003"));
        });

        return ruleId;
    }

    public async Task<bool> IsReportRuleDeletedAsync(Guid ruleId)
    {
        ReportingMonitorContext reportingContext = context ?? throw new InvalidOperationException("Fixture has not been initialized.");
        return await reportingContext.ReportRules
            .Where(row => row.Id == ruleId)
            .Select(row => row.Deleted)
            .SingleAsync();
    }

    public Task DelayHiddenOneTimeRuleInsertsAsync() => ExecuteAsync("""
        create function delay_hidden_one_time_rule_insert() returns trigger
        language plpgsql
        as $$
        begin
            perform pg_sleep(0.5);
            return new;
        end;
        $$;

        create trigger delay_hidden_one_time_rule_insert
        before insert on report_rule
        for each row
        when (new.is_hidden_system_rule and new.frequency = 5)
        execute function delay_hidden_one_time_rule_insert();
        """, static _ => { });

    public async Task SeedReportRulesAsync(bool due, bool hidden, bool deleted, bool notDue)
    {
        const string sql = """
            insert into site_search (id, site_name, create_date) values (@site_id, 'Reporting site', @created_at);
            insert into "AspNetUsers" ("Id", "Email") values (@user_id::text, 'due@example.test');
            insert into report_rule (id, site_id, user_id, frequency, last_generated, deleted, is_hidden_system_rule)
            values
                (@due_rule_id, @site_id, @user_id, 1, @due_last_generated, false, false),
                (@hidden_rule_id, @site_id, @user_id, 1, @due_last_generated, false, true),
                (@deleted_rule_id, @site_id, @user_id, 1, @due_last_generated, true, false),
                (@not_due_rule_id, @site_id, @user_id, 1, @not_due_last_generated, false, false),
                (@one_time_rule_id, @site_id, @user_id, 5, null, false, false);
            insert into report_user (id, report_rule_id, user_id) values (@recipient_id, @due_rule_id, @user_id);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("site_id", Guid.Parse("10000000-0000-0000-0000-000000000010"));
            command.Parameters.AddWithValue("user_id", Guid.Parse("10000000-0000-0000-0000-000000000020"));
            command.Parameters.AddWithValue("due_rule_id", DueRuleId);
            command.Parameters.AddWithValue("hidden_rule_id", Guid.Parse("10000000-0000-0000-0000-000000000002"));
            command.Parameters.AddWithValue("deleted_rule_id", Guid.Parse("10000000-0000-0000-0000-000000000003"));
            command.Parameters.AddWithValue("not_due_rule_id", Guid.Parse("10000000-0000-0000-0000-000000000004"));
            command.Parameters.AddWithValue("one_time_rule_id", Guid.Parse("10000000-0000-0000-0000-000000000005"));
            command.Parameters.AddWithValue("recipient_id", Guid.Parse("10000000-0000-0000-0000-000000000030"));
            command.Parameters.AddWithValue("created_at", FromUtc);
            command.Parameters.AddWithValue("due_last_generated", FromUtc.AddDays(-1));
            command.Parameters.AddWithValue("not_due_last_generated", ToUtc);
        });
    }

    public async Task<Guid> SeedSiteWithTransferredMonitorAsync()
    {
        Guid siteId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid monitorId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        const string sql = """
            insert into site_search (id, site_name, create_date) values (@site_id, 'Transferred monitor site', @from_utc);
            insert into contract (id, contract_number, on_hire_date, off_hire_date, company_id, site_id)
            values (@contract_id, 'reporting-transfer', @effective_from, @effective_to, @company_id, @site_id);
            insert into monitor_report (id, site_id, active, deployment_id, serial_id, type_of_monitor, off_line, alerts, cautions)
            values (@monitor_id, @site_id, true, @deployment_id, 'transferred-noise', 1, false, false, false);
            insert into deployment (id, start_date, contract_id, monitor_id)
            values (@deployment_id, @effective_from, @contract_id, @monitor_id);
            insert into noise_level_1_day_avg (serial_id, sample_time, laeq) values
                ('transferred-noise', @before_ownership, 8.0),
                ('transferred-noise', @inside_ownership, 10.0),
                ('transferred-noise', @after_ownership, 16.0);
            insert into my_atm_dust_level (serial_id, avrg, sample_time, pm_10) values ('transferred-noise', 3600, @inside_ownership, 11.0);
            insert into my_atm_dust_level_1_day_avg (serial_id, sample_time, pm_10) values ('transferred-noise', @inside_ownership, 12.0);
            insert into noise_level_1_hour_avg (serial_id, sample_time, laeq) values ('transferred-noise', @inside_ownership, 13.0);
            insert into noise_level_site_avg (serial_id, sample_time, laeq) values ('transferred-noise', @inside_ownership, 14.0);
            insert into omnidots_peak_level_1_day_peak (serial_id, sample_time, x_vtop, y_vtop, z_vtop)
            values ('transferred-noise', @inside_ownership, 3.0, 4.0, 5.0);
            insert into notification (id, monitor_id, notification_time, limit_on, averaging_period, level, alert_field, alert_type)
            values (@notification_id, @monitor_id, @inside_ownership, 9.0, 60, 10.0, 'NoiseLevel', 0);
            insert into rvt_alert_rule (id, monitor_id, alert_field, limit_on, alert_type, averaging_period)
            values (@alert_rule_id, @monitor_id, 'NoiseLevel', 9.0, 0, 60);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("site_id", siteId);
            command.Parameters.AddWithValue("monitor_id", monitorId);
            command.Parameters.AddWithValue("deployment_id", Guid.Parse("20000000-0000-0000-0000-000000000005"));
            command.Parameters.AddWithValue("contract_id", Guid.Parse("20000000-0000-0000-0000-000000000006"));
            command.Parameters.AddWithValue("company_id", Guid.Parse("20000000-0000-0000-0000-000000000007"));
            command.Parameters.AddWithValue("from_utc", FromUtc);
            command.Parameters.AddWithValue("effective_from", FromUtc.AddHours(9));
            command.Parameters.AddWithValue("effective_to", FromUtc.AddHours(15));
            command.Parameters.AddWithValue("before_ownership", FromUtc.AddHours(8));
            command.Parameters.AddWithValue("inside_ownership", FromUtc.AddHours(10));
            command.Parameters.AddWithValue("after_ownership", FromUtc.AddHours(16));
            command.Parameters.AddWithValue("notification_id", Guid.Parse("20000000-0000-0000-0000-000000000003"));
            command.Parameters.AddWithValue("alert_rule_id", Guid.Parse("20000000-0000-0000-0000-000000000004"));
        });

        return siteId;
    }

    public async Task<Guid> SeedSiteWithVibrationRuleAsync()
    {
        Guid siteId = Guid.Parse("21000000-0000-0000-0000-000000000001");
        Guid monitorId = Guid.Parse("21000000-0000-0000-0000-000000000002");
        const string sql = """
            insert into site_search (id, site_name, create_date)
            values (@site_id, 'Vibration alert site', @from_utc);
            insert into monitor_report (
                id, site_id, active, deployment_id, serial_id, type_of_monitor,
                off_line, alerts, cautions)
            values (@monitor_id, @site_id, true, null, 'vibration-1', 2, false, true, false);
            insert into notification (
                id, monitor_id, notification_time, limit_on, averaging_period,
                level, closed_time, closed_by_note, alert_field, alert_type)
            values (
                @notification_id, @monitor_id, @notification_time, 8.0, 60,
                9.0, @closed_time, 'Vibration reviewed', 'Peak', 0);
            insert into rvt_alert_rule (
                id, monitor_id, alert_field, limit_on, alert_type, averaging_period)
            values (@alert_rule_id, @monitor_id, 'Peak', 8.0, 0, 60);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("site_id", siteId);
            command.Parameters.AddWithValue("monitor_id", monitorId);
            command.Parameters.AddWithValue("from_utc", FromUtc);
            command.Parameters.AddWithValue("notification_id", Guid.Parse("21000000-0000-0000-0000-000000000003"));
            command.Parameters.AddWithValue("alert_rule_id", Guid.Parse("21000000-0000-0000-0000-000000000004"));
            command.Parameters.AddWithValue("notification_time", FromUtc.AddHours(10));
            command.Parameters.AddWithValue("closed_time", FromUtc.AddHours(11));
        });

        return siteId;
    }

    private async Task ExecuteAsync(string sql, Action<NpgsqlCommand> configure)
    {
        await using NpgsqlConnection connection = (database ?? throw new InvalidOperationException("Fixture has not been initialized.")).OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        configure(command);
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadTestData(string relativePath) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relativePath));
}
