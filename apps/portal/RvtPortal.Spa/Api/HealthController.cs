// File summary: Exposes API endpoints used by the React portal for health controller workflows.
// Major updates:
// - 2026-07-09 pending Refined generated endpoint comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace RvtPortal.Spa.Api;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IHostEnvironment environment;

    // Function summary: Initializes health endpoints with the current host environment.
    public HealthController(IHostEnvironment environment)
    {
        this.environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetHealthResponse), StatusCodes.Status200OK)]
    // Function summary: Returns the lightweight API health payload.
    public ActionResult<GetHealthResponse> Get()
    {
        return new GetHealthResponse
        {
            ServerTimeUtc = DateTime.UtcNow
        };
    }

    [HttpGet("diagnostics/download")]
    [ApiExplorerSettings(IgnoreApi = true)]
    // Function summary: Downloads a development-only diagnostics smoke file.
    public IActionResult DownloadDiagnostics()
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return NotFound(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Diagnostic download not available."));
        }

        var bytes = Encoding.UTF8.GetBytes("RVT Portal SPA diagnostics\n");
        return File(bytes, "text/plain", "rvt-portal-spa-diagnostics.txt");
    }

    [HttpGet("diagnostics/fault")]
    [ApiExplorerSettings(IgnoreApi = true)]
    // Function summary: Throws a development-only diagnostic fault for exception middleware verification.
    public IActionResult ThrowDiagnosticFault()
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return NotFound(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Diagnostic fault not available."));
        }

        throw new InvalidOperationException("Diagnostic API fault for error handling verification.");
    }
}
