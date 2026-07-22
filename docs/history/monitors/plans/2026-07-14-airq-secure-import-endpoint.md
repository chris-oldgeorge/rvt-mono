# AirQ Secure Import Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace AirQ's credential-bearing import GET endpoint with a private-by-default authenticated POST endpoint that obtains vendor credentials from runtime configuration.

**Architecture:** AirQ owns a small API-key validator and a narrow `IAirQDateImporter` port. Endpoint mapping reads the expected key from configuration, fails immediately when the API is enabled without a key, validates `X-Api-Key` in fixed time, and dispatches only the date to the importer. `AirQService` implements the importer and supplies its existing configured AirQ vendor credentials to `AirQApi`.

**Tech Stack:** .NET 10 minimal APIs, ASP.NET Core endpoint routing, `IConfiguration`, `System.Security.Cryptography.CryptographicOperations`, MSTest, Moq, Docker Compose.

## Global Constraints

- The state-changing endpoint must be `POST /store-noise-levels-for-date`; the legacy GET route must not exist.
- Only `date` is accepted from the request body. AirQ vendor credentials always come from `RVT__AIRQ_USER_ID` and `RVT__AIRQ_USER_AUTH` runtime configuration.
- Require the `X-Api-Key` header and compare it with `RVT__MONITOR_API_KEY` using `CryptographicOperations.FixedTimeEquals`.
- Missing or invalid API keys return `401 Unauthorized` without exposing which validation condition failed.
- `GET /liveness` remains unauthenticated.
- Remove AirQ's host-port publication from the base Compose file. Do not commit API keys or vendor credentials.
- Keep the implementation scoped to AirQ; do not add JWT, roles, or a shared monitor authentication framework.

---

## File Structure

- Create `airqmonitor/AirQMonitor/api/Security/AirQApiKeyValidator.cs`: validates one configured API key and performs fixed-time comparison.
- Create `airqmonitor/AirQMonitor/api/UseCases/IAirQDateImporter.cs`: endpoint-facing port that accepts only an ISO date string.
- Modify `airqmonitor/AirQMonitor/api/AirQService.cs`: implements `IAirQDateImporter` and resolves vendor credentials internally.
- Modify `airqmonitor/AirQMonitor/api/AirQMonitorServices.cs`: registers `AirQService` as both its concrete type and `IAirQDateImporter`.
- Modify `airqmonitor/AirQMonitor/api/MonitorApiEndpoints.cs`: maps the protected POST route, validates configuration at map time, and removes the legacy GET route.
- Modify `airqmonitor/AirQMonitorTests/TestMonitorApiEndpoints.cs`: verifies route metadata, authorization results, and importer dispatch with a mock.
- Create `airqmonitor/AirQMonitorTests/Security/AirQApiKeyValidatorTests.cs`: covers missing configuration, valid keys, invalid keys, and multi-value header rejection.
- Modify `docker-compose.yml`: remove AirQ's host `ports` mapping.
- Modify `docs/container-builds.md` and `README.md`: document the internal-only API, required secrets, and explicit developer-only port override command.
- Modify `project_state.md`: record the endpoint contract and configured secret names without values.

### Task 1: Build the API-Key Validator and Its Tests

**Files:**
- Create: `airqmonitor/AirQMonitor/api/Security/AirQApiKeyValidator.cs`
- Create: `airqmonitor/AirQMonitorTests/Security/AirQApiKeyValidatorTests.cs`

**Interfaces:**
- Produces: `AirQApiKeyValidator.Create(string? configuredKey)`, returning a validator or throwing `InvalidOperationException` for blank configuration.
- Produces: `bool IsAuthorized(StringValues suppliedKeys)`; accepts exactly one nonblank header value and uses fixed-time byte comparison.

- [ ] **Step 1: Write failing validator tests**

```csharp
using Microsoft.Extensions.Primitives;
using AirQ.Api.Security;

[TestClass]
public sealed class AirQApiKeyValidatorTests
{
    [TestMethod]
    public void Create_RejectsMissingConfiguredKey() =>
        Assert.ThrowsException<InvalidOperationException>(() => AirQApiKeyValidator.Create(null));

    [TestMethod]
    public void IsAuthorized_AcceptsExactlyOneMatchingHeaderValue()
    {
        var validator = AirQApiKeyValidator.Create("monitor-api-key");

        Assert.IsTrue(validator.IsAuthorized(new StringValues("monitor-api-key")));
        Assert.IsFalse(validator.IsAuthorized(StringValues.Empty));
        Assert.IsFalse(validator.IsAuthorized(new StringValues("wrong-key")));
        Assert.IsFalse(validator.IsAuthorized(new StringValues(["monitor-api-key", "wrong-key"])));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --filter FullyQualifiedName~AirQApiKeyValidatorTests`

