// File summary: Defines the report-generation service port used by report-rule business workflows.
// Major updates:
// - 2026-07-05 pending Added report-generation gateway abstraction for controller-to-business refactoring.

using RVT.BusinessLogic.Application;

namespace RVT.BusinessLogic.Reports;

public interface IReportGenerationGateway
{
    // Function summary: Requests report generation from the host-provided reporting-service integration.
    Task<ApplicationResult<ReportGenerationResponseModel>> RequestGenerationAsync(
        Guid reportRuleId,
        ReportGenerationRequestModel request,
        CancellationToken cancellationToken);
}
