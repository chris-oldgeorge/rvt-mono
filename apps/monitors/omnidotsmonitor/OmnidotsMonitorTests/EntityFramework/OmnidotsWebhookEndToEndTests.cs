using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Omnidots.Api;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsMonitorTests.EntityFramework;

[TestClass]
[TestCategory("PostgreSqlIntegration")]
[DoNotParallelize]
public sealed class OmnidotsWebhookEndToEndTests
{
    private const string SerialId = "23423";
    private const string WebhookSecret = "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww";
    private const string ConfigSecret = "cccccccccccccccccccccccccccccccc";
    private const string Email = "ops@example.test";
    private const string Phone = "+15550001111";
    private static readonly Guid SiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MonitorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime EventTime = new(2024, 7, 15, 10, 0, 0, DateTimeKind.Utc);

    private static PostgreSqlIntegrationDatabase? database;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        database = await PostgreSqlIntegrationDatabase.CreateAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/create.postgres.sql"),
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
            timeout.Token);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await database!.ResetAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"));
        await SeedMonitorAndContactsAsync();
    }

    [TestMethod]
    public async Task ProductionWebhook_ConcurrentSignedDuplicateAndReplay_CreateOneDurableDeliverySet()
    {
        await using var application = await StartApplicationAsync();
        using var client = application.GetTestClient();
        var body = ValidBody();
        var signature = Signature(body);

        var responses = await Task.WhenAll(
            PostWebhookAsync(client, body, signature),
            PostWebhookAsync(client, body, signature));

        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK));
        foreach (var response in responses)
        {
            Assert.AreEqual("{\"processed\":true}", await response.Content.ReadAsStringAsync());
            response.Dispose();
        }

        await AssertSingleDurableDeliverySetAsync();

        using var replay = await PostWebhookAsync(client, body, signature);

        Assert.AreEqual(HttpStatusCode.OK, replay.StatusCode);
        Assert.AreEqual("{\"processed\":true}", await replay.Content.ReadAsStringAsync());
        await AssertSingleDurableDeliverySetAsync();
    }

    private static async Task<WebApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = "true",
            ["Infrastructure"] = "local",
            ["MonitorScheduler:Enabled"] = "true",
            ["RVT:EMAIL_ENABLED"] = "false",
            ["RVT:SMS_ENABLED"] = "false",
            [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = "monitoring@example.test",
            [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = "Europe/London",
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00",
            [$"{OmnidotsTraceCollectionOptions.SectionName}:Enabled"] = "false",
            [$"{OmnidotsTraceCollectionOptions.SectionName}:MaxMonitorsPerRun"] = "1",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookUrl"] = "https://alerts.example.test/omnidots",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookSecret"] = WebhookSecret,
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigSecret"] = ConfigSecret,
            [$"{OmnidotsApiSecurityOptions.SectionName}:NotificationDelayMinutes"] = "5",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookConcurrencyLimit"] = "8",
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigureConcurrencyLimit"] = "2"
        });
        builder.Services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Api));
        builder.Services.AddOmnidotsMonitor();
        builder.Services.Replace(ServiceDescriptor.Singleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
            new OmnidotsMonitorContextFactory(
                database!.ConnectionString,
                new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>()))));
        builder.Services.PostConfigure<DurableAlertOptions>(options =>
            options.PortalBaseUrl = "https://portal.example.test/");

        var app = builder.Build();
        app.MapOmnidotsMonitorApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client,
        byte[] body,
        string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
        request.Headers.TryAddWithoutValidation(OmnidotsProtocol.SIGNATURE_HEADER, signature);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await client.SendAsync(request);
    }

    private static byte[] ValidBody() => Encoding.UTF8.GetBytes("""
        {"created_at":1721037600,"data":{"alarms":{"alarm_level_1":30,"alarm_level_2":70,"alarm_level_3":100},"axes":{"x":{"vtop":{"value":12}},"y":{"vtop":{"value":8}},"z":{"vtop":{"value":4}}}},"measuring_point_id":23423}
        """);

    private static string Signature(ReadOnlySpan<byte> body)
    {
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), body);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private static async Task AssertSingleDurableDeliverySetAsync()
    {
        Assert.AreEqual(1, await CountAsync("alert_occurrence"));
        Assert.AreEqual(1, await CountAsync("notification"));
        Assert.AreEqual(3, await CountAsync("alert_delivery_outbox"));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "Email:OPS@EXAMPLE.TEST",
                "MqttAlert:alert",
                "Sms:+15550001111"
            },
            await ReadDeliveryDestinationsAsync());
    }

    private static async Task SeedMonitorAndContactsAsync()
    {
        const string sql = """
            INSERT INTO site (id, site_name, create_date)
            VALUES (@site_id, 'Webhook end-to-end site', @created_at);

            INSERT INTO monitor
                (id, serial_id, customer_id, listed_at_time, model, manufacturer,
                 firmware_version, type_of_monitor)
            VALUES
                (@monitor_id, @serial_id, 42, @listed_at, 'SWARM', 'Omnidots', '1.0', 2);

            INSERT INTO "AspNetUsers"
                ("Id", is_disabled, "Email", email_confirmed, "PhoneNumber",
                 phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES
                (@user_id_text, false, @email, true, @phone, true, false, false, 0);

            INSERT INTO site_user (id, start_date, end_date, user_id, site_id)
            VALUES (@site_user_id, @start_date, NULL, @user_id, @site_id);

            INSERT INTO notification_setting
                (id, email, sms, start_time, end_time, site_user_id)
            VALUES (@setting_id, true, true, NULL, NULL, @site_user_id);
            """;

        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("site_id", SiteId);
        command.Parameters.AddWithValue("created_at", EventTime.AddYears(-1));
        command.Parameters.AddWithValue("monitor_id", MonitorId);
        command.Parameters.AddWithValue("serial_id", SerialId);
        command.Parameters.AddWithValue("listed_at", EventTime.AddYears(-1));
        command.Parameters.AddWithValue("user_id_text", UserId.ToString("D"));
        command.Parameters.AddWithValue("email", $" {Email.ToUpperInvariant()} ");
        command.Parameters.AddWithValue("phone", $" {Phone} ");
        command.Parameters.AddWithValue("site_user_id", Guid.Parse("55555555-5555-5555-5555-555555555555"));
        command.Parameters.AddWithValue("start_date", EventTime.AddYears(-1));
        command.Parameters.AddWithValue("user_id", UserId);
        command.Parameters.AddWithValue("setting_id", Guid.Parse("66666666-6666-6666-6666-666666666666"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(string table)
    {
        var allowedTable = table switch
        {
            "alert_occurrence" => "alert_occurrence",
            "notification" => "notification",
            "alert_delivery_outbox" => "alert_delivery_outbox",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {allowedTable};", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string[]> ReadDeliveryDestinationsAsync()
    {
        var values = new List<string>();
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT kind || ':' || destination FROM alert_delivery_outbox ORDER BY kind, destination;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }
}
