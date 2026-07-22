// File summary: Translates business-layer application results into ASP.NET Core API responses.
// Major updates:
// - 2026-07-08 pending Preserved application-result status metadata for outbound adapter failures.
// - 2026-07-05 pending Added transport adapter for controller-to-business refactoring.

using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;

namespace RvtPortal.Spa.Api;

public interface IApiResultMapper
{
    // Function summary: Converts a business-layer result into a typed controller action result.
    ActionResult<TResponse> ToActionResult<TModel, TResponse>(
        ControllerBase controller,
        ApplicationResult<TModel> result,
        Func<TModel, TResponse> map);
}

public sealed class ApiResultMapper : IApiResultMapper
{
    public ActionResult<TResponse> ToActionResult<TModel, TResponse>(
        ControllerBase controller,
        ApplicationResult<TModel> result,
        Func<TModel, TResponse> map)
    {
        return result.Kind switch
        {
            ApplicationResultKind.Success when result.Value is not null => map(result.Value),
            ApplicationResultKind.NotFound => controller.NotFound(CreateProblem(
                controller,
                StatusCodes.Status404NotFound,
                "Resource not found.",
                result.Message)),
            ApplicationResultKind.Forbidden => controller.Forbid(),
            ApplicationResultKind.Validation => controller.BadRequest(new ValidationProblemDetails(
                result.Errors
                    .GroupBy(error => error.Field)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()))),
            ApplicationResultKind.Conflict => controller.Conflict(CreateProblem(
                controller,
                StatusCodes.Status409Conflict,
                "Request conflict.",
                result.Message)),
            ApplicationResultKind.ExternalServiceUnavailable => ExternalServiceProblem(controller, result),
            _ => controller.StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblem(
                    controller,
                    StatusCodes.Status500InternalServerError,
                    "Unexpected application result.",
                    "The request could not be completed."))
        };
    }

    // Function summary: Creates problem details using the API correlation metadata already used by controllers.
    private static ProblemDetails CreateProblem(ControllerBase controller, int statusCode, string title, string? detail)
    {
        return ApiProblems.Create(controller.HttpContext, statusCode, title, detail);
    }

    // Function summary: Converts downstream adapter failures while preserving a specific status when supplied.
    private static ObjectResult ExternalServiceProblem<TModel>(ControllerBase controller, ApplicationResult<TModel> result)
    {
        var statusCode = result.StatusCode ?? StatusCodes.Status503ServiceUnavailable;
        return controller.StatusCode(
            statusCode,
            CreateProblem(controller, statusCode, "External service unavailable.", result.Message));
    }
}
