// File summary: Exposes API endpoints used by the React portal for monitor data view workflows.
// Major updates:
// - 2026-07-09 pending Routed data grid, graph, trace, and CSV workflows through an application service.
// - 2026-06-26 pending Scoped monitor data and traces to effective deployment/contract ownership windows.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic;
using RvtPortal.Spa.Application.Data;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/data")]
public class DataController : ControllerBase
{
    /// <summary>Set to "true" when a CSV export stopped at the reader's row bound and is therefore partial.</summary>
    public const string TruncatedHeader = "X-RVT-Truncated";
    private static readonly string[] TimestampQueryFields = ["fromDate", "toDate"];

    private readonly IDataApplicationService dataApplication;

    // Function summary: Initializes the data controller with the application service that owns data-view workflows.
    public DataController(IDataApplicationService dataApplication)
    {
        this.dataApplication = dataApplication;
    }

    [HttpGet("deployments/{deploymentId:guid}/grid")]
    [ProducesResponseType(typeof(MonitorDataGridResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns paged monitor data grid rows for a visible deployment.
    public async Task<ActionResult<MonitorDataGridResponse>> Grid(Guid deploymentId, [FromQuery] MonitorDataGridRequest request)
    {
        if (ValidateUtcQueryTimestamps() is { } timestampProblem)
        {
            return BadRequest(timestampProblem);
        }

        var result = await dataApplication.GetGridAsync(deploymentId, request, CurrentActor(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("deployments/{deploymentId:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Streams monitor data as CSV for a visible deployment.
    public async Task<IActionResult> Download(Guid deploymentId, [FromQuery] MonitorDataGridRequest request)
    {
        if (ValidateUtcQueryTimestamps() is { } timestampProblem)
        {
            return BadRequest(timestampProblem);
        }

        var result = await dataApplication.DownloadAsync(deploymentId, request, CurrentActor(), HttpContext.RequestAborted);
        return ToDownloadResult(result);
    }

    [HttpGet("deployments/{deploymentId:guid}/graph")]
    [ProducesResponseType(typeof(MonitorGraphResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns monitor graph data and alert thresholds for a visible deployment.
    public async Task<ActionResult<MonitorGraphResponse>> Graph(Guid deploymentId, [FromQuery] MonitorGraphRequest request)
    {
        if (ValidateUtcQueryTimestamps() is { } timestampProblem)
        {
            return BadRequest(timestampProblem);
        }

        var result = await dataApplication.GetGraphAsync(deploymentId, request, CurrentActor(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("deployments/{deploymentId:guid}/traces")]
    [ProducesResponseType(typeof(TraceListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns vibration trace indexes for a visible deployment.
    public async Task<ActionResult<TraceListResponse>> Traces(Guid deploymentId, [FromQuery] TraceListRequest request)
    {
        if (ValidateUtcQueryTimestamps() is { } timestampProblem)
        {
            return BadRequest(timestampProblem);
        }

        var result = await dataApplication.GetTracesAsync(deploymentId, request, CurrentActor(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("deployments/{deploymentId:guid}/traces/{traceId:guid}")]
    [ProducesResponseType(typeof(TraceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns vibration trace samples for a visible deployment and trace.
    public async Task<ActionResult<TraceDetailResponse>> TraceDetail(Guid deploymentId, Guid traceId)
    {
        var result = await dataApplication.GetTraceDetailAsync(deploymentId, traceId, CurrentActor(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("deployments/{deploymentId:guid}/traces/{traceId:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Streams vibration trace samples as CSV for a visible deployment and trace.
    public async Task<IActionResult> TraceDownload(Guid deploymentId, Guid traceId)
    {
        var result = await dataApplication.DownloadTraceAsync(deploymentId, traceId, CurrentActor(), HttpContext.RequestAborted);
        return ToDownloadResult(result);
    }

    // Function summary: Converts application workflow results into API action results.
    private ActionResult<T> ToActionResult<T>(DataWorkflowResult<T> result)
        where T : class
    {
        if (result.Value is not null)
        {
            return result.Value;
        }

        return ToProblemResult<T>(result.Failure!);
    }

    // Function summary: Converts download workflow results into file or problem responses.
    private IActionResult ToDownloadResult(DataDownloadWorkflowResult result)
    {
        if (result.Download is not null)
        {
            if (result.Download.Truncated)
            {
                // A CSV body has nowhere to put this, so the fact that the export stopped at the reader's row
                // bound travels as a header rather than being dropped.
                Response.Headers[TruncatedHeader] = "true";
            }

            return File(Encoding.UTF8.GetBytes(result.Download.Content), result.Download.ContentType, result.Download.FileName);
        }

        return ToProblemResult(result.Failure!);
    }

    // Function summary: Converts application workflow failures into typed API problem responses.
    private ActionResult<T> ToProblemResult<T>(DataWorkflowFailure failure)
        where T : class
    {
        return failure.Kind switch
        {
            DataWorkflowFailureKind.InvalidSort => BadRequest(new ProblemDetails
            {
                Title = "Unsupported sort field",
                Detail = $"Sort field '{failure.RequestedSort}' is not supported. Allowed values: {string.Join(", ", failure.AllowedFields ?? [])}"
            }),
            DataWorkflowFailureKind.InvalidTimestamp => BadRequest(InvalidTimestampProblem(failure)),
            DataWorkflowFailureKind.TraceNotFound => NotFound(new ProblemDetails
            {
                Title = "Trace not found",
                Detail = $"Trace '{failure.EntityId}' was not found or is not visible to the current user."
            }),
            _ => NotFound(ProblemDetailsFor(failure))
        };
    }

    // Function summary: Converts application workflow failures into untyped API problem responses.
    private IActionResult ToProblemResult(DataWorkflowFailure failure)
    {
        if (failure.Kind == DataWorkflowFailureKind.InvalidSort)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported sort field",
                Detail = $"Sort field '{failure.RequestedSort}' is not supported. Allowed values: {string.Join(", ", failure.AllowedFields ?? [])}"
            });
        }

        if (failure.Kind == DataWorkflowFailureKind.InvalidTimestamp)
        {
            return BadRequest(InvalidTimestampProblem(failure));
        }

        return NotFound(ProblemDetailsFor(failure));
    }

    // Function summary: Builds the bad-request payload for ambiguous or non-UTC date bounds.
    private static ProblemDetails InvalidTimestampProblem(DataWorkflowFailure failure)
    {
        return new ProblemDetails
        {
            Title = "UTC timestamps required",
            Detail = $"The following fields must be explicit UTC instants ending in 'Z': {string.Join(", ", failure.InvalidFields ?? [])}."
        };
    }

    // Function summary: Preserves the wire-format distinction that DateTime model binding loses for offset timestamps.
    private ProblemDetails? ValidateUtcQueryTimestamps()
    {
        var invalidFields = TimestampQueryFields
            .Where(field =>
            {
                var value = Request.Query[field].ToString();
                return value.Length > 0 && !value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        return invalidFields.Length == 0
            ? null
            : InvalidTimestampProblem(DataWorkflowFailure.InvalidTimestamp(invalidFields));
    }

    // Function summary: Builds the existing API problem-details payloads from application failure facts.
    private static ProblemDetails ProblemDetailsFor(DataWorkflowFailure failure)
    {
        return failure.Kind switch
        {
            DataWorkflowFailureKind.DeploymentNotFound => new ProblemDetails
            {
                Title = "Deployment not found",
                Detail = $"Deployment '{failure.EntityId}' was not found or is not visible to the current user."
            },
            DataWorkflowFailureKind.TraceNotFound => new ProblemDetails
            {
                Title = "Trace not found",
                Detail = $"Trace '{failure.EntityId}' was not found or is not visible to the current user."
            },
            DataWorkflowFailureKind.NoTraceDataToDownload => new ProblemDetails
            {
                Title = "No trace data to download",
                Detail = "There are no trace samples for this trace."
            },
            DataWorkflowFailureKind.NoDataToDownload => new ProblemDetails
            {
                Title = "No data to download",
                Detail = "There are no matching records for this deployment and date range."
            },
            _ => new ProblemDetails
            {
                Title = "Data request failed",
                Detail = "The data request could not be completed."
            }
        };
    }

    // Function summary: Captures the authenticated caller's data-view role facts for the application service.
    private DataViewActor CurrentActor()
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;
        return new DataViewActor(
            userId,
            User.IsInRole(RoleNames.RVTMasterAdmin) || User.IsInRole(RoleNames.RVTAdmin),
            User.IsInRole(RoleNames.CompanyUser));
    }
}
