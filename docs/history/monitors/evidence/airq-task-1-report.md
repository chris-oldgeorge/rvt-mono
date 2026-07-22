# AirQ Task 1 Report

## Scope

Implemented the AirQ API-key validator and its focused tests only. No endpoint, service, Compose, documentation, or plan files were changed.

## Implementation

- Added `airqmonitor/AirQMonitor/api/Security/AirQApiKeyValidator.cs`.
- Added `airqmonitor/AirQMonitorTests/Security/AirQApiKeyValidatorTests.cs`.
- `Create` rejects null, empty, and whitespace-only configuration with `InvalidOperationException`.
- `IsAuthorized` accepts exactly one nonblank supplied header value and compares UTF-8 bytes with `CryptographicOperations.FixedTimeEquals`.

## TDD Evidence

1. Added the prescribed tests before the production validator.
2. Ran the focused test command and observed the expected missing-type compilation failure.
3. Added the minimal validator implementation.
4. The supplied test snippet used `Assert.ThrowsException`, which is unavailable in this repository's MSTest 4 API. Replaced it with the equivalent `Assert.Throws` assertion; behavior and coverage are unchanged.

## Verification

- Focused: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --filter FullyQualifiedName~AirQApiKeyValidatorTests`
  - Passed: 2; Failed: 0; Skipped: 0.
- Full AirQ suite: `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore`
  - Passed: 106; Failed: 0; Skipped: 0.
- `git diff --check`: passed.

## Concerns

Both test runs emitted the repository's existing warning that `.sonarqube/bin/targets/SonarQube.Integration.targets` is missing. This did not prevent the build or affect test outcomes.
