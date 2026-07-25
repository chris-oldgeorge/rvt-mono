// File summary: Exercises SQL Server site-write runtime DML through EF Core command interception without a live server.
// Major updates:
// - 2026-07-25 pending Added structural runtime coverage for archive claims and notification-setting upserts.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RVT.DataAccess.Context;
using RvtPortal.Application.Sites;
using RvtPortal.Spa.Adapters.Sites;

namespace RvtPortal.Spa.Tests;

public sealed class SiteSqlServerDmlTests
{
    [Fact]
    public async Task TryClaimArchiveAsync_ExecutesParameterizedLockedClaimAndConditionalSiteUpdate()
    {
        var commands = new SuppressingNonQueryInterceptor();
        await using var context = CreateContext(commands);
        var adapter = new EfSiteWriteAdapter(context);
        var siteId = Guid.Parse("8098dd31-dcf4-4784-9981-432565f92b90");
        var archivedUtc = new DateTime(
            2026,
            7,
            25,
            12,
            34,
            56,
            DateTimeKind.Utc);
        const string createdBy = "sqlserver-archive-owner";
        const string archiveUrl =
            "https://archive.example/8098dd31dcf447849981432565f92b90/site-archive.zip";

        var result = await adapter.TryClaimArchiveAsync(
            siteId,
            createdBy,
            archiveUrl,
            archivedUtc,
            CancellationToken.None);

        Assert.True(result.Claimed);
        Assert.Equal(archiveUrl, result.DurableArchiveUrl);
        var command = Assert.Single(commands.Commands);
        var sql = NormalizeSql(command.CommandText);
        var archiveIdParameter = Assert.Single(
            command.Parameters,
            parameter =>
                parameter.Value is Guid archiveId &&
                archiveId != siteId);
        var siteIdParameters = ParametersFor(command, siteId);
        var createdByParameter = Assert.Single(
            command.Parameters,
            parameter => Equals(createdBy, parameter.Value));
        var archivedUtcParameter = Assert.Single(
            command.Parameters,
            parameter => Equals(archivedUtc, parameter.Value));
        var archiveUrlParameter = Assert.Single(
            command.Parameters,
            parameter => Equals(archiveUrl, parameter.Value));
        var archivedParameter = Assert.Single(
            command.Parameters,
            parameter => Equals(true, parameter.Value));
        Assert.Equal(3, siteIdParameters.Count);
        Assert.Multiple(
            () => Assert.Contains(
                "INSERT INTO [site_archived]",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "([id], [created_by], [create_date], [picture_link], [site_id])",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "WHERE NOT EXISTS",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "FROM [site_archived] WITH (UPDLOCK, HOLDLOCK)",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "WHERE [site_id] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "IF @@ROWCOUNT > 0",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "UPDATE [site]",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "SET [archived] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "WHERE [id] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                $"SELECT {archiveIdParameter.Name}, {createdByParameter.Name}, "
                    + $"{archivedUtcParameter.Name}, {archiveUrlParameter.Name}, "
                    + siteIdParameters[0].Name,
                sql,
                StringComparison.Ordinal),
            () => Assert.Contains(
                $"WHERE [site_id] = {siteIdParameters[1].Name}",
                sql,
                StringComparison.Ordinal),
            () => Assert.Contains(
                $"SET [archived] = {archivedParameter.Name} "
                    + $"WHERE [id] = {siteIdParameters[2].Name}",
                sql,
                StringComparison.Ordinal),
            () => Assert.DoesNotContain(
                siteId.ToString(),
                command.CommandText,
                StringComparison.OrdinalIgnoreCase),
            () => Assert.DoesNotContain(
                createdBy,
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.DoesNotContain(
                archiveUrl,
                command.CommandText,
                StringComparison.Ordinal));
        AssertSqlParameters(
            command,
            siteId,
            createdBy,
            archivedUtc,
            archiveUrl,
            true);
    }

    [Fact]
    public async Task UpsertNotificationSettingAsync_ExecutesParameterizedLockedUpdateAndConditionalInsert()
    {
        var commands = new SuppressingNonQueryInterceptor();
        await using var context = CreateContext(commands);
        var adapter = new EfSiteWriteAdapter(context);
        var siteUserId =
            Guid.Parse("ab0269dd-b324-487e-a506-1a42bd6f0ba9");
        var startTime = new TimeSpan(8, 15, 0);
        var endTime = new TimeSpan(17, 45, 0);
        var request = new SiteNotificationSettingMutation(
            Email: true,
            Sms: false,
            StartTime: "08:15",
            EndTime: "17:45");

        await adapter.UpsertNotificationSettingAsync(
            siteUserId,
            request,
            startTime,
            endTime,
            CancellationToken.None);

        var command = Assert.Single(commands.Commands);
        var sql = NormalizeSql(command.CommandText);
        var emailParameters = ParametersFor(command, request.Email);
        var smsParameters = ParametersFor(command, request.Sms);
        var startParameters = ParametersFor(command, startTime);
        var endParameters = ParametersFor(command, endTime);
        var siteUserParameters = ParametersFor(command, siteUserId);
        var settingIdParameter = Assert.Single(
            command.Parameters,
            parameter =>
                parameter.Value is Guid settingId &&
                settingId != siteUserId);
        Assert.Multiple(
            () => Assert.Equal(2, emailParameters.Count),
            () => Assert.Equal(2, smsParameters.Count),
            () => Assert.Equal(2, startParameters.Count),
            () => Assert.Equal(2, endParameters.Count),
            () => Assert.Equal(2, siteUserParameters.Count));
        Assert.Multiple(
            () => Assert.Contains(
                "UPDATE [notification_setting] WITH (UPDLOCK, HOLDLOCK)",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "[email] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "[sms] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "[start_time] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "[end_time] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "WHERE [site_user_id] = ",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "IF @@ROWCOUNT = 0",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "INSERT INTO [notification_setting]",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                "([id], [site_user_id], [email], [sms], [start_time], [end_time])",
                command.CommandText,
                StringComparison.Ordinal),
            () => Assert.Contains(
                $"SET [email] = {emailParameters[0].Name}, "
                    + $"[sms] = {smsParameters[0].Name}, "
                    + $"[start_time] = {startParameters[0].Name}, "
                    + $"[end_time] = {endParameters[0].Name} "
                    + $"WHERE [site_user_id] = {siteUserParameters[0].Name}",
                sql,
                StringComparison.Ordinal),
            () => Assert.Contains(
                $"VALUES ({settingIdParameter.Name}, "
                    + $"{siteUserParameters[1].Name}, "
                    + $"{emailParameters[1].Name}, "
                    + $"{smsParameters[1].Name}, "
                    + $"{startParameters[1].Name}, "
                    + $"{endParameters[1].Name})",
                sql,
                StringComparison.Ordinal),
            () => Assert.DoesNotContain(
                siteUserId.ToString(),
                command.CommandText,
                StringComparison.OrdinalIgnoreCase));
        AssertSqlParameters(
            command,
            siteUserId,
            request.Email,
            request.Sms,
            startTime,
            endTime);
    }

    private static RVTDbContext CreateContext(
        SuppressingNonQueryInterceptor commands)
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlServer(
                "Server=unused;Database=unused;User Id=unused;Password=unused;"
                + "Encrypt=False;Connect Timeout=1")
            .AddInterceptors(
                SuppressingConnectionInterceptor.Instance,
                commands)
            .Options;
        return new RVTDbContext(options);
    }

