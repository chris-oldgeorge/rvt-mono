using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RVT.DataAccess.Context;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Sites;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class SiteConcurrencyPostgresTests
{
    [RequiresPostgresFact]
    public async Task AtomicSiteWrites_ConcurrentRequestsKeepOneValidRowPerOwner()
    {
        await using PostgresSiteFixture fixture = await PostgresSiteFixture.CreateAsync();

        SiteArchiveClaimResult[] archiveClaims = await Task.WhenAll(
            fixture.ClaimArchiveAsync("https://archive.example/first.zip"),
            fixture.ClaimArchiveAsync("https://archive.example/second.zip"));
        Assert.Single(archiveClaims, claim => claim.Claimed);
        Assert.All(
            archiveClaims,
            claim => Assert.Contains(
                claim.DurableArchiveUrl,
                new[]
                {
                    "https://archive.example/first.zip",
                    "https://archive.example/second.zip"
                }));

        SiteNotificationSettingMutation firstRequest = new SiteNotificationSettingMutation(
            true,
            false,
            "08:00",
            "12:00");
        SiteNotificationSettingMutation secondRequest = new SiteNotificationSettingMutation(
            false,
            true,
            "13:00",
            "17:00");
        await Task.WhenAll(
            fixture.UpsertNotificationAsync(
                firstRequest,
                new TimeSpan(8, 0, 0),
                new TimeSpan(12, 0, 0)),
            fixture.UpsertNotificationAsync(
                secondRequest,
                new TimeSpan(13, 0, 0),
                new TimeSpan(17, 0, 0)));

        SiteWriteState state = await fixture.ReadStateAsync();
        Assert.Multiple(
            () => Assert.Equal(1, state.ArchiveCount),
            () => Assert.True(state.SiteArchived),
            () => Assert.Equal(1, state.NotificationCount),
            () => Assert.Contains(
                state.Notification,
                new[]
                {
                    new NotificationValue(
                        true,
                        false,
                        new TimeSpan(8, 0, 0),
                        new TimeSpan(12, 0, 0)),
                    new NotificationValue(
                        false,
                        true,
                        new TimeSpan(13, 0, 0),
                        new TimeSpan(17, 0, 0))
                }));

        await using RVTDbContext readContext = await fixture.CreateDomainContextAsync();
        SiteNotificationSettingsData? settings = await new EfSiteReadAdapter(readContext)
            .GetNotificationSettingsAsync(
                fixture.SiteId,
                CancellationToken.None);
        Assert.NotNull(settings);
        Assert.Single(settings.Assignments);
    }

    private sealed record NotificationValue(
        bool Email,
        bool Sms,
        TimeSpan? StartTime,
        TimeSpan? EndTime);

    private sealed record SiteWriteState(
        int ArchiveCount,
        bool SiteArchived,
        int NotificationCount,
        NotificationValue Notification);

    private sealed class PostgresSiteFixture : IAsyncDisposable
    {
        private readonly string baseConnectionString;
        private readonly string schema;

        private PostgresSiteFixture(
            string baseConnectionString,
            string schema,
            Guid siteId,
            Guid siteUserId)
        {
            this.baseConnectionString = baseConnectionString;
            this.schema = schema;
            SiteId = siteId;
            SiteUserId = siteUserId;
        }

        public Guid SiteId { get; }
        public Guid SiteUserId { get; }

        public static async Task<PostgresSiteFixture> CreateAsync()
        {
            string baseConnectionString = Environment.GetEnvironmentVariable(
                RequiresPostgresFactAttribute.ConnectionVariable)!;
            string schema = $"site_concurrency_{Guid.NewGuid():N}";
            Guid siteId = Guid.NewGuid();
            Guid siteUserId = Guid.NewGuid();
            PostgresSiteFixture fixture = new PostgresSiteFixture(
                baseConnectionString,
                schema,
                siteId,
                siteUserId);

            await using NpgsqlConnection connection = new NpgsqlConnection(
                baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SCHEMA "{schema}";

                CREATE TABLE "{schema}".site
                (
                    id uuid PRIMARY KEY,
                    site_name text NOT NULL,
                    create_date timestamp with time zone NOT NULL,
                    address_line_1 character varying(100),
                    address_line_2 character varying(100),
                    postcode character varying(10),
                    city character varying(30),
                    county character varying(30),
                    start_time interval,
                    end_time interval,
                    sat_start_time interval,
                    sat_end_time interval,
                    sun_start_time interval,
                    sun_end_time interval,
                    archived boolean NOT NULL
                );

                CREATE TABLE "{schema}".site_user
                (
                    id uuid PRIMARY KEY,
                    start_date timestamp with time zone NOT NULL,
                    end_date timestamp with time zone,
                    user_id uuid NOT NULL,
                    site_id uuid NOT NULL REFERENCES "{schema}".site(id),
                    site_contact boolean NOT NULL
                );

                CREATE TABLE "{schema}".site_archived
                (
                    id uuid PRIMARY KEY,
                    created_by text NOT NULL,
                    create_date timestamp with time zone NOT NULL,
                    picture_link character varying(250),
                    site_id uuid NOT NULL REFERENCES "{schema}".site(id)
                );

                CREATE UNIQUE INDEX ix_site_archived_site_id
                ON "{schema}".site_archived(site_id);

                CREATE TABLE "{schema}".notification_setting
                (
                    id uuid PRIMARY KEY,
                    site_user_id uuid NOT NULL,
                    email boolean NOT NULL,
                    sms boolean NOT NULL,
                    start_time interval,
                    end_time interval
                );

                CREATE UNIQUE INDEX ix_notification_setting_site_user_id
                ON "{schema}".notification_setting(site_user_id);

                INSERT INTO "{schema}".site
                    (id, site_name, create_date, archived)
                VALUES
                    (@site_id, 'PostgreSQL Concurrency Site', @now_utc, FALSE);

                INSERT INTO "{schema}".site_user
                    (id, start_date, user_id, site_id, site_contact)
                VALUES
                    (@site_user_id, @now_utc, @user_id, @site_id, TRUE);
                """;
            command.Parameters.AddWithValue("site_id", siteId);
            command.Parameters.AddWithValue("site_user_id", siteUserId);
            command.Parameters.AddWithValue("user_id", Guid.NewGuid());
            command.Parameters.AddWithValue(
                "now_utc",
                new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc));
            await command.ExecuteNonQueryAsync();
            return fixture;
        }

        public Task<SiteArchiveClaimResult> ClaimArchiveAsync(
            string archiveUrl) =>
            ExecuteAsync(
                async (context, adapter, token) =>
                {
                    SiteArchiveClaimResult claim = await adapter.TryClaimArchiveAsync(
                        SiteId,
                        "postgres-test",
                        archiveUrl,
                        new DateTime(
                            2026,
                            7,
                            24,
                            12,
                            0,
                            0,
                            DateTimeKind.Utc),
                        token);
                    if (claim.Claimed)
                    {
                        await context.SaveChangesAsync(token);
                    }

                    return claim;
                });

        public Task<bool> UpsertNotificationAsync(
            SiteNotificationSettingMutation request,
            TimeSpan startTime,
            TimeSpan endTime) =>
            ExecuteAsync(
                async (context, adapter, token) =>
                {
                    await adapter.UpsertNotificationSettingAsync(
                        SiteUserId,
                        request,
                        startTime,
                        endTime,
                        token);
                    await context.SaveChangesAsync(token);
                    return true;
                });

        public async Task<SiteWriteState> ReadStateAsync()
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(
                ConnectionString());
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM site_archived),
                    (SELECT archived FROM site LIMIT 1),
                    (SELECT COUNT(*) FROM notification_setting),
                    email,
                    sms,
                    start_time,
                    end_time
                FROM notification_setting
                LIMIT 1;
                """;
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new SiteWriteState(
                checked((int)reader.GetInt64(0)),
                reader.GetBoolean(1),
                checked((int)reader.GetInt64(2)),
                new NotificationValue(
                    reader.GetBoolean(3),
                    reader.GetBoolean(4),
                    reader.IsDBNull(5)
                        ? null
                        : reader.GetFieldValue<TimeSpan>(5),
                    reader.IsDBNull(6)
                        ? null
                        : reader.GetFieldValue<TimeSpan>(6)));
        }

        public async Task<RVTDbContext> CreateDomainContextAsync()
        {
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString());
            await connection.OpenAsync();
            DbContextOptions<RVTDbContext> options = new DbContextOptionsBuilder<RVTDbContext>()
                .UseNpgsql(connection)
                .Options;
            return new OwnedNpgsqlDomainContext(options, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(
                baseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;";
            await command.ExecuteNonQueryAsync();
        }

        private async Task<T> ExecuteAsync<T>(
            Func<
                RVTDbContext,
                EfSiteWriteAdapter,
                CancellationToken,
                Task<T>> operation)
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(
                ConnectionString());
            await connection.OpenAsync();
            DbContextOptions<RVTDbContext> options = new DbContextOptionsBuilder<RVTDbContext>()
                .UseNpgsql(connection)
                .Options;
            await using RVTDbContext context = new RVTDbContext(options);
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync();
            T? result = await operation(
                context,
                new EfSiteWriteAdapter(context),
                CancellationToken.None);
            await transaction.CommitAsync();
            return result;
        }

        private string ConnectionString()
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(
                baseConnectionString)
            {
                SearchPath = schema
            };
            return builder.ConnectionString;
        }
    }

    private sealed class OwnedNpgsqlDomainContext(
        DbContextOptions<RVTDbContext> options,
        NpgsqlConnection connection)
        : RVTDbContext(options)
    {
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
