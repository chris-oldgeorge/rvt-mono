using Npgsql;
using Quartz;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;
using Rvt.Reporting.Data.Postgres;
using Rvt.Reporting.Messaging.SendGrid;
using Rvt.Reporting.Pdf.Documents;
using Rvt.Reporting.Service.Api;
using Rvt.Reporting.Service.Scheduling;
using Rvt.Reporting.Storage.AzureBlob;
using Rvt.Reporting.Storage.PortalContent;
using Rvt.Reporting.Storage.ReportInsights;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:8080");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();
builder.Services.AddScoped<IReportingRepository, PostgresReportingRepository>();
builder.Services.AddScoped<IReportPdfRenderer, QuestPdfReportRenderer>();
builder.Services.AddScoped<IReportStorage, AzureBlobReportStorage>();
builder.Services.AddScoped<IReportMessageSender, SendGridReportMessageSender>();
builder.Services.AddHttpClient<ICustomerLogoProvider, SpaCustomerLogoClient>();
builder.Services.AddHttpClient<IReportNarrativeProvider, OllamaReportNarrativeProvider>();

var connectionString = builder.Configuration.GetConnectionString("ReportingDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:ReportingDatabase is required.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

builder.Services.Configure<AzureBlobReportStorageOptions>(options =>
{
    options.ServiceUri = builder.Configuration["RVT:BLOB_SERVICE_URI"];
    options.ConnectionString = builder.Configuration["RVT:BLOB_CONNECTION_STRING"];
    options.ContainerName = builder.Configuration["RVT:BLOB_REPORT_CONTAINER_NAME"] ?? options.ContainerName;
});

builder.Services.Configure<SendGridReportMessageOptions>(options =>
{
    options.EmailEnabled = builder.Configuration.GetValue("RVT:EMAIL_ENABLED", true);
    options.EmailTestMode = builder.Configuration.GetValue("RVT:EMAIL_TEST_MODE", false);
    options.TestReportToEmail = builder.Configuration["RVT:EMAIL_TEST_REPORT_TO_EMAIL"];
    options.FromEmail = builder.Configuration["RVT:EMAIL_ALERT_FROM_EMAIL"] ?? options.FromEmail;
    options.FromName = builder.Configuration["RVT:EMAIL_ALERT_FROM_NAME"] ?? options.FromName;
    options.ApiKey = builder.Configuration["RVT:SENDGRID_API_KEY"];
});

builder.Services.Configure<InternalApiOptions>(options =>
{
    options.InternalApiKey = builder.Configuration["RVT:INTERNAL_API_KEY"];
});

builder.Services.Configure<SpaCustomerLogoClientOptions>(options =>
{
    options.BaseUrl = builder.Configuration["RVT:SPA_BACKEND_BASE_URL"];
    options.InternalApiKey = builder.Configuration["RVT:SPA_REPORT_CONTENT_API_KEY"];
});

builder.Services.AddSingleton(new OllamaReportNarrativeOptions
{
    Enabled = builder.Configuration.GetValue("RVT:AI_SUMMARY_ENABLED", false),
    BaseUrl = builder.Configuration["RVT:AI_SUMMARY_BASE_URL"] ?? "http://localhost:11434",
    Model = builder.Configuration["RVT:AI_SUMMARY_MODEL"] ?? "llama3.2",
    TimeoutSeconds = builder.Configuration.GetValue("RVT:AI_SUMMARY_TIMEOUT_SECONDS", 8)
});

builder.Services.AddQuartz(quartz =>
{
    var jobKey = new JobKey(ScheduledReportsJob.Name);
    var cron = builder.Configuration["Quartz:ScheduledReportsCron"] ?? "0 0 1-6 ? * * *";
    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["Quartz:TimeZone"] ?? "UTC");

    quartz.AddJob<ScheduledReportsJob>(job => job.WithIdentity(jobKey));
    quartz.AddTrigger(trigger => trigger
        .ForJob(jobKey)
        .WithIdentity($"{ScheduledReportsJob.Name}-trigger")
        .WithCronSchedule(cron, schedule => schedule
            .InTimeZone(timeZone)
            .WithMisfireHandlingInstructionDoNothing()));
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var app = builder.Build();

app.MapGet("/health/live", (IConfiguration configuration) =>
{
    var serviceName = configuration["RVT:SERVICE_NAME"] ?? "RVT Reporting";
    var serviceVersion = configuration["RVT:SERVICE_VERSION"] ?? "dev";
    return Results.Ok(new { status = "live", serviceName, serviceVersion });
});

app.MapGet("/health/ready", async (IReportingRepository repository, CancellationToken cancellationToken) =>
{
    var canConnect = await repository.CanConnectAsync(cancellationToken).ConfigureAwait(false);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

var internalApi = app.MapGroup("/internal").AddEndpointFilter<InternalApiKeyFilter>();
internalApi.MapReportingEndpoints();

app.Run();

public partial class Program;
