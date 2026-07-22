using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Reports;

public interface IReportingDataQueries
{
    Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}
