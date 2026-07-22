# Sonar Duplication Reduction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce SonarCloud duplicated-lines density for `aileron-forward_rvt-monitors`, with an immediate target of bringing new-code duplication below the 3% quality-gate threshold and a secondary target of materially reducing the current 24.2% overall duplication.

**Architecture:** Keep monitor-specific API/client logic in each monitor project, but move genuinely shared runtime infrastructure into `rvt-monitor-common/Rvt.Monitor.Common`. Preserve existing public namespaces such as `Rvt.Api`, `Rvt.Api.Comms`, `Rvt.Api.Mqtt`, `Rvt.Notification`, and `Rvt.Rules` during the first pass so callers do not need broad namespace rewrites.

**Tech Stack:** .NET 10, MSTest, SonarCloud, coverlet OpenCover, `Rvt.Monitor.Common`, MQTTnet, SendGrid, EF Core, PostgreSQL/SQL Server provider-aware tests.

---

## Current SonarCloud Duplication Snapshot

Latest checked values from SonarCloud:

| Metric | Value |
| --- | ---: |
| Overall duplicated lines density | 24.2% |
| Overall duplicated lines | 6,173 |
| Overall duplicated blocks | 307 |
| Files with duplication | 88 |
| New-code duplicated lines density | 26.1% |

Largest duplicated files:

| Duplicated lines | Blocks | Density | File |
| ---: | ---: | ---: | --- |
| 437 | 39 | 73.3% | `svantekmonitor/SvantekMonitor/api/SvantekApiRuleProcessing.cs` |
| 398 | 37 | 72.6% | `airqmonitor/AirQMonitor/api/AirQApiRuleProcessing.cs` |
| 222 | 11 | 31.9% | `svantekmonitor/SvantekMonitor/api/db/DBClient.cs` |
| 217 | 9 | 33.9% | `airqmonitor/AirQMonitor/api/db/DBClient.cs` |
| 182 | 8 | 93.3% | `svantekmonitor/SvantekMonitor/api/rvt-common/comms/MessageService.cs` |
| 176 | 4 | 93.1% | `myatmmonitor/MyAtmMonitor/api/rvt-common/comms/MessageService.cs` |
| 169 | 5 | 90.4% | `omnidotsmonitor/OmnidotsMonitor/api/rvt-common/comms/MessageService.cs` |
| 162 | 6 | 92.6% | `airqmonitor/AirQMonitor/api/rvt-common/comms/MessageService.cs` |
| 148 | 10 | 11.9% | `omnidotsmonitor/OmnidotsMonitor/api/db/DBUtil.cs` |
| 146 | 12 | 13.0% | `myatmmonitor/MyAtmMonitor/api/db/DBUtil.cs` |
| 141 | 8 | 14.3% | `svantekmonitor/SvantekMonitor/api/db/DBUtil.cs` |
| 140 | 8 | 15.8% | `airqmonitor/AirQMonitor/api/db/DBUtil.cs` |

## File Structure

Create or move shared runtime files into `rvt-monitor-common/Rvt.Monitor.Common`:

- `Configuration/RvtConfig.cs`: one superset runtime config class with current appsettings/environment fallback behavior.
- `LegacyRvtApi/AdapterException.cs`: moved from one monitor copy, namespace remains `Rvt.Api`.
- `LegacyRvtApi/RvtLogger.cs`: moved from one monitor copy, namespace remains `Rvt.Api`.
- `LegacyRvtApi/Comms/*.cs`: common email, SMS, message, and comms client code, namespace remains `Rvt.Api.Comms`.
- `LegacyRvtApi/Mqtt/*.cs`: common MQTT client/message code, namespaces remain `Rvt.Api.Mqtt` and `Rvt.Model.Mqtt`.
- `LegacyRvtApi/Rules/*.cs`: common alert DTOs/enums, namespace remains `Rvt.Rules`.
- `LegacyRvtApi/Notification/*.cs`: common contact/notification types, namespace remains `Rvt.Notification`.

Keep monitor-specific code in place:

- `airqmonitor/AirQMonitor/api/AirQApiRuleProcessing.cs`
- `svantekmonitor/SvantekMonitor/api/SvantekApiRuleProcessing.cs`
- `myatmmonitor/MyAtmMonitor/api/MyAtmApiRuleProcessing.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/*`
- monitor `DBClient.cs` implementations until EF parity is rechecked.

