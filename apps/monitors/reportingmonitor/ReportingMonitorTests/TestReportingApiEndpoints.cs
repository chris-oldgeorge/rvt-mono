using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ReportingMonitor.Api;
using ReportingMonitor.Api.Security;
using ReportingMonitor.Api.UseCases;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitorTests;

public sealed class TestReportingApiEndpoints
{
    [Fact]
    public async Task InternalApiKeyFilter_WithMissingConfiguredKey_ReturnsUnauthorized()
    {
        var filter = CreateFilter("test-key");
        var invoked = false;

        var result = await filter.InvokeAsync(CreateInvocationContext(), _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(new StatusResult(StatusCodes.Status200OK));
        });

        Assert.False(invoked);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task InternalApiKeyFilter_WithInvalidConfiguredKey_ReturnsUnauthorized()
    {
        var filter = CreateFilter("test-key");
        var invoked = false;

        var result = await filter.InvokeAsync(CreateInvocationContext("wrong-key"), _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(new StatusResult(StatusCodes.Status200OK));
        });

        Assert.False(invoked);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task InternalApiKeyFilter_WithValidConfiguredKey_InvokesNext()
    {
        var filter = CreateFilter("test-key");
        var invoked = false;

        var result = await filter.InvokeAsync(CreateInvocationContext("test-key"), _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(new StatusResult(StatusCodes.Status200OK));
        });

