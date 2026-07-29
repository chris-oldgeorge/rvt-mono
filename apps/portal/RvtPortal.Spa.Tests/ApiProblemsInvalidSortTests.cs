// File summary: Pins the single invalid-sort response shape shared by every list controller.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// Nine controllers previously carried private <c>InvalidSort</c> helpers in
/// two diverged shapes (plain problem text vs. a machine-readable
/// <c>allowedSortFields</c> extension). <see cref="ApiProblems.InvalidSort"/>
/// is now the only shape; these tests pin its contract.
/// </summary>
public sealed class ApiProblemsInvalidSortTests
{
    [Fact]
    public void InvalidSort_ProducesTheCanonicalProblem()
    {
        BadRequestObjectResult result = ApiProblems.InvalidSort(
            new DefaultHttpContext(),
            "bogusField",
            ["name", "createdAt"],
            "widgets");

        ProblemDetails problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Invalid sort field", problem.Title);
        Assert.Equal("Sort field 'bogusField' is not supported for widgets.", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    [Fact]
    public void InvalidSort_OrdersTheAllowedFieldsCaseInsensitively()
    {
        BadRequestObjectResult result = ApiProblems.InvalidSort(
            new DefaultHttpContext(),
            "bogusField",
            ["siteName", "CompanyName", "alertLevel"],
            "widgets");

        ProblemDetails problem = Assert.IsType<ProblemDetails>(result.Value);
        string[] allowed = Assert.IsType<string[]>(problem.Extensions["allowedSortFields"]);
        Assert.Equal(["alertLevel", "CompanyName", "siteName"], allowed);
    }
}