Delete only after replacement builds:

- `*/api/rvt-common/**/*.cs` from all four monitor projects.

## Task 1: Baseline And Guardrails

**Files:**
- Modify: `project_state.md`
- No production files.

- [ ] **Step 1: Confirm clean starting point**

Run:

```bash
git status --short
```

Expected output: no lines.

- [ ] **Step 2: Capture current duplication metrics**

Run:

```bash
curl -sS -u "$SONAR_TOKEN:" "https://sonarcloud.io/api/measures/component?component=aileron-forward_rvt-monitors&metricKeys=duplicated_lines_density,new_duplicated_lines_density,duplicated_lines,duplicated_blocks,duplicated_files"
```

Expected values at plan time:

```text
duplicated_lines_density=24.2
duplicated_lines=6173
duplicated_blocks=307
duplicated_files=88
new_duplicated_lines_density=26.1
```

- [ ] **Step 3: Run full local baseline tests**

Run:

```bash
dotnet test rvt-monitors.sln --no-restore
```

Expected output: all five test projects pass.

- [ ] **Step 4: Record baseline in `project_state.md`**

Add a dated section:

```markdown
## SonarCloud Duplication Reduction Baseline - 2026-07-03
- Starting duplication metrics: overall `24.2%`, new-code `26.1%`, duplicated lines `6173`, duplicated blocks `307`, duplicated files `88`.
- Highest-impact duplicated families are monitor rule processing, copied `api/rvt-common` runtime helpers, legacy `DBUtil`, monitor DB clients, and small DTO/model clones.
```

- [ ] **Step 5: Commit baseline note**

Run:

```bash
git add project_state.md
git commit -m "docs: record sonar duplication baseline"
```

## Task 2: Consolidate The Copied `api/rvt-common` Runtime Code

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Configuration/RvtConfig.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/AdapterException.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/RvtLogger.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/CommsClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/CommsException.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/EmailSender.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/ICommsClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/IMessageService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/MessageService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Comms/SmsSender.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Mqtt/IRvtMqttClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Mqtt/RvtMqttClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Mqtt/RvtMqttMessage.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Rules/AlertActivityTimeDto.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Rules/RvtAlertRuleDto.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Rules/RvtAlertType.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Notification/RvtContactDto.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/Notification/RvtNotificationDto.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Delete after build passes: `airqmonitor/AirQMonitor/api/rvt-common/**/*.cs`
- Delete after build passes: `myatmmonitor/MyAtmMonitor/api/rvt-common/**/*.cs`
- Delete after build passes: `omnidotsmonitor/OmnidotsMonitor/api/rvt-common/**/*.cs`
- Delete after build passes: `svantekmonitor/SvantekMonitor/api/rvt-common/**/*.cs`

- [ ] **Step 1: Add shared dependencies to common project**

Modify `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.9" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
<PackageReference Include="MQTTnet" Version="4.3.7.1207" />
<PackageReference Include="MQTTnet.Extensions.ManagedClient" Version="4.3.7.1207" />
<PackageReference Include="SendGrid" Version="9.29.3" />
```

- [ ] **Step 2: Create the shared superset `RvtConfig`**

Create `rvt-monitor-common/Rvt.Monitor.Common/Configuration/RvtConfig.cs` with namespace `Rvt.Api`. Preserve existing static property names. The class must include the common properties and all monitor-specific properties currently used by AirQ, MyAtm, Omnidots, and Svantek.

