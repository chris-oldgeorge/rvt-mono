# RVT Common Monitor RC Validation

## Scope and source identity

- Validation date: 2026-07-16.
- Monitor repository: `RVT-Group-LTD/rvt-monitors`.
- Package-consumer implementation commit: `b796b21c33a80531bfa53c299e6822c01b94a397`.
- Validation-command correction commit: `7b6a3be9c482f6c80972b82afd1f7a32bdbc8dbd`.
- Package conversion commit: `069e3cfe33be44704d2762b4ebec0b290a3f2404`.
- Pre-conversion rollback commit: `924ed20deee37fba17452612ae40eae8e0fe6168`.
- Authoritative Common repository: `RVT-Group-LTD/rvt-reporting`.
- Published RC source commit: `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
- SDK: .NET SDK `10.0.203`.

The monitor source directory remains present during the rollback window. Consumer projects resolve the private packages; the retained source projects are not consumer references. Commit `7b6a3be9c482f6c80972b82afd1f7a32bdbc8dbd` changes validation commands and their plan only; the runtime source and validated images remain based on `b796b21c33a80531bfa53c299e6822c01b94a397`.

## Package resolution

All 14 consumer lock files restored in locked mode. The five runtime hosts resolve Common and Infrastructure directly. The five monitor test projects resolve IntegrationTesting directly with private assets; ReportingMonitorTests also has an explicit direct Common reference. Reporting Messaging and Storage resolve Common directly.

| Package | Resolved version | NuGet lock content hash |
| --- | --- | --- |
| `Rvt.Monitor.Common` | `0.2.0-rc.1` | `+JAnUTKwFD07+rTwl1eHePz3HVltf5FNvHMN/bJc0uZYxfVBox1taYwF1uANaVYKG8agZjnhzDmgEDoj/vQTTw==` |
| `Rvt.Monitor.Common.Infrastructure` | `0.2.0-rc.1` | `PscR8XS7pIXoiGUlCJWmyYPhkWZa/jwH1Xv6489Wbv4uy6R5V4deguTJihmZRnoy5HFJU+RUVuv0ftCUAFUPPQ==` |
| `Rvt.Monitor.IntegrationTesting` | `0.2.0-rc.1` | `0p35dT+xJv8tbfsPlglwbhtxqM2H89Vp/wBJzF+XKjueE/2tGIMZlPWDW0kvVZ/S4dlG4wPr4OjbP1rAeBCanA==` |

An authenticated locked replay using a clean temporary NuGet cache restored all 20 solution projects. Authentication was supplied only to the restore process; no credential value was written to the cache or repository.

## Formatter, build, and PostgreSQL-backed tests

The validation used a disposable PostgreSQL 17 container. The database setting was supplied only to the test process and is not recorded here.

| Check | Result |
| --- | --- |
| `dotnet format rvt-monitors.sln --verify-no-changes --no-restore` | Passed |
| `dotnet build rvt-monitors.sln --no-restore --nologo` | Passed in 4.91 seconds; 0 warnings, 0 errors |
| `dotnet test rvt-monitors.sln --no-build --nologo` | Passed; 1,410 passed, 0 failed, 0 skipped |

Validation found that .NET SDK 10.0.203 does not accept `--nologo` for `dotnet format`. The executable package-consumer workflow and its plan example were corrected to use the passing formatter command shown above.

Suite counts were:

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| IntegrationTesting | 6 | 0 | 0 |
| Common Infrastructure | 64 | 0 | 0 |
| Common | 426 | 0 | 0 |
| AirQ | 118 | 0 | 0 |
| MyATM | 198 | 0 | 0 |
| Omnidots | 399 | 0 | 0 |
| Svantek | 124 | 0 | 0 |
| Reporting | 75 | 0 | 0 |

The Task 1 behavior suites remain green. The total is four tests higher than the 1,406-test extraction baseline because the package migration adds consumer-boundary coverage.

## Container inventory

All five Compose images built through the BuildKit `nuget_credentials` secret mount. `scripts/report-rvt-package-inventory.sh` inspected the published `.deps.json` in each image and passed its required-runtime-package, synchronized-version, and test-package-exclusion checks.

| Image | Local image ID | Common | Infrastructure | IntegrationTesting in runtime |
| --- | --- | --- | --- | --- |
| `rvt/airqmonitor:local` | `0e3933773aeabfb3786397afd536e251e7f9d8482cf3e0814324c5d78437b2fb` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/myatmmonitor:local` | `909a629a800ce1c60fee2c1e962873009bb98a890c2cee0e7ca072bd36758bea` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/omnidotsmonitor:local` | `7ab0dcb2a18f9b583c421e5f0dcb7087e4e523c79ce1676285a2001950fadedb` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/svantekmonitor:local` | `ce30a1779609815f33c0ce8b3970dd6ba70b7b12c44062c9386663a7efb655f6` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/reportingmonitor:local` | `23119a3036a2e92e85850b4bd55c8890454a05e656708b053f8d4de8dff31d4f` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |

These are local content-addressed image IDs. No registry digest was produced by the local Compose build.

## Staging-equivalent checks

`docker compose config --quiet` passed with a non-secret process-local placeholder for the required build secret. The five package-only images then passed these staging-equivalent checks with untracked validation-only configuration:

| Check | Result |
| --- | --- |
| API startup | AirQ, MyATM, Omnidots, Svantek, and Reporting started |
| Liveness | HTTP 200 from all five hosts |
| Readiness | HTTP 200 from MyATM and Reporting, the two hosts that expose `/readiness` |
| Storage configuration | Reporting started with its default Local storage; the full green Reporting suite covers storage behavior |
| Provider-disabled communications | Email and SMS remained disabled; no provider call was made |
| Quartz startup | Five scheduler-mode containers stayed running; each log showed Quartz 3.18.1 initialize and start |
| Quartz job execution | All validation triggers were disabled; no vendor job was invoked |
| One-shot dispatcher | An intentional unknown job on each image logged monitor-job startup, rejected the host-specific unknown job, and exited 2 as expected |

`AIRQ_TESTLOCAL_SERIAL_ID=validation-only scripts/run-testlocal-suite.sh --dry-run` passed and enumerated eight bounded jobs: one AirQ job, two MyATM jobs, three Omnidots jobs, and two Svantek jobs. The testlocal suite was deliberately not executed because no live vendor credentials were introduced. The dry run and the other staging-equivalent checks made no live vendor, email, or SMS calls.

## Rollback checkpoint

The conversion commit resolves to `069e3cfe33be44704d2762b4ebec0b290a3f2404`; its parent, `924ed20deee37fba17452612ae40eae8e0fe6168`, is the source-reference rollback candidate.

An isolated worktree at the rollback commit reproduced the plan's no-restore-first limitation: the first build stopped only with `NETSDK1004` because the new worktree had no assets files. After a clean restore, the source-reference solution built in 5.40 seconds with 0 warnings and 0 errors. This proves the retained pre-conversion source graph is buildable when rollback includes its required restore step.

## Credential and secret handling

- `NuGet.config` contains source mapping and no package-source credentials.
- Container restores receive the NuGet credential through a BuildKit secret environment mount, not a Dockerfile `ARG` or `ENV`.
- The locked replay received authentication only in its process environment.
- Targeted Task 10 history and environment-name scans found no stored credential value.
- This evidence contains no token, connection string, destination, provider response, or staging secret.

## Cleanup

All ten disposable API and scheduler containers and the disposable PostgreSQL container were removed. The untracked scheduler validation file was deleted. The isolated rollback worktree at `/private/tmp/rvt-monitors-source-rollback` was removed after its build result was captured.