Expected: compilation failure because `AirQ.Api.Security.AirQApiKeyValidator` does not exist.

- [ ] **Step 3: Implement the validator**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace AirQ.Api.Security;

public sealed class AirQApiKeyValidator
{
    private readonly byte[] expectedKey;

    private AirQApiKeyValidator(string configuredKey) => expectedKey = Encoding.UTF8.GetBytes(configuredKey);

    public static AirQApiKeyValidator Create(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("Monitor API requires RVT__MONITOR_API_KEY when enabled.");
        }

        return new AirQApiKeyValidator(configuredKey);
    }

    public bool IsAuthorized(StringValues suppliedKeys)
    {
        if (suppliedKeys.Count != 1 || string.IsNullOrWhiteSpace(suppliedKeys[0]))
        {
            return false;
        }

        var suppliedKey = Encoding.UTF8.GetBytes(suppliedKeys[0]!);
        return CryptographicOperations.FixedTimeEquals(expectedKey, suppliedKey);
    }
}
```

- [ ] **Step 4: Run the focused tests**

Run: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --filter FullyQualifiedName~AirQApiKeyValidatorTests`

Expected: 2 passing tests.

- [ ] **Step 5: Commit the isolated validator work**

```bash
git add airqmonitor/AirQMonitor/api/Security/AirQApiKeyValidator.cs \
  airqmonitor/AirQMonitorTests/Security/AirQApiKeyValidatorTests.cs
git commit -m "feat: add airq api key validator"
```

### Task 2: Remove Request Credentials and Protect the POST Endpoint

**Files:**
- Create: `airqmonitor/AirQMonitor/api/UseCases/IAirQDateImporter.cs`
- Modify: `airqmonitor/AirQMonitor/api/AirQService.cs:10-45`
- Modify: `airqmonitor/AirQMonitor/api/AirQMonitorServices.cs:14-43`
- Modify: `airqmonitor/AirQMonitor/api/MonitorApiEndpoints.cs:1-31`
- Modify: `airqmonitor/AirQMonitorTests/TestMonitorApiEndpoints.cs:12-35`

**Interfaces:**
- Consumes: `AirQApiKeyValidator.Create(string? configuredKey)` and `IsAuthorized(StringValues suppliedKeys)` from Task 1.
- Produces: `public interface IAirQDateImporter { void StoreNoiseLevelsForDate(string date); }`.
- Produces: `public sealed record StoreNoiseLevelsForDateRequest(string Date);` as the POST JSON body.
- Produces: `POST /store-noise-levels-for-date`, requiring `X-Api-Key`; `GET /liveness` remains public.

- [ ] **Step 1: Write failing route and handler tests**

Replace the route test with metadata and direct-handler tests. Register a mocked `IAirQDateImporter`, add `RVT__MONITOR_API_KEY` to the test configuration, and call the mapped request delegate with `DefaultHttpContext`.

```csharp
[TestMethod]
public void MapAirQMonitorApi_RejectsMissingApiKeyConfiguration()
{
    using var app = CreateApp(apiKey: null, new Mock<IAirQDateImporter>().Object);

    Assert.ThrowsException<InvalidOperationException>(() => app.MapAirQMonitorApi());
}

[TestMethod]
public void MapAirQMonitorApi_RegistersLivenessAndOnlyProtectedPostImportRoute()
{
    using var app = CreateApp("monitor-api-key", new Mock<IAirQDateImporter>().Object);
    app.MapAirQMonitorApi();

    var import = GetRoute(app, "/store-noise-levels-for-date");
    CollectionAssert.AreEquivalent(["POST"], import.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.ToList());
    Assert.IsNull(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
        .OfType<RouteEndpoint>().SingleOrDefault(endpoint => endpoint.RoutePattern.RawText == "/store-noise-levels-for-date" &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET")));
}

[TestMethod]
public async Task StoreNoiseLevelsForDate_ReturnsUnauthorizedWithoutMatchingApiKey()
{
    var importer = new Mock<IAirQDateImporter>(MockBehavior.Strict);
    var context = CreateHttpContext();

    var result = MonitorApiEndpoints.StoreNoiseLevelsForDate(
        new StoreNoiseLevelsForDateRequest("2026-07-14"), context.Request, importer.Object,
        AirQApiKeyValidator.Create("monitor-api-key"));

    await result.ExecuteAsync(context);
    Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    importer.VerifyNoOtherCalls();
}

[TestMethod]
public async Task StoreNoiseLevelsForDate_UsesOnlyTheRequestedDateAfterApiKeyValidation()
{
    var importer = new Mock<IAirQDateImporter>();
    var context = CreateHttpContext("monitor-api-key");

    var result = MonitorApiEndpoints.StoreNoiseLevelsForDate(
        new StoreNoiseLevelsForDateRequest("2026-07-14"), context.Request, importer.Object,
        AirQApiKeyValidator.Create("monitor-api-key"));

    await result.ExecuteAsync(context);
    Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
    importer.Verify(service => service.StoreNoiseLevelsForDate("2026-07-14"), Times.Once);
}
```

