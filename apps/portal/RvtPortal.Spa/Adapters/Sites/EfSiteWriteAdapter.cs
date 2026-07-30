// File summary: Implements PostgreSQL site mutation persistence with atomic claim and upsert writes.
// Major updates:
// - 2026-07-30 pending Removed the InMemory fallbacks once the Spa test host moved onto PostgreSQL.
// - 2026-07-25 pending Made PostgreSQL ON CONFLICT the canonical concurrency path.

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
        SiteMutation source = mutation.Source;
        Site site = new()
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
            OperatingHours = [.. mutation.OperatingHours
                .Select(hours => new SiteOperatingHours
                {
                    DayOfWeek = hours.DayOfWeek,
                    StartTime = hours.StartTime,
                    EndTime = hours.EndTime,
                    IsClosed = hours.IsClosed
                })]
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
        int affected = await domainContext.Contracts
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
        Site? site = await domainContext.Sites
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site is null)
        {
            return false;
        }

        SiteMutation source = mutation.Source;
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
        foreach (ValidatedSiteOperatingHours hours in mutation.OperatingHours)
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
        Guid archiveId = Guid.NewGuid();
        DateTime createDateUtc = DateTime.SpecifyKind(
            archivedUtc,
            DateTimeKind.Utc);
        if (!IsPostgres() && !IsSqlite())
        {
            throw UnsupportedRelationalProvider();
        }

        int affected = await domainContext.Database.ExecuteSqlInterpolatedAsync(
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
            string? durableArchiveUrl = await domainContext.SiteArchived
                .AsNoTracking()
                .Where(item => item.SiteId == siteId)
                .Select(item => item.PictureLink)
                .SingleAsync(cancellationToken);
            return new SiteArchiveClaimResult(
                Claimed: false,
                durableArchiveUrl);
        }

        Site site = await domainContext.Sites
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
        Guid settingId = Guid.NewGuid();
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
