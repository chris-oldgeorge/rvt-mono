# Self-Hosted SonarQube Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the unified monorepo's complete SonarQube Cloud analysis only on
manual request, using an isolated Linux ARM64 GitHub Actions runner and
TimescaleDB companion hosted in Docker on the development machine.

**Architecture:** A Docker Compose stack under `.github/runner` provides a
repository-scoped runner and database on a private network. A root
`workflow_dispatch` workflow targets the runner's `rvt-sonar` label, builds
`Rvt.Mono.slnx`, collects .NET and Portal client coverage, and waits for the
SonarQube Cloud quality gate. Behavior-oriented shell tests execute the runner
bootstrap against fixtures and inspect parsed Compose/workflow configuration to
prevent automatic triggers, host mounts, Docker socket access, credential drift,
or loss of coverage and database requirements.

**Tech Stack:** GitHub Actions, GitHub Actions Runner 2.334.0, Docker Compose,
Ubuntu 24.04 Linux ARM64, TimescaleDB 2.28.3/PostgreSQL 17, .NET 10,
SonarScanner for .NET 11.2.1, dotnet-coverage 18.9.0, JDK 17, Node.js 24,
Vitest LCOV, Bash.

## Global Constraints

- The only workflow event is scalar `on: workflow_dispatch`.
- SonarQube Cloud host is `https://sonarcloud.io`.
- Organization is `aileron-forward`; project key is
  `aileron-forward_rvt-mono`.
- Scanner authentication comes only from `secrets.SONAR_TOKEN`.
- Runner selection is `[self-hosted, linux, ARM64, rvt-sonar]`.
- Do not mount the development source tree or `/var/run/docker.sock`.
- Persist only the three GitHub runner registration files in a Docker named
  volume.
- Bootstrap runner version `2.334.0` with Linux ARM64 SHA-256
  `f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4`.
- Use `timescale/timescaledb:2.28.3-pg17` as the sibling database.
- Use both `RVT_TEST_POSTGRES_CONNECTION` and
  `RVT__POSTGRES_INTEGRATION_CONNECTION`.
- Build and test `Rvt.Mono.slnx` in Release mode using the direct source
  project graph.
- Import one `dotnet-coverage` XML report and Portal Vitest LCOV.
- Wait at most 600 seconds for the Sonar quality gate.
- Preserve all unrelated untracked files listed in `project_state.md`.

---

### Task 1: Containerized runner and database stack

**Files:**
- Create: `.github/runner/Dockerfile`
- Create: `.github/runner/entrypoint.sh`
- Create: `.github/runner/docker-compose.yml`
- Create: `tests/verify-sonar-runner-stack.test.sh`

**Interfaces:**
- Consumes: shell variable `RUNNER_REGISTRATION_TOKEN` supplied only to a
  transient bootstrap container while a runner is first registered or replaced.
- Produces: runner labels `self-hosted`, `linux`, `ARM64`, `rvt-sonar`;
  network hostname `rvt-sonar-db`; database `rvt_sonar_ci`; named volume
  `runner-state`.

- [ ] **Step 1: Write the failing runner-stack behavior test**

Create `tests/verify-sonar-runner-stack.test.sh` as an executable Bash test. It
must:

- fail clearly if any of the three runner-stack files are absent;
- run `docker compose config --format json` and use a standard-library JSON
  parser to assert the resolved service image, runner labels, database
  hostname, named-volume mount, dependency, and absence of bind mounts,
  published ports, privileged mode, and Docker socket access;
- execute `entrypoint.sh` against a temporary fake runner distribution using
  overridable `RUNNER_DIST_ROOT`, `RUNNER_HOME`, `RUNNER_STATE_ROOT`, and
  `RUNNER_USER` values plus a fake `gosu` on `PATH`;
- prove that a first start without `RUNNER_REGISTRATION_TOKEN` fails;
- prove that a bootstrap-only first registration with a token invokes fake
  registration, moves only `.runner`, `.credentials`, and
  `.credentials_rsaparams` into state, restores symlinks, and exits without
  reaching the listener;
- prove that a normal second start with no token reuses persisted state,
  restores symlinks, and reaches the listener without invoking registration;
