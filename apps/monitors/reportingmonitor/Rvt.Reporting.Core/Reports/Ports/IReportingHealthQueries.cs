namespace Rvt.Reporting.Core.Reports;

public interface IReportingHealthQueries
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
