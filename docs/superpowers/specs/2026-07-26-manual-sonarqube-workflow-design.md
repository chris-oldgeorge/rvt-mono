# Manual SonarQube Cloud Workflow Design

Date: 2026-07-26

Status: Approved for implementation.

## Objective

Provide an on-demand GitHub Actions workflow that analyses the complete RVT
monorepo in SonarQube Cloud. The workflow must never run automatically for a
push, pull request, tag, or schedule.

## SonarQube Cloud Identity

- Host: `https://sonarcloud.io`
- Organization: `aileron-forward`
- Project key: `aileron-forward_rvt-mono`
- Authentication: GitHub Actions repository secret `SONAR_TOKEN`

The token is passed to scanner processes through the environment and is never
stored in the repository or printed deliberately.

## Workflow Boundary

Add one root workflow at `.github/workflows/sonarqube.yml`. Its only event is
`workflow_dispatch`, so an operator starts analysis from GitHub Actions and
selects the Git ref to analyse.

The workflow has read-only repository permissions. A concurrency group scoped
to the selected ref prevents two manual analyses of the same ref from
overlapping; an existing run is not cancelled by a later request.

Nested legacy workflows under imported application directories are not reused.
They target pre-monorepo projects and are not root GitHub Actions entry points.

## Runner and Database

The job runs on a dedicated containerized self-hosted runner on the ARM64
development machine. GitHub routes the job with:

```yaml
runs-on: [self-hosted, linux, ARM64, rvt-sonar]
```

Docker Compose owns two containers on one private network:

- `rvt-sonar-runner`: a Linux ARM64 GitHub Actions runner with the custom
  `rvt-sonar` label; and
- `rvt-sonar-db`: `timescale/timescaledb:2.28.3-pg17`.

The runner container does not mount the development machine's source tree or
Docker socket. It receives no inbound host port. The runner state is stored in
a Docker named volume; the checked-out work directory remains inside the
container and is cleaned by the checkout action between trusted manual jobs.

The runner image pins GitHub Actions Runner `2.334.0` and verifies the Linux
ARM64 archive with SHA-256
`f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4`.
It runs the listener as a non-root user. GitHub runner auto-updates remain
enabled so GitHub does not stop scheduling jobs when the pinned bootstrap
version ages out.

The first start accepts a short-lived repository runner registration token
through `RUNNER_REGISTRATION_TOKEN`. The token is never committed. It is only
needed while initially registering or replacing the persisted runner state;
no long-lived GitHub personal access token is stored in the container.

The database container exposes port 5432 only on the private Compose network
and creates database `rvt_sonar_ci` with the job-local `postgres` superuser and
password `postgres`. Both live-test contracts point to host `rvt-sonar-db`:

- `RVT_TEST_POSTGRES_CONNECTION` for Portal tests; and
- `RVT__POSTGRES_INTEGRATION_CONNECTION` for Common and monitor tests.

The runner waits for the database health check and explicitly ensures the
TimescaleDB and `pgcrypto` extensions exist before analysis.

Docker Desktop must be running and the development machine must be awake for
the runner to accept work. Jobs remain queued while the runner is offline. The
runner is approved only for this private repository and manually selected,
trusted refs.

Within the job, pinned GitHub setup actions configure:

- .NET SDK 10;
- JDK 17 for the SonarScanner;
- Node.js 24 with the Portal client package-lock cache; and
- job-local SonarScanner for .NET `11.2.1` and `dotnet-coverage` `18.9.0`
  installations.

The workflow checks out full Git history for correct SonarQube Cloud
attribution.

## Analysis Sequence

The job executes these stages in order:

1. Verify the PostgreSQL-only repository boundary.
2. Install the pinned scanner and coverage tools under a job-local directory.
3. Begin analysis for `aileron-forward_rvt-mono`.
4. Restore `Rvt.Mono.slnx` serially.
5. Build the complete solution in Release mode, without incremental
   compilation and with single-node MSBuild ordering.
6. Run every .NET test project under `dotnet-coverage`, producing one Visual
   Studio coverage XML report.
7. Install Portal client dependencies with `npm ci`.
8. Run the Portal Vitest coverage command, producing LCOV coverage.
9. End analysis and upload the results to SonarQube Cloud.
10. Wait for the project's quality-gate result and fail the manual run if the
    gate fails or times out.

Browser end-to-end tests are outside this workflow. They require a deployed
application/browser topology and do not contribute the unit/integration
coverage reports imported by this scan.

## Scanner Configuration

The scanner imports:

- .NET coverage through `sonar.cs.vscoveragexml.reportsPaths`; and
- Portal TypeScript coverage through `sonar.javascript.lcov.reportPaths`.

Generated database migrations, generated EF models, generated API schema,
build output, coverage output, client distribution output, and dependency
directories are excluded from coverage or duplication metrics where the
existing Portal policy already treats them as generated artifacts.

`sonar.qualitygate.wait=true` makes the GitHub Actions result reflect the
SonarQube Cloud quality gate. `sonar.qualitygate.timeout=600` bounds the wait at
ten minutes.

## Failure Behaviour

The workflow fails closed when:

- `SONAR_TOKEN` is missing or rejected;
- the database service is unhealthy;
- restore, build, .NET tests, or frontend tests fail;
- either coverage report is missing;
- analysis upload fails; or
- the quality gate fails or exceeds its timeout.

The scanner end step runs only after successful build and coverage collection;
an incomplete analysis is not presented as successful.

## Verification

Repository verification will prove:

- the root workflow has `workflow_dispatch` and no automatic trigger;
- the configured organization and project key are exact;
- only `secrets.SONAR_TOKEN` supplies the scanner credential;
- both PostgreSQL test connection variables are present;
- the solution build and both coverage imports are configured;
- the workflow waits for the quality gate;
- the YAML parses successfully; and
- existing source-boundary and PostgreSQL guards remain green.

Because a local checkout cannot access repository secrets or impersonate the
self-hosted GitHub job exactly, final end-to-end verification requires:

1. building and starting the Docker Compose runner stack with a short-lived
   registration token;
2. confirming the `rvt-sonar` runner is online in repository settings; and
3. starting one manual workflow run after the workflow is pushed.
