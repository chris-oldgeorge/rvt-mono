// File summary: Provides shared-key protected report asset fetch workflows for the reporting service.
// Major updates:
// - 2026-07-09 pending Moved report-content key validation and logo lookup out of the API controller.

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Ports.Storage;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Application.ReportContent;

public interface IReportContentApplicationService
{
    // Function summary: Returns a site customer logo when the internal reporting-service key and site are valid.
    Task<ReportContentFileResult> GetCustomerLogoAsync(
        Guid siteId,
        string? internalKey,
        CancellationToken cancellationToken);
}

public enum ReportContentFailureKind
{
    Unauthorized,
    ServiceUnavailable,
    NotFound
}

public sealed record ReportContentFileResult(StoredContentFile? File, ReportContentFailureKind? Failure)
{
    // Function summary: Wraps a successful report-content file response.
    public static ReportContentFileResult Success(StoredContentFile file)
    {
        return new ReportContentFileResult(file, null);
    }

    // Function summary: Wraps a report-content failure response.
    public static ReportContentFileResult Failed(ReportContentFailureKind failure)
    {
        return new ReportContentFileResult(null, failure);
    }
}

public sealed class ReportContentApplicationService : IReportContentApplicationService
{
    private readonly RVTDbContext domainContext;
    private readonly ICustomerLogoStorage customerLogoStorage;
    private readonly IConfiguration configuration;

    // Function summary: Initializes report-content workflows with data, storage, and configuration dependencies.
    public ReportContentApplicationService(
        RVTDbContext domainContext,
        ICustomerLogoStorage customerLogoStorage,
        IConfiguration configuration)
    {
        this.domainContext = domainContext;
        this.customerLogoStorage = customerLogoStorage;
        this.configuration = configuration;
    }

    // Function summary: Validates the internal key and opens the site logo if the site is active and has one.
    public async Task<ReportContentFileResult> GetCustomerLogoAsync(
        Guid siteId,
        string? internalKey,
        CancellationToken cancellationToken)
    {
        var configuredKey = configuration["ReportContent:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return ReportContentFileResult.Failed(ReportContentFailureKind.ServiceUnavailable);
        }

        if (string.IsNullOrEmpty(internalKey) || !FixedTimeEquals(internalKey, configuredKey))
        {
            return ReportContentFileResult.Failed(ReportContentFailureKind.Unauthorized);
        }

        var siteExists = await domainContext.Sites
            .AsNoTracking()
            .AnyAsync(site => site.Id == siteId && !site.Archived, cancellationToken);
        if (!siteExists)
        {
            return ReportContentFileResult.Failed(ReportContentFailureKind.NotFound);
        }

        var logo = await customerLogoStorage.OpenReadAsync(siteId, cancellationToken);
        return logo is null
            ? ReportContentFileResult.Failed(ReportContentFailureKind.NotFound)
            : ReportContentFileResult.Success(logo);
    }

    // Function summary: Compares the supplied and configured keys in constant time to avoid timing side channels.
    private static bool FixedTimeEquals(string provided, string configured)
    {
        // Hash both to a fixed length first so the comparison length does not leak via timing.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