- retain only the minimal source assertions needed for the pinned runner
  version/checksum and the absence of Docker installation in the Dockerfile.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
tests/verify-sonar-runner-stack.test.sh
```

Expected: FAIL with `Missing self-hosted runner file:
.github/runner/Dockerfile`.

- [ ] **Step 3: Add the pinned Linux ARM64 runner image**

Create `.github/runner/Dockerfile` with this responsibility:

```dockerfile
FROM ubuntu:24.04

ARG RUNNER_VERSION=2.334.0
ARG RUNNER_SHA256=f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       ca-certificates curl git gosu jq libicu74 libssl3 postgresql-client \
       tar gzip unzip xz-utils zlib1g \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --uid 1001 --shell /bin/bash runner
WORKDIR /opt/actions-runner-dist
RUN curl -fsSLo actions-runner.tar.gz \
      "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-arm64-${RUNNER_VERSION}.tar.gz" \
    && echo "${RUNNER_SHA256}  actions-runner.tar.gz" | sha256sum -c - \
    && tar -xzf actions-runner.tar.gz \
    && rm actions-runner.tar.gz

COPY --chmod=755 entrypoint.sh /usr/local/bin/runner-entrypoint
ENTRYPOINT ["/usr/local/bin/runner-entrypoint"]
```

Do not install Docker inside the image.

- [ ] **Step 4: Add non-root registration and startup**

Create `.github/runner/entrypoint.sh`. Persist only the three GitHub registration
files in `/runner-state`, require the short-lived registration token only when
that state is absent, and run the listener as `runner`. `RUNNER_BOOTSTRAP_ONLY`
defaults to `false`; when it is `true`, registration persists the files and the
entrypoint exits before `run.sh`. Production defaults are fixed, while path/user
variables may be overridden by the behavior test:

```bash
#!/usr/bin/env bash
set -euo pipefail

runner_dist_root="${RUNNER_DIST_ROOT:-/opt/actions-runner-dist}"
runner_home="${RUNNER_HOME:-/home/runner/actions-runner}"
runner_state="${RUNNER_STATE_ROOT:-/runner-state}"
runner_user="${RUNNER_USER:-runner}"
bootstrap_only="${RUNNER_BOOTSTRAP_ONLY:-false}"
registration_files=(.runner .credentials .credentials_rsaparams)
mkdir -p "${runner_home}" "${runner_state}"

if [[ ! -x "${runner_home}/bin/Runner.Listener" ]]; then
  cp -a "${runner_dist_root}/." "${runner_home}/"
fi
chown -R "${runner_user}" "${runner_home}" "${runner_state}"

cd "${runner_home}"
for registration_file in "${registration_files[@]}"; do
  if [[ -f "${runner_state}/${registration_file}" ]]; then
    ln -sfn "${runner_state}/${registration_file}" "${runner_home}/${registration_file}"
  fi
done

if [[ ! -f "${runner_state}/.runner" ]]; then
  : "${RUNNER_REGISTRATION_TOKEN:?Set a short-lived repository runner registration token for first start.}"
  gosu "${runner_user}" ./config.sh \
    --unattended \
    --url "${RUNNER_URL}" \
    --token "${RUNNER_REGISTRATION_TOKEN}" \
    --name "${RUNNER_NAME}" \
    --labels "${RUNNER_LABELS}" \
    --work _work \
    --replace

  for registration_file in "${registration_files[@]}"; do
    mv "${runner_home}/${registration_file}" "${runner_state}/${registration_file}"
    ln -s "${runner_state}/${registration_file}" "${runner_home}/${registration_file}"
  done
fi

if [[ "${bootstrap_only}" == "true" ]]; then
  unset RUNNER_REGISTRATION_TOKEN
  exit 0
fi

unset RUNNER_REGISTRATION_TOKEN
exec gosu "${runner_user}" ./run.sh
```

- [ ] **Step 5: Add the isolated Compose topology**

Create `.github/runner/docker-compose.yml` with no published port, bind mount,
or Docker socket:

```yaml
name: rvt-sonar-runner

