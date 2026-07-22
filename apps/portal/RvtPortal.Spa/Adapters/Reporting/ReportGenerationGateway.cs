// File summary: Adapts the SPA report-generation HTTP client to the business-layer report-generation gateway.
// Major updates:
// - 2026-07-08 pending Removed adapter dependency on API mapper to keep dependency direction clean.
// - 2026-07-08 pending Moved reporting-service gateway implementation into the reporting adapter namespace.
// - 2026-07-05 pending Added report-generation gateway adapter for report-rule business workflows.

using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Reports;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Adapters.Reporting;

public sealed class ReportGenerationGateway : IReportGenerationGateway
{
    private readonly IReportGenerationClient client;

    // Function summary: Initializes this adapter with the existing reporting-service HTTP client.
    public ReportGenerationGateway(IReportGenerationClient client)
    {
        this.client = client;
    }

    public async Task<ApplicationResult<ReportGenerationResponseModel>> RequestGenerationAsync(
        Guid reportRuleId,
        ReportGenerationRequestModel request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.RequestGenerationAsync(
                reportRuleId,
                new ReportGenerationRequest
                {
                    ReportDate = request.ReportDate,
                    SendToRecipients = request.SendToRecipients
                },
                cancellationToken);
            if (response == null)
            {
                return ApplicationResult<ReportGenerationResponseModel>.NotFound($"Report rule '{reportRuleId}' was not found.");
            }

            return ApplicationResult<ReportGenerationResponseModel>.Success(ToGenerationModel(response));
        }
        catch (ReportGenerationServiceException exception)
        {
            return ApplicationResult<ReportGenerationResponseModel>.ExternalServiceUnavailable(exception.Message, exception.StatusCode);
        }
    }

    // Function summary: Converts the reporting-service adapter response into the business-layer gateway model.
    private static ReportGenerationResponseModel ToGenerationModel(ReportGenerationRequestResponse response)
    {
        return new ReportGenerationResponseModel(
            response.Id,
            response.ReportRuleId,
            response.Status,
            response.Message,
            response.RequestedAtUtc);
    }
}
