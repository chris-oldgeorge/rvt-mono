# SonarQube Analysis

The repository has a root solution, `rvt-monitors.sln`, for whole-project analysis. It includes:

- AirQ app and tests
- MyAtm app and tests
- Omnidots app and tests
- Svantek app and tests
- `Rvt.Monitor.Common` and tests

Use one SonarQube project for the full monitor estate:

```text
Project key: rvt-monitors
Project name: RVT Monitors
```

## Create the SonarQube Project

Create a token in SonarQube with permission to create projects, then run:

```sh
export SONAR_HOST_URL=http://localhost:9000
export SONAR_TOKEN=<redacted>
scripts/create-sonarqube-project.sh
```

Override the project identity if needed:

```sh
SONAR_PROJECT_KEY=my-key SONAR_PROJECT_NAME="My Name" scripts/create-sonarqube-project.sh
```

## Run Analysis

Install the .NET scanner once on the machine running analysis:

```sh
dotnet tool install --global dotnet-sonarscanner
```

Run the scan:

```sh
export SONAR_HOST_URL=http://localhost:9000
export SONAR_TOKEN=<redacted>
scripts/run-sonarqube-analysis.sh
```

For SonarCloud, include the organization:

```sh
export SONAR_HOST_URL=https://sonarcloud.io
export SONAR_ORGANIZATION=aileron-forward
export SONAR_PROJECT_KEY=aileron-forward_rvt-monitors
export SONAR_TOKEN=<redacted>
scripts/run-sonarqube-analysis.sh
```

The script restores and builds `rvt-monitors.sln`. Tests are optional because the monitor test suites can require Docker/Testcontainers:

```sh
RUN_TESTS=true scripts/run-sonarqube-analysis.sh
```

When `RUN_TESTS=true`, coverage collection is enabled by default. The script uses the existing `coverlet.collector` references in the test projects, writes OpenCover reports under `TestResults/coverage/<timestamp>/`, and passes the matching `sonar.cs.opencover.reportsPaths` value to the scanner. Disable coverage only when you explicitly need a plain test run:

```sh
RUN_TESTS=true COLLECT_COVERAGE=false scripts/run-sonarqube-analysis.sh
```

SonarQube import expects OpenCover output. The script fails the run if the coverage collector does not produce `coverage.opencover.xml` files.

Do not commit SonarQube tokens. Keep them in the shell environment or your CI secret store.