```csharp
using Microsoft.Extensions.Configuration;

namespace Rvt.Api;

public sealed class RvtConfig
{
    private RvtConfig() { }

    private static readonly IConfigurationRoot Configuration = BuildConfiguration();

    private static string GetSetting(string name, string defaultValue = "")
    {
        var environmentValue = Environment.GetEnvironmentVariable(name);
        return environmentValue ?? Configuration[name] ?? defaultValue;
    }

    private static bool GetBoolSetting(string name, bool defaultValue = false) =>
        bool.TryParse(GetSetting(name), out var value) ? value : defaultValue;

    private static int GetIntSetting(string name, int defaultValue) =>
        int.TryParse(GetSetting(name), out var value) ? value : defaultValue;

    private static IConfigurationRoot BuildConfiguration()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .Build();
    }

    public static readonly string SERVICE_NAME = GetSetting("RVT__SERVICE_NAME", "RVT monitor data collector running ");
    public static readonly string SERVICE_VERSION = GetSetting("RVT__SERVICE_VERSION", "v0.1.0");
    public static readonly bool MQTT_ENABLED = GetBoolSetting("RVT__MQTT_ENABLED");
    public static readonly string MQTT_HOSTNAME = GetSetting("RVT__MQTT_HOSTNAME", "rvt-mqtt-namespace.westeurope-1.ts.eventgrid.azure.net");
    public static readonly string MQTT_CLIENT_ID = GetSetting("RVT__MQTT_CLIENT_ID", "client1-session1");
    public static readonly string MQTT_USERNAME = GetSetting("RVT__MQTT_USERNAME", "client1-authn-ID");
    public static readonly string MQTT_CERTIFICATE_PATH = GetSetting("RVT__MQTT_CERTIFICATE_PATH");
    public static readonly string MQTT_PRIVATE_KEY_PATH = GetSetting("RVT__MQTT_PRIVATE_KEY_PATH");
    public static readonly bool SMS_ENABLED = GetBoolSetting("RVT__SMS_ENABLED");
    public static readonly bool EMAIL_ENABLED = GetBoolSetting("RVT__EMAIL_ENABLED");
    public static readonly string DB_CONNECTION_STRING = GetSetting("ConnectionStrings__DefaultConnection");
    public static readonly string DATABASE_PROVIDER = GetSetting("RVT__DATABASE_PROVIDER", "PostgreSql");
    public static readonly bool TESTLOCAL = GetBoolSetting("testlocal");
    public static readonly string PORTAL_BASE_URL = GetSetting("RVT__PORTAL_BASE_URL", "https://www.rvtcloud.com/");
    public static readonly string BASE_URL = GetSetting("RVT__BASE_URL");
    public static readonly string LOCAL_TIME_ZONE = GetSetting("RVT__LOCAL_TIME_ZONE", "GMT Standard Time");
    public static readonly string EMAIL_ALERT_FROM_EMAIL = GetSetting("RVT__EMAIL_ALERT_FROM_EMAIL", "NoReply@rvtgroup.co.uk");
    public static readonly string EMAIL_ALERT_FROM_NAME = GetSetting("RVT__EMAIL_ALERT_FROM_NAME", "RVT Cloud");
    public static readonly string SENDGRID_API_KEY = GetSetting("RVT__SENDGRID_API_KEY");
    public static readonly string SMS_API_SECRET = GetSetting("RVT__SMS_API_SECRET");
    public static readonly string SMS_API_KEY = GetSetting("RVT__SMS_API_KEY");
    public static readonly string SMS_SENDER = GetSetting("RVT__SMS_SENDER", "KrakenAlert");
    public static readonly string INSERT_TOPIC = GetSetting("RVT__INSERT_TOPIC");
    public static readonly string ALERT_TOPIC = GetSetting("RVT__ALERT_TOPIC");
    public static readonly string SENT_OK = "Sent ok";
    public static readonly string OFFLINE_RULE = "offline-rule";

    public static readonly string USER_ID = GetSetting("RVT__AIRQ_USER_ID", GetSetting("RVT__OMNIDOTS_USER_ID"));
    public static readonly string USER_AUTH = GetSetting("RVT__AIRQ_USER_AUTH", GetSetting("RVT__OMNIDOTS_USER_AUTH"));
    public static readonly string TOKEN = GetSetting("RVT__MYATM_TOKEN", GetSetting("RVT__OMNIDOTS_TOKEN"));
    public static readonly bool USE_TOKEN = GetBoolSetting("RVT__OMNIDOTS_USE_TOKEN", true);
    public static readonly string WEBHOOK_URL = GetSetting("RVT__OMNIDOTS_WEBHOOK_URL");
    public static readonly string WEBHOOK_SECRET = GetSetting("RVT__OMNIDOTS_WEBHOOK_SECRET");
    public static readonly string SIGNATURE_HEADER = "x-omnidots-notifier-signature";
    public static readonly string APP_ID = "omnidots-adapter";
    public static readonly string CONFIG_SECRET = GetSetting("RVT__OMNIDOTS_CONFIG_SECRET");
    public static readonly int NOTIFICATION_DELAY_MINUTES = GetIntSetting("RVT__NOTIFICATION_DELAY_MINUTES", 5);
    public static readonly string UNKNOWN = "Unknown";
    public static readonly string UNSPECIFIED = "unspecified";
    public static readonly string ON = "On";
    public static readonly string OFF = "Off";
    public static readonly string MSG_SENSOR_ONLINE = "sensor_went_online";
    public static readonly string MSG_SENSOR_GUIDELINE_ALARM = "sensor_guide_line_alarm";
    public static readonly string MSG_SENSOR_OFFLINE = "sensor_measurement_stopped_clipping";

    public static readonly string API_KEY = GetSetting("RVT__SVANTEK_API_KEY");
    public static readonly string BlobConnectionString = GetSetting("RVT__BLOB_CONNECTION_STRING");
    public static readonly string BlobServiceUri = GetSetting("RVT__BLOB_SERVICE_URI");
    public static readonly string AudioFolder = GetSetting("RVT__AUDIO_FOLDER", "audiofiles");
    public const string API_URL_PROJECTS_GET_DATA = "projects-get-data.php";
    public const string API_URL_STATIONS_GET_LIST = "stations-get-list.php";
    public const string API_URL_PROJECTS_GET_RESULT_LIST = "projects-get-result-list.php";
    public const string API_URL_PROJECTS_GET_RESULT_DATA_MULTI = "projects-get-result-data-multi-point.php";
}
```