    private static void AssertSqlParameters(
        CommandSnapshot command,
        params object[] expectedValues)
    {
        Assert.NotEmpty(command.Parameters);
        Assert.All(
            command.Parameters,
            parameter =>
            {
                Assert.Equal(
                    "Microsoft.Data.SqlClient.SqlParameter",
                    parameter.TypeName);
                Assert.Contains(
                    parameter.Name,
                    command.CommandText,
                    StringComparison.Ordinal);
            });
        foreach (var expected in expectedValues)
        {
            Assert.Contains(
                command.Parameters,
                parameter => Equals(expected, parameter.Value));
        }
    }

    private static IReadOnlyList<ParameterSnapshot> ParametersFor(
        CommandSnapshot command,
        object expectedValue) =>
        command.Parameters
            .Where(parameter => Equals(expectedValue, parameter.Value))
            .ToArray();

    private static string NormalizeSql(string sql) =>
        string.Join(
            " ",
            sql.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));

    private sealed class SuppressingConnectionInterceptor
        : DbConnectionInterceptor
    {
        public static SuppressingConnectionInterceptor Instance { get; } =
            new();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class SuppressingNonQueryInterceptor
        : DbCommandInterceptor
    {
        public List<CommandSnapshot> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            Commands.Add(new CommandSnapshot(
                command.CommandText,
                command.Parameters
                    .Cast<DbParameter>()
                    .Select(parameter => new ParameterSnapshot(
                        parameter.GetType().FullName
                            ?? parameter.GetType().Name,
                        parameter.ParameterName,
                        parameter.Value))
                    .ToArray()));
            return ValueTask.FromResult(
                InterceptionResult<int>.SuppressWithResult(1));
        }
    }

    private sealed record CommandSnapshot(
        string CommandText,
        IReadOnlyList<ParameterSnapshot> Parameters);

    private sealed record ParameterSnapshot(
        string TypeName,
        string Name,
        object? Value);
}