services:
  rvt-sonar-db:
    image: timescale/timescaledb:2.28.3-pg17
    environment:
      POSTGRES_DB: rvt_sonar_ci
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      TIMESCALEDB_TELEMETRY: "off"
    healthcheck:
      test: [CMD-SHELL, pg_isready -U postgres -d rvt_sonar_ci]
      interval: 5s
      timeout: 5s
      retries: 30

  rvt-sonar-runner:
    build:
      context: .
    environment:
      RUNNER_URL: https://github.com/chris-oldgeorge/rvt-mono
      RUNNER_NAME: rvt-sonar-dev
      RUNNER_LABELS: rvt-sonar
    volumes:
      - runner-state:/runner-state
    depends_on:
      rvt-sonar-db:
        condition: service_healthy
    restart: unless-stopped

volumes:
  runner-state:
```

- [ ] **Step 6: Run runner-stack validation**

Run:

```bash
chmod +x .github/runner/entrypoint.sh tests/verify-sonar-runner-stack.test.sh
tests/verify-sonar-runner-stack.test.sh
docker compose -f .github/runner/docker-compose.yml config --quiet
```

Expected: behavior test PASS and Compose configuration exit 0.

- [ ] **Step 7: Commit the runner stack**

```bash
git add .github/runner tests/verify-sonar-runner-stack.test.sh
git commit -m "Add isolated self-hosted Sonar runner"
```

### Task 2: Manual monorepo SonarQube workflow

**Files:**
- Create: `.github/workflows/sonarqube.yml`
- Create: `tests/verify-manual-sonarqube-workflow.test.sh`

**Interfaces:**
- Consumes: runner label `rvt-sonar`, database hostname `rvt-sonar-db`,
  repository secret `SONAR_TOKEN`.
- Produces: SonarQube Cloud analysis for `aileron-forward_rvt-mono`, .NET
  coverage at `artifacts/coverage/coverage.xml`, frontend LCOV at
  `apps/portal/RvtPortal.Client/coverage/lcov.info`.

- [ ] **Step 1: Write the failing workflow boundary test**

Create executable `tests/verify-manual-sonarqube-workflow.test.sh`. Use Ruby's
standard-library Psych syntax tree (not YAML 1.1 object coercion) to parse the
workflow and assert:

- the only event is the scalar `workflow_dispatch`;
- the analyze job selects exactly
  `[self-hosted, linux, ARM64, rvt-sonar]`;
- permissions, timeout, both PostgreSQL variables, and the absence of
  `services` are represented in the parsed job;
- the parsed steps use pinned action commit SHAs and contain the required
  scanner identity, secret, solution, coverage paths, quality-gate settings,
  database preparation, and test commands;
- no step references the Docker socket or invokes Docker.

Minimal scalar-content assertions are acceptable for embedded multi-line shell
programs because GitHub interprets those programs outside the YAML data model.

- [ ] **Step 2: Run the workflow test to verify it fails**

Run:

```bash
tests/verify-manual-sonarqube-workflow.test.sh
```

Expected: FAIL with `Missing .github/workflows/sonarqube.yml`.

- [ ] **Step 3: Create the manual-only job envelope**

Create `.github/workflows/sonarqube.yml` with this top-level contract:

```yaml
name: SonarQube

on: workflow_dispatch

permissions:
  contents: read

concurrency:
  group: sonar-${{ github.ref }}
  cancel-in-progress: false

jobs:
  analyze:
    runs-on: [self-hosted, linux, ARM64, rvt-sonar]
    timeout-minutes: 120
    env:
      RVT_TEST_POSTGRES_CONNECTION: Host=rvt-sonar-db;Port=5432;Database=rvt_sonar_ci;Username=postgres;Password=postgres
      RVT__POSTGRES_INTEGRATION_CONNECTION: Host=rvt-sonar-db;Port=5432;Database=rvt_sonar_ci;Username=postgres;Password=postgres
```

- [ ] **Step 4: Add pinned checkout and toolchain setup**

Use the already-reviewed action commits from the imported Portal workflow:

```yaml
    steps:
      - name: Check out repository
        uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5
        with:
          fetch-depth: 0

      - name: Set up JDK 17
        uses: actions/setup-java@c1e323688fd81a25caa38c78aa6df2d33d3e20d9
        with:
          java-version: 17
          distribution: zulu

      - name: Set up .NET 10
        uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9
        with:
          dotnet-version: 10.0.x

      - name: Set up Node.js 24
        uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020
        with:
          node-version: 24.x
          cache: npm
          cache-dependency-path: apps/portal/RvtPortal.Client/package-lock.json