- [ ] **Step 3: Set per-monitor topic and service defaults in appsettings**

Add these values to each monitor `appsettings.json` so the shared `RvtConfig` keeps monitor-specific topic behavior without source duplication:

AirQ:

```json
{
  "RVT__SERVICE_NAME": "AirQMonitor noise monitor data collector running ",
  "RVT__BASE_URL": "https://datacollector.airqweb.com",
  "RVT__INSERT_TOPIC": "rvt/noise/inserted",
  "RVT__ALERT_TOPIC": "rvt/noise/alerted"
}
```

MyAtm:

```json
{
  "RVT__SERVICE_NAME": "MyAtmMonitor dust monitor data collector running ",
  "RVT__BASE_URL": "https://api.my-atmosphere.cloud/api/",
  "RVT__INSERT_TOPIC": "rvt/dust/inserted",
  "RVT__ALERT_TOPIC": "rvt/dust/alerted"
}
```

Omnidots:

```json
{
  "RVT__SERVICE_NAME": "OmnidotsMonitor vibration monitor data collector running ",
  "RVT__BASE_URL": "https://honeycomb.omnidots.com",
  "RVT__INSERT_TOPIC": "rvt/vibration/inserted",
  "RVT__ALERT_TOPIC": "rvt/vibration/alerted"
}
```

Svantek:

```json
{
  "RVT__SERVICE_NAME": "SvantekMonitor noise monitor data collector running ",
  "RVT__BASE_URL": "https://svannet.com/api/v2.3/",
  "RVT__INSERT_TOPIC": "rvt/noise/inserted",
  "RVT__ALERT_TOPIC": "rvt/noise/alerted"
}
```

Merge these keys into existing JSON objects rather than replacing existing appsettings.

- [ ] **Step 4: Move one copy of each duplicated common file**

Use the AirQ copy for `AdapterException`, `RvtLogger`, `Comms`, `Mqtt`, `Rules`, and `Notification` files unless another copy has newer behavior. Preserve namespaces and public type names exactly.

- [ ] **Step 5: Delete monitor-local common copies**

After the common project builds, delete:

```text
airqmonitor/AirQMonitor/api/rvt-common
myatmmonitor/MyAtmMonitor/api/rvt-common
omnidotsmonitor/OmnidotsMonitor/api/rvt-common
svantekmonitor/SvantekMonitor/api/rvt-common
```

- [ ] **Step 6: Build and test**

Run:

```bash
dotnet build rvt-monitors.sln
dotnet test rvt-monitors.sln --no-build
```

Expected output: build succeeds and all tests pass.

- [ ] **Step 7: Run SonarCloud with coverage**

Run:

```bash
SONAR_HOST_URL="https://sonarcloud.io" \
SONAR_ORGANIZATION="aileron-forward" \
SONAR_PROJECT_KEY="aileron-forward_rvt-monitors" \
SONAR_PROJECT_NAME="RVT Monitors" \
RUN_TESTS=true \
scripts/run-sonarqube-analysis.sh
```

Expected result: duplicated lines decrease by at least 1,400 lines because four copied common trees have become one compiled shared source.

