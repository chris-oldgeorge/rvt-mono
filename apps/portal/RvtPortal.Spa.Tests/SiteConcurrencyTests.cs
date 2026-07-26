using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Sites;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class SiteConcurrencyTests
{
    [Fact]
    public async Task ArchiveAsync_ConcurrentRequestsKeepOneDurableArtifactAndReturnStableSuccess()
    {
        await using var fixture = await RelationalSiteFixture.CreateAsync();
        await using var firstScope = await fixture.CreateScopeAsync();
        await using var secondScope = await fixture.CreateScopeAsync();
        var archives = new CoordinatedArchivePort();
        var firstService = CreateArchiveService(firstScope, archives);
        var secondService = CreateArchiveService(secondScope, archives);
        var admin = Admin();

        var first = firstService.ArchiveAsync(
            admin,
            fixture.SiteId,
            "first-admin",
            CancellationToken.None);
        await archives.FirstExportEntered;
        var second = secondService.ArchiveAsync(
            admin,
            fixture.SiteId,
            "second-admin",
            CancellationToken.None);
        await archives.SecondExportEntered;

        var firstResult = await first;
        archives.AllowSecondExport();
        var secondResult = await second;

        await using var verification = await fixture.CreateDomainContextAsync();
        var metadata = await verification.SiteArchived
            .AsNoTracking()
            .Where(item => item.SiteId == fixture.SiteId)
            .ToListAsync();
        Assert.Multiple(
            () => Assert.Equal(UseCaseResultKind.Success, firstResult.Kind),
            () => Assert.Equal(UseCaseResultKind.Success, secondResult.Kind),
            () => Assert.Single(metadata),
            () => Assert.True(verification.Sites
                .Where(item => item.Id == fixture.SiteId)
                .Select(item => item.Archived)
                .Single()),
            () => Assert.Single(archives.ActiveUrls),
            () => Assert.Equal(0, archives.CleanupCount),
            () => Assert.Contains(metadata.Single().PictureLink!, archives.ActiveUrls));
    }

    [Fact]
    public async Task ArchiveAsync_ArchivedLegacyUrlDeletesOnlyStableCandidate()
    {
        await using var fixture = await RelationalSiteFixture.CreateAsync();
        const string legacyArchiveUrl = "https://archive.example/legacy/site.zip";
        await using (var setup = await fixture.CreateDomainContextAsync())
        {
            var site = await setup.Sites.SingleAsync(item => item.Id == fixture.SiteId);
            site.Archived = true;
            setup.SiteArchived.Add(new SiteArchived
            {
                Id = Guid.NewGuid(),
                SiteId = fixture.SiteId,
                PictureLink = legacyArchiveUrl,
                CreatedBy = "legacy-admin",
                CreateDate = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        await using var scope = await fixture.CreateScopeAsync();
        var archives = new CoordinatedArchivePort();
        archives.TrackActive(fixture.SiteId, legacyArchiveUrl);
        var service = CreateArchiveService(scope, archives);

        var result = await service.ArchiveAsync(
            Admin(),
            fixture.SiteId,
            "retry-admin",
            CancellationToken.None);

        Assert.Multiple(
            () => Assert.Equal(UseCaseResultKind.Success, result.Kind),
            () => Assert.Equal(0, archives.ExportCount),
            () => Assert.Equal(1, archives.CleanupCount),
            () => Assert.Single(archives.ActiveUrls),
            () => Assert.Contains(legacyArchiveUrl, archives.ActiveUrls),
            () => Assert.Equal(
                [(fixture.SiteId, legacyArchiveUrl)],
                archives.ReconciliationRequests));
    }

    [Fact]
    public async Task NotificationSettings_ConcurrentFirstWritesKeepOneCompleteRowAndRemainReadable()
    {
        await using var fixture = await RelationalSiteFixture.CreateAsync();
        await using var firstScope = await fixture.CreateScopeAsync();
        await using var secondScope = await fixture.CreateScopeAsync();
        var firstRequest = new SiteNotificationSettingMutation(
            Email: true,
            Sms: false,
            StartTime: "08:00",
            EndTime: "12:00");
        var secondRequest = new SiteNotificationSettingMutation(
            Email: false,
            Sms: true,
            StartTime: "13:00",
            EndTime: "17:00");

        var first = firstScope.Adapter.UpsertNotificationSettingAsync(
            fixture.SiteUserId,
            firstRequest,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            CancellationToken.None);
        var second = secondScope.Adapter.UpsertNotificationSettingAsync(
            fixture.SiteUserId,
            secondRequest,
            new TimeSpan(13, 0, 0),
            new TimeSpan(17, 0, 0),
            CancellationToken.None);

        await Task.WhenAll(first, second);
        await Task.WhenAll(
            firstScope.DomainContext.SaveChangesAsync(),
            secondScope.DomainContext.SaveChangesAsync());

        await using var verification = await fixture.CreateDomainContextAsync();
        var rows = await verification.NotificationSettings
            .AsNoTracking()
            .Where(item => item.SiteUserId == fixture.SiteUserId)
            .ToListAsync();
        var row = Assert.Single(rows);
        var firstValue = new NotificationValue(
            true,
            false,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0));
        var secondValue = new NotificationValue(
            false,
            true,
            new TimeSpan(13, 0, 0),
            new TimeSpan(17, 0, 0));
        var persisted = new NotificationValue(
            row.Email,
            row.SMS,
            row.StartTime,
            row.EndTime);
        Assert.Contains(persisted, new[] { firstValue, secondValue });

        var reader = new EfSiteReadAdapter(verification);
        var firstRead = await reader.GetNotificationSettingsAsync(
            fixture.SiteId,
            CancellationToken.None);
        var secondRead = await reader.GetNotificationSettingsAsync(
            fixture.SiteId,
            CancellationToken.None);
        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal(firstRead.SiteId, secondRead.SiteId);
        Assert.Equal(firstRead.SiteName, secondRead.SiteName);
        Assert.Equal(firstRead.Assignments, secondRead.Assignments);
        var assignment = Assert.Single(firstRead.Assignments);
        Assert.Equal(fixture.SiteUserId, assignment.SiteUserId);
    }

    [Fact]
    public void DomainModel_RequiresOneArchivePerSiteAndOneNotificationSettingPerSiteUser()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new RVTDbContext(options);

        var archiveIndex = Assert.Single(
            context.Model.FindEntityType(typeof(SiteArchived))!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SiteArchived.SiteId)]));
        var notificationIndex = Assert.Single(
            context.Model.FindEntityType(typeof(NotificationSettings))!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(NotificationSettings.SiteUserId)]));

        Assert.True(archiveIndex.IsUnique);
        Assert.True(notificationIndex.IsUnique);
    }

    [Fact]
    public void UniquenessMigration_GeneratesPostgreSqlDeduplicationBeforeUniqueIndexes()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql("Host=localhost;Database=rvt_migration_script;Username=rvt")
            .Options;
        using var context = new RVTDbContext(options);
        var script = context.Database.GetService<IMigrator>().GenerateScript(
            "20260714132042_CanonicalBaseline",
            "20260723234806_EnforceSiteWriteUniqueness");

        var notificationDelete = script.IndexOf(
            "DELETE FROM public.notification_setting",
            StringComparison.Ordinal);
        var archiveDelete = script.IndexOf(
            "DELETE FROM public.site_archived",
            StringComparison.Ordinal);
        var siteUpdate = script.IndexOf(
            "UPDATE public.site AS sites",
            StringComparison.Ordinal);
        var archiveIndex = script.IndexOf(
            "ix_site_archived_site_id",
            siteUpdate,
            StringComparison.Ordinal);
        var notificationIndex = script.IndexOf(
            "ix_notification_setting_site_user_id",
            siteUpdate,
            StringComparison.Ordinal);

        Assert.Multiple(
            () => Assert.Contains(
                "LOCK TABLE public.notification_setting",
                script,
                StringComparison.Ordinal),
            () => Assert.True(notificationDelete >= 0),
            () => Assert.True(archiveDelete > notificationDelete),
            () => Assert.True(siteUpdate > archiveDelete),
            () => Assert.True(archiveIndex > siteUpdate),
            () => Assert.True(notificationIndex > siteUpdate),
            () => Assert.DoesNotContain("[dbo]", script, StringComparison.Ordinal),
            () => Assert.DoesNotContain("PRAGMA", script, StringComparison.Ordinal));
    }

    private static SiteApplicationService CreateArchiveService(
        RelationalSiteScope scope,
        CoordinatedArchivePort archives)
    {
        return new SiteApplicationService(
            new ArchiveReadPort(scope.DomainContext),
            scope.Adapter,
            scope.UnitOfWork,
            new EmptyUserDirectory(),
            archives,
            new EmptyLogoPort(),
            TimeProvider.System);
    }

    private static PortalUserContext Admin() =>
        new(Guid.NewGuid(), "admin", null, true, false, false);

    private sealed record NotificationValue(
        bool Email,
        bool Sms,
        TimeSpan? StartTime,
        TimeSpan? EndTime);

    private sealed class CoordinatedArchivePort : ISiteArchivePort
    {
        private readonly TaskCompletionSource firstExportEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondExportEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource allowSecondExport =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int cleanupCount;
        private int exportCount;

        public ConcurrentDictionary<string, byte> ActiveUrls { get; } = new();
        public int CleanupCount => Volatile.Read(ref cleanupCount);
        public int ExportCount => Volatile.Read(ref exportCount);
        public ConcurrentQueue<(Guid SiteId, string DurableArchiveUrl)> ReconciliationRequests { get; } = new();
        public Task FirstExportEntered => firstExportEntered.Task;
        public Task SecondExportEntered => secondExportEntered.Task;

        public async Task<SiteArchiveExportResult> ExportAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref exportCount);
            var url = StableUrl(siteId);
            ActiveUrls.TryAdd(url, 0);
            if (call == 1)
            {
                firstExportEntered.TrySetResult();
                await secondExportEntered.Task.WaitAsync(cancellationToken);
            }
            else
            {
                secondExportEntered.TrySetResult();
                await allowSecondExport.Task.WaitAsync(cancellationToken);
            }

            return SiteArchiveExportResult.Success(url);
        }

        public void AllowSecondExport() => allowSecondExport.TrySetResult();

        public void TrackActive(Guid siteId, string durableArchiveUrl)
        {
            ActiveUrls.TryAdd(StableUrl(siteId), 0);
            ActiveUrls.TryAdd(durableArchiveUrl, 0);
        }

        public Task<SiteArchiveCleanupResult> CleanupSupersededAsync(
            Guid siteId,
            string durableArchiveUrl,
            CancellationToken cancellationToken)
        {
            ReconciliationRequests.Enqueue((siteId, durableArchiveUrl));
            var candidateUrl = StableUrl(siteId);
            if (string.Equals(
                    candidateUrl,
                    durableArchiveUrl,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(SiteArchiveCleanupResult.Success());
            }

            Interlocked.Increment(ref cleanupCount);
            return Task.FromResult(
                ActiveUrls.TryRemove(candidateUrl, out _)
                    ? SiteArchiveCleanupResult.Success()
                    : SiteArchiveCleanupResult.Failed(
                        "The archive artifact was not present."));
        }

        private static string StableUrl(Guid siteId) =>
            $"https://archive.example/{siteId:N}/site-archive.zip";
    }

    private sealed class ArchiveReadPort(RVTDbContext context) : ISiteReadPort
    {
        public Task<SiteArchiveState?> GetArchiveStateAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            context.Sites
                .AsNoTracking()
                .Where(item => item.Id == siteId)
                .Select(item => new SiteArchiveState(
                    item.Id,
                    item.Archived,
                    context.SiteArchived
                        .Where(archive => archive.SiteId == item.Id)
                        .Select(archive => archive.PictureLink)
                        .SingleOrDefault()))
                .SingleOrDefaultAsync(cancellationToken);

        public Task<SiteDetailModel?> GetAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            context.Sites
                .AsNoTracking()
                .Where(item => item.Id == siteId)
                .Select(item => new SiteDetailModel
                {
                    Id = item.Id,
                    SiteName = item.SiteName,
                    Archived = item.Archived
                })
                .SingleOrDefaultAsync(cancellationToken);

        public Task<bool> ExistsAsync(
            Guid siteId,
            SiteAccessScope scope,
            CancellationToken cancellationToken) =>
            context.Sites.AnyAsync(item => item.Id == siteId, cancellationToken);

        public Task<PagedResult<SiteListModel>> QueryAsync(
            SiteAccessScope scope,
            SiteQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SiteOptionsModel> OptionsAsync(
            Guid? companyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
            Guid siteId,
            PageRequest page,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
            Guid siteId,
            PageRequest page,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SiteMutationValidationData> GetMutationValidationDataAsync(
            SiteMutation request,
            Guid? currentSiteId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SiteNotificationSettingTarget?> GetNotificationSettingTargetAsync(
            Guid siteId,
            Guid siteUserId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyUserDirectory : IPortalUserDirectory
    {
        public Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortalUserProfile>>([]);

        public Task<PortalUserProfile?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<PortalUserProfile?>(null);
    }

    private sealed class EmptyLogoPort : ISiteLogoPort
    {
        public Task<bool> ExistsAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<SiteLogoSaveResult> SaveAsync(
            Guid siteId,
            SiteLogoUpload upload,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SiteLogoFile?> OpenReadAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RelationalSiteFixture : IAsyncDisposable
    {
        private readonly string databasePath;

        private RelationalSiteFixture(
            string databasePath,
            Guid siteId,
            Guid siteUserId)
        {
            this.databasePath = databasePath;
            SiteId = siteId;
            SiteUserId = siteUserId;
        }

        public Guid SiteId { get; }
        public Guid SiteUserId { get; }

        public static async Task<RelationalSiteFixture> CreateAsync()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"rvt-site-concurrency-{Guid.NewGuid():N}.db");
            var siteId = Guid.NewGuid();
            var siteUserId = Guid.NewGuid();
            var fixture = new RelationalSiteFixture(
                databasePath,
                siteId,
                siteUserId);
            await using var scope = await fixture.CreateScopeAsync();
            await CreateTablesAsync(scope.DomainContext);
            await CreateTablesAsync(scope.SearchContext);
            await CreateTablesAsync(scope.ApplicationContext);
            await scope.DomainContext.Database.ExecuteSqlRawAsync(
                "PRAGMA journal_mode=WAL;");
            scope.DomainContext.Sites.Add(new Site
            {
                Id = siteId,
                SiteName = "Concurrent Site",
                CreateDate = DateTime.UtcNow,
                Contracts = []
            });
            scope.DomainContext.SiteUsers.Add(new SiteUsers
            {
                Id = siteUserId,
                SiteId = siteId,
                UserId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.AddDays(-1),
                SiteContact = true
            });
            await scope.DomainContext.SaveChangesAsync();
            return fixture;
        }

        public async Task<RelationalSiteScope> CreateScopeAsync()
        {
            var connection = new SqliteConnection(
                $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30;Pooling=False");
            await connection.OpenAsync();
            var domainContext = new RVTDbContext(
                new DbContextOptionsBuilder<RVTDbContext>()
                    .UseSqlite(connection)
                    .Options);
            var searchContext = new RVTSearchContext(
                new DbContextOptionsBuilder<RVTSearchContext>()
                    .UseSqlite(connection)
                    .Options);
            var applicationContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options);
            return new RelationalSiteScope(
                connection,
                domainContext,
                searchContext,
                applicationContext);
        }

        public async Task<RVTDbContext> CreateDomainContextAsync()
        {
            var connection = new SqliteConnection(
                $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30;Pooling=False");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<RVTDbContext>()
                .UseSqlite(connection)
                .Options;
            return new OwnedConnectionDomainContext(options, connection);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            var writeAheadLog = databasePath + "-wal";
            if (File.Exists(writeAheadLog))
            {
                File.Delete(writeAheadLog);
            }

            var sharedMemory = databasePath + "-shm";
            if (File.Exists(sharedMemory))
            {
                File.Delete(sharedMemory);
            }

            return ValueTask.CompletedTask;
        }

        private static async Task CreateTablesAsync(DbContext context)
        {
            await context.Database
                .GetService<IRelationalDatabaseCreator>()
                .CreateTablesAsync();
        }
    }

    private sealed class RelationalSiteScope : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        public RelationalSiteScope(
            SqliteConnection connection,
            RVTDbContext domainContext,
            RVTSearchContext searchContext,
            ApplicationDbContext applicationContext)
        {
            this.connection = connection;
            DomainContext = domainContext;
            SearchContext = searchContext;
            ApplicationContext = applicationContext;
            UnitOfWork = new EfCoreUnitOfWork(
                domainContext,
                searchContext,
                applicationContext);
            Adapter = new EfSiteWriteAdapter(domainContext);
        }

        public RVTDbContext DomainContext { get; }
        public RVTSearchContext SearchContext { get; }
        public ApplicationDbContext ApplicationContext { get; }
        public EfCoreUnitOfWork UnitOfWork { get; }
        public EfSiteWriteAdapter Adapter { get; }

        public async ValueTask DisposeAsync()
        {
            await DomainContext.DisposeAsync();
            await SearchContext.DisposeAsync();
            await ApplicationContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class OwnedConnectionDomainContext(
        DbContextOptions<RVTDbContext> options,
        SqliteConnection connection)
        : RVTDbContext(options)
    {
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