Use these test helpers in the same class so the tests create a real minimal-API service provider without a network listener:

```csharp
private static WebApplication CreateApp(string? apiKey, IAirQDateImporter importer)
{
    var builder = WebApplication.CreateBuilder();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["RVT__MONITOR_API_KEY"] = apiKey
    });
    builder.Services.AddSingleton(importer);
    return builder.Build();
}

private static DefaultHttpContext CreateHttpContext(string? apiKey = null)
{
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();
    if (apiKey is not null)
    {
        context.Request.Headers["X-Api-Key"] = apiKey;
    }

    return context;
}

private static RouteEndpoint GetRoute(WebApplication app, string path) =>
    ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
        .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == path);
```

- [ ] **Step 2: Run the endpoint tests to verify they fail**

Run: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --filter FullyQualifiedName~TestMonitorApiEndpoints`

Expected: compilation failure because the importer interface, request record, and static endpoint handler do not exist.

- [ ] **Step 3: Add the narrow importer port and make AirQService use configured vendor credentials**

Create `IAirQDateImporter.cs`:

```csharp
namespace AirQ.Api.UseCases;

public interface IAirQDateImporter
{
    void StoreNoiseLevelsForDate(string date);
}
```

Change `AirQService` to implement the interface and replace its public backfill method with:

```csharp
public sealed class AirQService : IAirQDateImporter
{
    public void StoreNoiseLevelsForDate(string date)
    {
        airQApi.StoreNoiseLevelsForDate(RvtConfig.USER_ID, RvtConfig.USER_AUTH, date);
    }
}
```

Keep the existing job methods unchanged. This is the only route from the HTTP endpoint to vendor credentials.

- [ ] **Step 4: Register the same singleton under the endpoint port**

Replace the final `AirQService` registration in `AddAirQMonitor` with a single concrete factory plus interface alias:

```csharp
services.AddSingleton(provider =>
{
    RvtLogger.CreateLogger(provider.GetRequiredService<ILoggerFactory>(), "AirQService");
    return new AirQService(provider.GetRequiredService<AirQApi>());
});
services.AddSingleton<IAirQDateImporter>(provider => provider.GetRequiredService<AirQService>());
```

Preserve the existing startup exception handling around the `AirQService` construction when moving it into the factory.

- [ ] **Step 5: Map the POST endpoint and fail closed for an absent key**

Replace the credential-bearing `MapGet` block with this app-local request record and handler:

```csharp
public sealed record StoreNoiseLevelsForDateRequest(string Date);

public static IResult StoreNoiseLevelsForDate(
    StoreNoiseLevelsForDateRequest request,
    HttpRequest httpRequest,
    IAirQDateImporter importer,
    AirQApiKeyValidator apiKeyValidator)
{
    if (!apiKeyValidator.IsAuthorized(httpRequest.Headers["X-Api-Key"]))
    {
        return Results.Unauthorized();
    }

    importer.StoreNoiseLevelsForDate(request.Date);
    return Results.Ok();
}
```

At the start of `MapAirQMonitorApi`, obtain the app configuration through `endpoints.ServiceProvider.GetRequiredService<IConfiguration>()`, resolve the key with:

```csharp
var apiKey = Environment.GetEnvironmentVariable("RVT__MONITOR_API_KEY")
    ?? configuration["RVT__MONITOR_API_KEY"];
var apiKeyValidator = AirQApiKeyValidator.Create(apiKey);
```

Then map:

```csharp
endpoints.MapPost("/store-noise-levels-for-date",
    (StoreNoiseLevelsForDateRequest request, HttpRequest httpRequest, IAirQDateImporter importer) =>
        StoreNoiseLevelsForDate(request, httpRequest, importer, apiKeyValidator));
```

This makes API startup fail before accepting requests when a key is missing, while keeping the validator immutable and outside the request body or URL.

- [ ] **Step 6: Run focused endpoint tests**

Run: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --filter FullyQualifiedName~TestMonitorApiEndpoints`

Expected: all endpoint tests pass, including `401` rejection without importer invocation and `200` dispatch with a valid key.

- [ ] **Step 7: Run the AirQ unit and PostgreSQL suite**