- [ ] **Step 8: Commit**

Run:

```bash
git add .
git commit -m "refactor: consolidate shared monitor runtime helpers"
```

## Task 3: Extract Shared Rule-Processing Flow For Noise Monitors

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Rules/NoiseAlertRuleProcessor.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Rules/NoiseAlertRuleEvaluation.cs`
- Modify: `airqmonitor/AirQMonitor/api/AirQApiRuleProcessing.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekApiRuleProcessing.cs`
- Test: `airqmonitor/AirQMonitorTests/TestAirQApiRuleProcessing.cs`
- Test: `svantekmonitor/SvantekMonitorTests/TestSvantekApiRuleProcessing.cs`

- [ ] **Step 1: Add shared rule evaluation tests**

Create tests that prove the common behavior before deleting duplicated branches:

```csharp
[TestClass]
public sealed class NoiseAlertRuleProcessorTests
{
    [TestMethod]
    public void ShouldActivateAlertWhenLevelExceedsLimitOn()
    {
        var rule = new RvtAlertRuleDto
        {
            Field = "LAeq",
            LimitOn = 70,
            LimitOff = 65,
            AlertType = AlertType.Alert,
            IsActive = false,
            IsDeleted = false,
            AveragingPeriod = 900,
            RuleActiveTime = AlertActivityTimeDto.AlwaysActive()
        };

        var result = NoiseAlertRuleProcessor.EvaluateRule(rule, level: 71, previousAlert: AlertType.Ignore);

        Assert.IsTrue(result.ShouldNotify);
        Assert.IsTrue(result.ShouldSetActive);
        Assert.AreEqual(AlertType.Alert, result.NextPreviousAlert);
    }

    [TestMethod]
    public void ShouldDeactivateActiveRuleWhenLevelFallsBelowLimitOff()
    {
        var rule = new RvtAlertRuleDto
        {
            Field = "LAeq",
            LimitOn = 70,
            LimitOff = 65,
            AlertType = AlertType.Alert,
            IsActive = true,
            IsDeleted = false,
            AveragingPeriod = 900,
            RuleActiveTime = AlertActivityTimeDto.AlwaysActive()
        };

        var result = NoiseAlertRuleProcessor.EvaluateRule(rule, level: 64, previousAlert: AlertType.Ignore);

        Assert.IsFalse(result.ShouldNotify);
        Assert.IsTrue(result.ShouldSetInactive);
    }
}
```

- [ ] **Step 2: Implement `NoiseAlertRuleEvaluation`**

Create:

```csharp
namespace Rvt.Monitor.Common.Rules;

public sealed record NoiseAlertRuleEvaluation(
    bool ShouldNotify,
    bool ShouldSetActive,
    bool ShouldSetInactive,
    AlertType NextPreviousAlert,
    string Reason);
```

- [ ] **Step 3: Implement shared rule transition logic**

Create:

```csharp
namespace Rvt.Monitor.Common.Rules;

public static class NoiseAlertRuleProcessor
{
    public static NoiseAlertRuleEvaluation EvaluateRule(
        RvtAlertRuleDto rule,
        double level,
        AlertType previousAlert)
    {
        if (rule.IsDeleted)
        {
            return rule.IsActive
                ? new NoiseAlertRuleEvaluation(false, false, true, previousAlert, "deleted")
                : new NoiseAlertRuleEvaluation(false, false, false, previousAlert, "deleted");
        }

        if (level >= rule.LimitOn)
        {
            if (rule.IsActive)
            {
                return new NoiseAlertRuleEvaluation(false, false, false, rule.AlertType, "already-active");
            }

            if (rule.AlertType == AlertType.Alert || previousAlert != AlertType.Alert)
            {
                return new NoiseAlertRuleEvaluation(true, true, false, rule.AlertType, "limit-on");
            }

            return new NoiseAlertRuleEvaluation(false, false, false, previousAlert, "alert-already-sent");
        }

        if (level <= rule.LimitOff && rule.IsActive)
        {
            return new NoiseAlertRuleEvaluation(false, false, true, previousAlert, "limit-off");
        }

        return rule.IsActive
            ? new NoiseAlertRuleEvaluation(false, false, false, rule.AlertType, "inside-active-band")
            : new NoiseAlertRuleEvaluation(false, false, false, previousAlert, "inside-inactive-band");
    }
}
```

- [ ] **Step 4: Refactor AirQ and Svantek to call the shared evaluator**

Keep provider-specific parts local:

- AirQ level extraction from `NoiseDto`.
- Svantek level lookup through `dbClient.GetAverageNoiseLevel`.
- `ProcessAlertForContacts*` call signatures.
- MQTT message topic and payload construction.

Replace the repeated active/inactive/notify branching with calls to `NoiseAlertRuleProcessor.EvaluateRule`.

- [ ] **Step 5: Test rule behavior**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore
```