```

- [ ] **Step 5: Add database readiness and extension setup**

Use the runner image's PostgreSQL client and fail closed:

```yaml
      - name: Prepare integration database
        env:
          PGPASSWORD: postgres
        run: |
          set -euo pipefail
          until pg_isready -h rvt-sonar-db -U postgres -d rvt_sonar_ci; do
            sleep 2
          done
          psql -h rvt-sonar-db -U postgres -d rvt_sonar_ci -v ON_ERROR_STOP=1 <<'SQL'
          CREATE EXTENSION IF NOT EXISTS timescaledb;
          CREATE EXTENSION IF NOT EXISTS pgcrypto;
          SQL
```

- [ ] **Step 6: Add scanner begin, source build, and coverage**

Install pinned tools under `.sonar`, begin analysis with the exact project
identity and report paths, then build serially:

```yaml
      - name: Install analysis tools
        run: |
          dotnet tool install dotnet-sonarscanner --tool-path .sonar --version 11.2.1
          dotnet tool install dotnet-coverage --tool-path .sonar --version 18.9.0

      - name: Begin SonarQube analysis
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: |
          set -euo pipefail
          ./.sonar/dotnet-sonarscanner begin \
            /k:aileron-forward_rvt-mono \
            /o:aileron-forward \
            /d:sonar.host.url=https://sonarcloud.io \
            /d:sonar.token="${SONAR_TOKEN}" \
            /d:sonar.cs.vscoveragexml.reportsPaths=artifacts/coverage/coverage.xml \
            /d:sonar.javascript.lcov.reportPaths=apps/portal/RvtPortal.Client/coverage/lcov.info \
            /d:sonar.scanner.scanAll=true \
            '/d:sonar.cpd.exclusions=apps/portal/database/**,apps/portal/RVT.DataAccess/Migrations/**,apps/portal/RVT.DataAccess/EntityModels/Models/**,apps/portal/RvtPortal.Client/dist/**,apps/portal/RvtPortal.Client/coverage/**,apps/portal/RvtPortal.Client/src/api/schema.d.ts,**/bin/**,**/obj/**' \
            '/d:sonar.coverage.exclusions=apps/portal/database/**,apps/portal/RVT.DataAccess/Migrations/**,apps/portal/RVT.DataAccess/EntityModels/Models/**,apps/portal/RvtPortal.Client/dist/**,apps/portal/RvtPortal.Client/coverage/**,apps/portal/RvtPortal.Client/src/api/schema.d.ts,**/bin/**,**/obj/**' \
            /d:sonar.qualitygate.wait=true \
            /d:sonar.qualitygate.timeout=600

      - name: Restore and build monorepo
        run: |
          dotnet restore Rvt.Mono.slnx --disable-parallel
          dotnet build Rvt.Mono.slnx --configuration Release --no-restore --no-incremental --nologo -m:1

      - name: Collect .NET coverage
        run: |
          mkdir -p artifacts/coverage
          ./.sonar/dotnet-coverage collect \
            "dotnet test Rvt.Mono.slnx --configuration Release --no-build --no-restore --nologo -m:1" \
            -f xml \
            -o artifacts/coverage/coverage.xml
          test -s artifacts/coverage/coverage.xml
```

- [ ] **Step 7: Add Portal coverage and scanner completion**

```yaml
      - name: Collect Portal client coverage
        working-directory: apps/portal/RvtPortal.Client
        run: |
          npm ci
          npm run test:coverage
          test -s coverage/lcov.info

      - name: End SonarQube analysis
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: ./.sonar/dotnet-sonarscanner end /d:sonar.token="${SONAR_TOKEN}"
```

- [ ] **Step 8: Run parsed workflow validation**

Run:

```bash
chmod +x tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
git diff --check
```

Expected: PASS and no whitespace errors.

- [ ] **Step 9: Commit the workflow**

```bash
git add .github/workflows/sonarqube.yml tests/verify-manual-sonarqube-workflow.test.sh
git commit -m "Add manual monorepo SonarQube workflow"
```

### Task 3: Operations documentation and final verification

**Files:**
- Create: `docs/operations/github-actions/self-hosted-sonar-runner.md`
- Modify: `docs/index.md`
- Modify: `README.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: `.github/runner/docker-compose.yml`,
  `.github/workflows/sonarqube.yml`.
