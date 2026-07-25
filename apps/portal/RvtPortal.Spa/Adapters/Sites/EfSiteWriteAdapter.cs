// File summary: Implements PostgreSQL site mutation persistence with intentional SQLite/InMemory test paths.
// Major updates:
// - 2026-07-25 pending Removed retired SQL Server locked-batch writes while preserving ON CONFLICT concurrency.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Spa.Adapters.Sites;

public sealed class EfSiteWriteAdapter(RVTDbContext domainContext)
    : ISiteWritePort
{
    public Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken)
    {
        var source = mutation.Source;
        var site = new Site
        {
            SiteName = source.SiteName,
            AddressLine1 = source.AddressLine1,
            AddressLine2 = source.AddressLine2,
            Postcode = source.Postcode,
            City = source.City,
            County = source.County,
            StartTime = mutation.StartTime,
            EndTime = mutation.EndTime,
            SatStartTime = mutation.SaturdayStartTime,
            SatEndTime = mutation.SaturdayEndTime,
            SunStartTime = mutation.SundayStartTime,
            SunEndTime = mutation.SundayEndTime,
            CreateDate = DateTime.SpecifyKind(createDateUtc, DateTimeKind.Utc),
            Contracts = [],
            OperatingHours = mutation.OperatingHours
                .Select(hours => new SiteOperatingHours
                {
                    DayOfWeek = hours.DayOfWeek,
                    StartTime = hours.StartTime,
                    EndTime = hours.EndTime,
                    IsClosed = hours.IsClosed
                })
                .ToList()
        };
        domainContext.Sites.Add(site);
        return Task.FromResult(site.Id);
    }

    public async Task<bool> TryClaimContractAsync(
        Guid contractId,
        Guid companyId,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        // EF InMemory does not implement ExecuteUpdateAsync. Keep the production
        // relational claim atomic while preserving equivalent host-contract
        // behavior for the suite's non-relational provider.
        if (!domainContext.Database.IsRelational())
        {
            var contract = await domainContext.Contracts.SingleOrDefaultAsync(
                item =>
                    item.Id == contractId &&
                    item.CompanyId == companyId &&
                    item.SiteiD == null,
                cancellationToken);
            if (contract is null)
            {
                return false;
            }

            contract.SiteiD = siteId;
            await domainContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await domainContext.Contracts
            .Where(contract =>
                contract.Id == contractId &&
                contract.CompanyId == companyId &&
                contract.SiteiD == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    contract => contract.SiteiD,
                    siteId),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site is null)
        {
            return false;
        }

        var source = mutation.Source;
        site.SiteName = source.SiteName;
        site.AddressLine1 = source.AddressLine1;
        site.AddressLine2 = source.AddressLine2;
        site.Postcode = source.Postcode;
        site.City = source.City;
        site.County = source.County;
        site.StartTime = mutation.StartTime;
        site.EndTime = mutation.EndTime;
        site.SatStartTime = mutation.SaturdayStartTime;
        site.SatEndTime = mutation.SaturdayEndTime;
        site.SunStartTime = mutation.SundayStartTime;
        site.SunEndTime = mutation.SundayEndTime;
        site.OperatingHours.Clear();
        foreach (var hours in mutation.OperatingHours)
        {
            site.OperatingHours.Add(new SiteOperatingHours
            {
                SiteId = site.Id,
                DayOfWeek = hours.DayOfWeek,
                StartTime = hours.StartTime,
                EndTime = hours.EndTime,
                IsClosed = hours.IsClosed
            });
        }

        return true;
    }

    public async Task<SiteArchiveClaimResult> TryClaimArchiveAsync(
        Guid siteId,
        string createdBy,
        string archiveUrl,
        DateTime archivedUtc,
        CancellationToken cancellationToken)
    {
        if (!domainContext.Database.IsRelational())
        {
            var existing = await domainContext.SiteArchived
                .SingleOrDefaultAsync(
                    item => item.SiteId == siteId,
                    cancellationToken);
            if (existing is not null)
            {
                return new SiteArchiveClaimResult(
                    Claimed: false,
                    existing.PictureLink);
            }

            var inMemorySite = await domainContext.Sites
                .SingleAsync(item => item.Id == siteId, cancellationToken);
            inMemorySite.Archived = true;
            domainContext.SiteArchived.Add(new SiteArchived
            {
                SiteId = siteId,
                CreatedBy = createdBy,
                PictureLink = archiveUrl,
                CreateDate = DateTime.SpecifyKind(archivedUtc, DateTimeKind.Utc)
            });
            return new SiteArchiveClaimResult(
                Claimed: true,
                archiveUrl);
        }

        var archiveId = Guid.NewGuid();
        var createDateUtc = DateTime.SpecifyKind(
            archivedUtc,
            DateTimeKind.Utc);
        if (!IsPostgres() && !IsSqlite())
        {
            throw UnsupportedRelationalProvider();
        }

        var affected = await domainContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "site_archived"
                ("id", "created_by", "create_date", "picture_link", "site_id")
            VALUES
                ({archiveId}, {createdBy}, {createDateUtc}, {archiveUrl}, {siteId})
            ON CONFLICT ("site_id") DO NOTHING
            """,
            cancellationToken);

        if (affected == 0)
        {
            var durableArchiveUrl = await domainContext.SiteArchived
                .AsNoTracking()
                .Where(item => item.SiteId == siteId)
                .Select(item => item.PictureLink)
                .SingleAsync(cancellationToken);
            return new SiteArchiveClaimResult(
                Claimed: false,
                durableArchiveUrl);
        }

        var site = await domainContext.Sites
            .SingleAsync(item => item.Id == siteId, cancellationToken);
        site.Archived = true;
        return new SiteArchiveClaimResult(
            Claimed: true,
            archiveUrl);
    }

    public async Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken)
    {
        if (domainContext.Database.IsRelational())
        {
            var settingId = Guid.NewGuid();
            if (!IsPostgres() && !IsSqlite())
            {
                throw UnsupportedRelationalProvider();
            }

            await domainContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "notification_setting"
                    ("id", "site_user_id", "email", "sms", "start_time", "end_time")
                VALUES
                    ({settingId}, {siteUserId}, {request.Email}, {request.Sms}, {startTime}, {endTime})
                ON CONFLICT ("site_user_id") DO UPDATE
                SET
                    "email" = EXCLUDED."email",
                    "sms" = EXCLUDED."sms",
                    "start_time" = EXCLUDED."start_time",
                    "end_time" = EXCLUDED."end_time"
                """,
                cancellationToken);
            return;
        }

        var settings = await domainContext.NotificationSettings
            .SingleOrDefaultAsync(
                item => item.SiteUserId == siteUserId,
                cancellationToken);
        if (settings is null)
        {
            settings = new NotificationSettings
            {
                SiteUserId = siteUserId
            };
            domainContext.NotificationSettings.Add(settings);
        }

        settings.Email = request.Email;
        settings.SMS = request.Sms;
        settings.StartTime = startTime;
        settings.EndTime = endTime;
    }

    private bool IsPostgres() =>
        string.Equals(
            domainContext.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);

    private bool IsSqlite() =>
        string.Equals(
            domainContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal);

    private InvalidOperationException UnsupportedRelationalProvider() =>
        new(
            $"Provider '{domainContext.Database.ProviderName}' does not have an atomic Sites write implementation.");
}