Expected output: all three test projects pass.

- [ ] **Step 6: Commit**

Run:

```bash
git add .
git commit -m "refactor: share noise alert rule evaluation"
```

## Task 4: Remove Or Exclude Legacy `DBUtil` Duplication

**Files:**
- Modify or delete: `airqmonitor/AirQMonitor/api/db/DBUtil.cs`
- Modify or delete: `myatmmonitor/MyAtmMonitor/api/db/DBUtil.cs`
- Modify or delete: `omnidotsmonitor/OmnidotsMonitor/api/db/DBUtil.cs`
- Modify or delete: `svantekmonitor/SvantekMonitor/api/db/DBUtil.cs`
- Modify: `scripts/run-sonarqube-analysis.sh` only if temporary exclusions are chosen.

- [ ] **Step 1: Find live `DBUtil` call sites**

Run:

```bash
rg "DBUtil\\." airqmonitor myatmmonitor omnidotsmonitor svantekmonitor
```

Expected decision:

- If a monitor has no `DBUtil` call sites, delete that monitor's `DBUtil.cs`.
- If a monitor still has live call sites, migrate those specific paths to EF-backed `DBClient` first.

- [ ] **Step 2: Prefer deletion over exclusion**

For each `DBUtil.cs` deleted, run that monitor's tests:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore
```

- [ ] **Step 3: Use a time-boxed Sonar exclusion only for still-live legacy files**

If `DBUtil` cannot be deleted because it still guards a production path, add this argument in `scripts/run-sonarqube-analysis.sh` with a comment that links it to EF parity removal:

```bash
"/d:sonar.cpd.exclusions=**/api/db/DBUtil.cs"
```

This is acceptable only as a temporary quality-gate unblocker; the preferred end state is deleting `DBUtil.cs`.

- [ ] **Step 4: Commit**

Run:

```bash
git add .
git commit -m "refactor: reduce legacy db utility duplication"
```

## Task 5: Share Monitor Host Bootstrap

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Hosting/MonitorHost.cs`
- Modify: `airqmonitor/AirQMonitor/Program.cs`
- Modify: `myatmmonitor/MyAtmMonitor/Program.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/Program.cs`
- Modify: `svantekmonitor/SvantekMonitor/Program.cs`

- [ ] **Step 1: Create shared host helper**

Create:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.Common.Hosting;

public static class MonitorHost
{
    public static IConfiguration BuildConfiguration(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }

    public static async Task<int> RunAsync<TDispatcher>(
        string[] args,
        string monitorName,
        Func<IConfiguration, Task<int?>> runJobIfRequested,
        Action<IServiceCollection, IConfiguration> configureScheduler,
        Func<WebApplication, Task> mapApi,
        Action<ILoggingBuilder>? configureLogging = null)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        var configuration = BuildConfiguration(args);
        var jobResult = await runJobIfRequested(configuration);
        if (jobResult.HasValue)
        {
            return jobResult.Value;
        }

        var apiEnabled = configuration.GetValue<bool>("MonitorApi:Enabled");
        var schedulerEnabled = MonitorInfrastructureOptions.IsQuartzSchedulerEnabled(configuration);

        if (apiEnabled)
        {
            var apiBuilder = WebApplication.CreateBuilder(args);
            apiBuilder.Configuration.AddConfiguration(configuration);
            configureLogging?.Invoke(apiBuilder.Logging);

            if (schedulerEnabled)
            {
                apiBuilder.Services.AddMonitorQuartzScheduler<TDispatcher>(apiBuilder.Configuration, monitorName);
            }

            var app = apiBuilder.Build();
            await mapApi(app);
            await app.RunAsync();
            return 0;
        }