Run: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --logger "console;verbosity=minimal"`

Expected: all AirQ tests pass against the configured local PostgreSQL fixture.

- [ ] **Step 8: Commit the protected endpoint work**

```bash
git add airqmonitor/AirQMonitor/api/AirQService.cs \
  airqmonitor/AirQMonitor/api/AirQMonitorServices.cs \
  airqmonitor/AirQMonitor/api/MonitorApiEndpoints.cs \
  airqmonitor/AirQMonitor/api/UseCases/IAirQDateImporter.cs \
  airqmonitor/AirQMonitorTests/TestMonitorApiEndpoints.cs
git commit -m "fix: secure airq date import endpoint"
```

### Task 3: Restrict Compose Ingress and Document the Contract

**Files:**
- Modify: `docker-compose.yml:2-17`
- Modify: `README.md:100-135`
- Modify: `docs/container-builds.md:12-38`
- Modify: `project_state.md:1-20`

**Interfaces:**
- Consumes: `POST /store-noise-levels-for-date` with `X-Api-Key` and JSON `{ "date": "yyyy-MM-dd" }` from Task 2.
- Produces: base Compose deployment with no AirQ host port; AirQ remains reachable only by other Compose services at `http://airqmonitor-api:8080`.

- [ ] **Step 1: Write the failing Compose contract check**

Run this before changing Compose:

```bash
docker compose config | sed -n '/airqmonitor-api:/,/^[^ ]/p' | rg 'published: "8081"'
```

Expected: the command finds AirQ's currently published host port, demonstrating the insecure default exists.

- [ ] **Step 2: Remove AirQ host-port publishing**

Delete only the AirQ service's `ports` block:

```yaml
    ports:
      - "8081:8080"
```

Do not change other monitor port mappings in this task.

- [ ] **Step 3: Document configuration and explicit developer access**

In both operational documents, state:

```text
AirQ's import API is not published to the host by the base Compose file.
Set RVT__MONITOR_API_KEY through a secret mechanism before enabling MonitorApi.
Call POST /store-noise-levels-for-date with X-Api-Key and {"date":"YYYY-MM-DD"}.
For a temporary local host mapping, use an untracked override file with:
services:
  airqmonitor-api:
    ports: ["127.0.0.1:8081:8080"]
```

Do not place a key in `docker-compose.yml`, README examples, or tracked development settings. Add the endpoint/security decision and secret names, but no secret values, to the top of `project_state.md`.

- [ ] **Step 4: Verify the Compose topology and full build**

Run:

```bash
docker compose config
dotnet build rvt-monitors.sln --no-restore
git diff --check
```

Expected: Compose configuration is valid, `airqmonitor-api` has no published host port, the build has 0 warnings and 0 errors, and whitespace validation is clean.

- [ ] **Step 5: Commit ingress and documentation changes**

```bash
git add docker-compose.yml README.md docs/container-builds.md project_state.md
git commit -m "docs: restrict airq import ingress"
```

### Task 4: Final Security Regression Verification

**Files:**
- Verify only; no production source changes.

**Interfaces:**
- Consumes: all changes from Tasks 1-3.
- Produces: evidence that legacy credential-bearing GET access is absent and the protected POST contract is the only import control route.

- [ ] **Step 1: Run all relevant tests sequentially**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: all tests pass; AirQ's PostgreSQL tests use the local ignored fixture configuration or the explicit `RVT__POSTGRES_INTEGRATION_CONNECTION` environment variable.

- [ ] **Step 2: Scan route and Compose source for the retired attack path**

Run:

```bash
rg -n "MapGet\(\"/store-noise-levels-for-date|user_auth.*store-noise-levels-for-date|8081:8080" \
  airqmonitor/AirQMonitor docker-compose.yml
```

Expected: no matches.

- [ ] **Step 3: Inspect the final API contract**

Run:

```bash
rg -n "MapPost\(\"/store-noise-levels-for-date|X-Api-Key|RVT__MONITOR_API_KEY|RVT__AIRQ_USER_(ID|AUTH)" \
  airqmonitor/AirQMonitor rvt-monitor-common/Rvt.Monitor.Common
```

Expected: the POST route, API-key validation, and configuration-only vendor credentials are present; no values are printed.

- [ ] **Step 4: Commit any verification-only test adjustments**

```bash
git status --short
git diff --check
```

Expected: clean working tree. Do not create an empty commit.

## Plan Self-Review

- Spec coverage: Tasks 1-2 cover authenticated POST, fixed-time header validation, configuration-only vendor credentials, startup failure, and liveness access. Task 3 covers private Compose ingress and documentation. Task 4 proves the old GET path and host exposure are gone.
- Placeholder scan: no unfinished markers or deferred implementation instructions remain.
- Type consistency: `AirQApiKeyValidator`, `IAirQDateImporter`, `StoreNoiseLevelsForDateRequest`, and `StoreNoiseLevelsForDate` are defined before their consuming tasks and use the same names throughout.
