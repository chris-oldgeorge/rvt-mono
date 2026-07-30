// File summary: Defines the report-generation service port used by report-rule business workflows.
// Major updates:
// - 2026-07-30 pending Moved from RVT.BusinessLogic beside the report-rule models; results now use UseCaseResult.
// - 2026-07-05 pending Added report-generation gateway abstraction for controller-to-business refactoring.

using RvtPortal.Application.Common;

namespace RvtPortal.Spa.UseCases.ReportRules;

public interface IReportGenerationGateway
{
    // Function summary: Requests report generation from the host-provided reporting-service integration.
    Task<UseCaseResult<ReportGenerationResponseModel>> RequestGenerationAsync(
        Guid reportRuleId,
        ReportGenerationRequestModel request,
        CancellationToken cancellationToken);
}