        if (schedulerEnabled)
        {
            var schedulerHost = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
                .ConfigureServices((context, services) =>
                {
                    services.AddMonitorQuartzScheduler<TDispatcher>(context.Configuration, monitorName);
                    configureScheduler(services, context.Configuration);
                })
                .ConfigureLogging(logging => configureLogging?.Invoke(logging))
                .Build();

            await schedulerHost.RunAsync();
            return 0;
        }

        await Console.Error.WriteLineAsync("No monitor execution mode configured. Set MonitorApi:Enabled=true, MonitorScheduler:Enabled=true, or pass --job <name>.");
        return 2;
    }
}
```

- [ ] **Step 2: Replace duplicated `Program.cs` bodies with small wrappers**

Each monitor `Program.cs` should keep only:

- monitor-specific `using` directives.
- `MonitorJobRunner.GetJobName` and `MonitorJobRunner.RunAsync` wrapper.
- API map call such as `app.MapAirQMonitorApi()`.
- optional logging override for Svantek.

- [ ] **Step 3: Build and commit**

Run:

```bash
dotnet build rvt-monitors.sln
dotnet test rvt-monitors.sln --no-build
git add .
git commit -m "refactor: share monitor host bootstrap"
```

## Task 6: Recheck Sonar And Decide Whether DTO Duplication Is Worth Changing

**Files:**
- Modify: `project_state.md`
- Optional create: `rvt-monitor-common/Rvt.Monitor.Common/Models/SiteInfoDto.cs`
- Optional create: `rvt-monitor-common/Rvt.Monitor.Common/Models/SampleResponse.cs`
- Optional create: `rvt-monitor-common/Rvt.Monitor.Common/Models/ErrorResponse.cs`

- [ ] **Step 1: Run final SonarCloud analysis with coverage**

Run:

```bash
SONAR_HOST_URL="https://sonarcloud.io" \
SONAR_ORGANIZATION="aileron-forward" \
SONAR_PROJECT_KEY="aileron-forward_rvt-monitors" \
SONAR_PROJECT_NAME="RVT Monitors" \
RUN_TESTS=true \
scripts/run-sonarqube-analysis.sh
```

- [ ] **Step 2: Query final duplication metrics**

Run:

```bash
curl -sS -u "$SONAR_TOKEN:" "https://sonarcloud.io/api/measures/component?component=aileron-forward_rvt-monitors&metricKeys=duplicated_lines_density,new_duplicated_lines_density,duplicated_lines,duplicated_blocks,duplicated_files"
```

- [ ] **Step 3: Decide on DTO/model consolidation**

Only move DTO/model clones if either condition is true:

- `new_duplicated_lines_density` remains above 3%.
- the remaining duplicated files are maintained manually and are not just provider response shapes.

Do not refactor JSON response classes that mirror external API payloads unless tests prove serialization names and nullability stay unchanged.

- [ ] **Step 4: Record result**

Add to `project_state.md`:

```markdown
## SonarCloud Duplication Reduction Result - 2026-07-03
- Final duplicated-lines density: record the `duplicated_lines_density` value returned by the Step 2 SonarCloud API query.
- Final new-code duplicated-lines density: record the `new_duplicated_lines_density` period value returned by the Step 2 SonarCloud API query.
- Final duplicated lines: record the `duplicated_lines` value returned by the Step 2 SonarCloud API query.
- Refactors completed: shared rvt-common runtime, shared noise alert rule evaluation, DBUtil deletion/exclusion decision, shared monitor host bootstrap.
- Remaining duplication is classified as either external API DTO shape duplication, provider-specific DB mapping duplication, or intentional monitor-specific implementation that is not safe to consolidate in this pass.
```

- [ ] **Step 5: Commit final notes**

Run:

```bash
git add project_state.md
git commit -m "docs: record sonar duplication reduction results"
```

## Expected Impact

The copied `api/rvt-common` consolidation should remove the largest low-risk family of duplication and likely cut more than 1,400 duplicated lines. Rule-processing extraction targets the two largest files directly and should cut another several hundred duplicated lines while also reducing cognitive complexity. DBUtil deletion is the biggest cleanup if EF parity allows it; otherwise a time-boxed CPD exclusion can unblock the quality gate while the EF migration finishes. Program bootstrap extraction is low risk and should remove about 200 duplicated lines.

The realistic first implementation pass should make the new-code duplication gate pass. Overall duplication may still remain above 3% because this repository has four related monitor apps with intentionally similar external API DTOs and provider-specific data access.