- Produces: safe bootstrap, start, stop, replacement, manual-run, and recovery
  instructions for the development machine operator.

- [ ] **Step 1: Document secure runner registration**

Create `docs/operations/github-actions/self-hosted-sonar-runner.md` with exact
operator steps:

1. Confirm the repository remains private and Docker Desktop is running.
2. Open repository **Settings → Actions → Runners → New self-hosted runner** and
   copy the short-lived registration token.
3. Bootstrap registration in a transient container without writing the token to
   disk or placing it in persistent runner configuration:

   ```bash
   read -rsp "Short-lived runner registration token: " RUNNER_REGISTRATION_TOKEN
   echo
   export RUNNER_REGISTRATION_TOKEN
   docker compose -f .github/runner/docker-compose.yml run --rm \
     -e RUNNER_BOOTSTRAP_ONLY=true \
     -e RUNNER_REGISTRATION_TOKEN \
     rvt-sonar-runner
   unset RUNNER_REGISTRATION_TOKEN
   docker compose -f .github/runner/docker-compose.yml up -d
   ```

4. Confirm runner `rvt-sonar-dev` is online with label `rvt-sonar`.
5. Start **Actions → SonarQube → Run workflow** and select the trusted ref.
6. Inspect runner/database logs:

   ```bash
   docker compose -f .github/runner/docker-compose.yml logs --tail=200
   ```

7. Stop without deleting registration state:

   ```bash
   docker compose -f .github/runner/docker-compose.yml stop
   ```

8. Explain normal restart, safe runner replacement and recovery: stop/remove
   the local persistent runner, remove its stale GitHub record, delete only the
   `rvt-sonar-runner_runner-state` named volume, obtain a fresh token, bootstrap
   again, and start the stack. Document that deleting this one volume removes
   only runner registration state and requires re-registration.

Include GitHub's warning that self-hosted runners are restricted to private
repositories and trusted code.

- [ ] **Step 2: Link the operations guide**

Add the guide under the operations section of `docs/index.md` and add a concise
manual SonarQube entry to the root `README.md`. Do not duplicate bootstrap
commands in the README; link to the operations guide.

- [ ] **Step 3: Record final state**

Replace the "implementation has not started" statement in `project_state.md`
with:

- branch and commit identifiers;
- the `.github/runner` file structure and variable definitions;
- runner and database names/labels;
- workflow trigger and Sonar identity;
- tool versions and coverage paths;
- validation commands and results;
- the remaining external step: obtaining a short-lived registration token and
  executing the first manual run.

Keep `Next-session instruction: Read project_state.md to get up to speed` as
the final line.

- [ ] **Step 4: Run all final static and repository gates**

Run:

```bash
tests/verify-sonar-runner-stack.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
for test_script in tests/verify-*.test.sh; do "${test_script}"; done
docker compose -f .github/runner/docker-compose.yml config --quiet
git diff --check
```

Expected: all behavior/semantic shell tests PASS, Compose parses, and no
whitespace errors.

If Docker Desktop is running, also run:

```bash
docker compose -f .github/runner/docker-compose.yml build rvt-sonar-runner
```

Expected: the runner archive checksum validates and the Linux ARM64 image
builds. Do not start/register the runner without a short-lived registration
token.

- [ ] **Step 5: Commit documentation and state**

```bash
git add README.md docs/index.md docs/operations/github-actions/self-hosted-sonar-runner.md project_state.md
git commit -m "Document self-hosted Sonar operations"
```

- [ ] **Step 6: Review and push**

Run:

```bash
git status --short
git log --oneline origin/codex/direct-project-references..HEAD
git push origin codex/direct-project-references
```

Confirm that only the known unrelated untracked files remain. After the push,
the operator must register/start the runner and launch the first manual
SonarQube workflow; that remote run is the final integration test.