        Assert.True(invoked);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<StatusResult>(result).StatusCode);
    }

    [Fact]
    public async Task InternalApiKeyFilter_WithoutConfiguredKeyInDevelopment_InvokesNext()
    {
        var filter = CreateFilter(internalApiKey: null, development: true);
        var invoked = false;

        var result = await filter.InvokeAsync(CreateInvocationContext(), _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(new StatusResult(StatusCodes.Status200OK));
        });

        Assert.True(invoked);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<StatusResult>(result).StatusCode);
    }

    [Fact]
    public async Task InternalApiKeyFilter_WithoutConfiguredKeyOutsideDevelopment_ReturnsUnauthorized()
    {
        var filter = CreateFilter(internalApiKey: null, development: false);
        var invoked = false;

        var result = await filter.InvokeAsync(CreateInvocationContext(), _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(new StatusResult(StatusCodes.Status200OK));
        });

        Assert.False(invoked);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GenerateRuleReportHandler_DelegatesRuleIdAndTriggerToService()
    {
        var service = new RecordingReportGenerationService();
        var handler = new GenerateRuleReportHandler(service);
        var ruleId = Guid.NewGuid();
        var triggerUtc = DateTimeOffset.UtcNow;

        await handler.HandleAsync(ruleId, triggerUtc, CancellationToken.None);

        Assert.Equal(ruleId, service.RuleId);
        Assert.Equal(triggerUtc, service.RuleTriggerUtc);
    }

    [Fact]
    public async Task GenerateScheduledAsync_SerializesTheEstablishedCountAndReportsEnvelope()
    {
        var expectedReport = GeneratedReport();
        var service = new RecordingReportGenerationService { ScheduledReports = [expectedReport] };
        var result = await ReportingMonitorApi.GenerateScheduledAsync(
            new GenerateScheduledReportsHandler(service),
            CancellationToken.None);

        var response = await ExecuteJsonAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, response.Body.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(expectedReport.ReportId, response.Body.RootElement.GetProperty("reports")[0].GetProperty("reportId").GetGuid());
    }

    [Fact]
    public async Task GenerateRuleAsync_UsesOptionalBodyTriggerAndSerializesTheEstablishedEnvelope()
    {
        var expectedReport = GeneratedReport();
        var service = new RecordingReportGenerationService { RuleReports = [expectedReport] };
        var handler = new GenerateRuleReportHandler(service);
        var ruleId = Guid.NewGuid();
        var triggerUtc = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);

        var result = await ReportingMonitorApi.GenerateRuleAsync(
            ruleId,
            new RuleGenerationRequest(triggerUtc),
            handler,
            CancellationToken.None);
        var response = await ExecuteJsonAsync(result);

        Assert.Equal(ruleId, service.RuleId);
        Assert.Equal(triggerUtc, service.RuleTriggerUtc);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, response.Body.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(expectedReport.ReportId, response.Body.RootElement.GetProperty("reports")[0].GetProperty("reportId").GetGuid());
    }

    [Fact]
    public async Task GenerateOneTimeReportHandler_PropagatesValidationException()
    {
        var service = new RecordingReportGenerationService
        {
            OneTimeException = new OneTimeReportValidationException([
                new ValidationError(nameof(OneTimeReportRequest.FromUtc), "Start time must be earlier than end time.")
            ])
        };
        var handler = new GenerateOneTimeReportHandler(service);

        var exception = await Assert.ThrowsAsync<OneTimeReportValidationException>(() =>
            handler.HandleAsync(new OneTimeReportRequest(), CancellationToken.None));

        Assert.Single(exception.Errors);
    }

    [Fact]
    public async Task CreateOneTimeReportValidationProblem_GroupsMultipleRecipientErrors()
    {
        var result = ReportingMonitorApi.CreateOneTimeReportValidationProblem(new OneTimeReportValidationException([
            new ValidationError(nameof(OneTimeReportRequest.RecipientEmails), "Invalid recipient email: first-invalid"),
            new ValidationError(nameof(OneTimeReportRequest.RecipientEmails), "Invalid recipient email: second-invalid")
        ]));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var messages = document.RootElement
            .GetProperty("errors")
            .GetProperty(nameof(OneTimeReportRequest.RecipientEmails))
            .EnumerateArray()
            .Select(static message => message.GetString()!)
            .ToArray();

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(["Invalid recipient email: first-invalid", "Invalid recipient email: second-invalid"], messages);
    }

    private static InternalApiKeyFilter CreateFilter(string? internalApiKey, bool development = true) =>
        new(new ReportingMonitorOptions { InternalApiKey = internalApiKey }, new TestHostEnvironment(development));

    private static GeneratedReport GeneratedReport() => new(
        Guid.Parse("40000000-0000-0000-0000-000000000001"),
        Guid.Parse("40000000-0000-0000-0000-000000000002"),
        new Uri("https://reports.example.test/report.pdf"),
        new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero));

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteJsonAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddOptions<JsonOptions>()
            .Configure(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .Services
            .BuildServiceProvider();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await JsonDocument.ParseAsync(context.Response.Body));
    }

    private static EndpointFilterInvocationContext CreateInvocationContext(string? suppliedKey = null)
    {
        var context = new DefaultHttpContext();
        if (suppliedKey is not null)
        {
            context.Request.Headers[InternalApiKeyFilter.HeaderName] = suppliedKey;
        }

        return new TestEndpointFilterInvocationContext(context);
    }

    private sealed class TestEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;

        public override IList<object?> Arguments { get; } = [];

        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }

    private sealed class StatusResult(int statusCode) : IResult
    {
        public int StatusCode { get; } = statusCode;

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCode;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHostEnvironment(bool development) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = development ? Environments.Development : Environments.Production;
        public string ApplicationName { get; set; } = "ReportingMonitorTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingReportGenerationService : IReportGenerationService
    {
        public Guid? RuleId { get; private set; }
        public DateTimeOffset? RuleTriggerUtc { get; private set; }
        public OneTimeReportValidationException? OneTimeException { get; init; }
        public IReadOnlyList<GeneratedReport> ScheduledReports { get; init; } = [];
        public IReadOnlyList<GeneratedReport> RuleReports { get; init; } = [];

        public Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken) =>
            Task.FromResult(ScheduledReports);

        public Task<IReadOnlyList<GeneratedReport>> GenerateRuleAsync(Guid reportRuleId, DateTimeOffset triggerUtc, CancellationToken cancellationToken)
        {
            RuleId = reportRuleId;
            RuleTriggerUtc = triggerUtc;
            return Task.FromResult(RuleReports);
        }

        public Task<OneTimeReportResponse> GenerateOneTimeReportAsync(OneTimeReportRequest request, CancellationToken cancellationToken) =>
            OneTimeException is null
                ? Task.FromResult(new OneTimeReportResponse(Guid.NewGuid(), Guid.NewGuid(), new Uri("https://reports.example.com/test"), request.FromUtc, request.ToUtc))
                : Task.FromException<OneTimeReportResponse>(OneTimeException);
    }
}
