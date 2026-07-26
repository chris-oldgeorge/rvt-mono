# Self-Hosted SonarQube Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the unified monorepo's complete SonarQube Cloud analysis only on
manual request, using an isolated Linux ARM64 GitHub Actions runner and
TimescaleDB companion hosted in Docker on the development machine.

**Architecture:** A Docker Compose stack under `.github/runner` provides a
repository-scoped runner and database on a private network. A root
`workflow_dispatch` workflow targets the runner's `rvt-sonar` label, builds
`Rvt.Mono.slnx`, collects .NET and Portal client coverage, and waits for the
SonarQube Cloud quality gate. Structural shell tests prevent automatic triggers,
host mounts, Docker socket access, credential drift, or loss of coverage and
database requirements.

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
- Persist only runner registration/runtime state in a Docker named volume.
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
- Consumes: shell variable `RUNNER_REGISTRATION_TOKEN` supplied only while a
  runner is first registered or replaced.
- Produces: runner labels `self-hosted`, `linux`, `ARM64`, `rvt-sonar`;
  network hostname `rvt-sonar-db`; database `rvt_sonar_ci`; named volume
  `runner-state`.

- [ ] **Step 1: Write the failing runner-stack structural test**

Create `tests/verify-sonar-runner-stack.test.sh` as an executable Bash test. It
must require the four exact properties below and reject host access:

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${repo_root}/.github/runner/Dockerfile"
entrypoint="${repo_root}/.github/runner/entrypoint.sh"
compose="${repo_root}/.github/runner/docker-compose.yml"

for required in "${dockerfile}" "${entrypoint}" "${compose}"; do
  [[ -f "${required}" ]] || {
    printf 'Missing self-hosted runner file: %s\n' "${required#${repo_root}/}" >&2
    exit 1
  }
done

grep -Fq 'ARG RUNNER_VERSION=2.334.0' "${dockerfile}"
grep -Fq 'f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4' "${dockerfile}"
grep -Fq 'timescale/timescaledb:2.28.3-pg17' "${compose}"
grep -Fq 'RUNNER_LABELS: rvt-sonar' "${compose}"
grep -Fq 'runner-state:/runner-state' "${compose}"
grep -Fq 'rvt-sonar-db' "${compose}"

if grep -Eq '/var/run/docker\.sock|/Users/|[[:space:]-]\.\.?/[^:]+' "${compose}"; then
  echo 'Runner stack must not mount the Docker socket or repository paths.' >&2
  exit 1
fi

grep -Fq 'gosu runner' "${entrypoint}"
grep -Fq 'RUNNER_REGISTRATION_TOKEN' "${entrypoint}"
echo 'Self-hosted Sonar runner stack verified.'
```

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
that state is absent, and run the listener as `runner`:

```bash
#!/usr/bin/env bash
set -euo pipefail

runner_home=/home/runner/actions-runner
runner_state=/runner-state
registration_files=(.runner .credentials .credentials_rsaparams)
mkdir -p "${runner_home}" "${runner_state}"

if [[ ! -x "${runner_home}/bin/Runner.Listener" ]]; then
  cp -a /opt/actions-runner-dist/. "${runner_home}/"
fi
chown -R runner:runner "${runner_home}" "${runner_state}"

cd "${runner_home}"
for registration_file in "${registration_files[@]}"; do
  if [[ -f "${runner_state}/${registration_file}" ]]; then
    ln -sfn "${runner_state}/${registration_file}" "${runner_home}/${registration_file}"
  fi
done

if [[ ! -f "${runner_state}/.runner" ]]; then
  : "${RUNNER_REGISTRATION_TOKEN:?Set a short-lived repository runner registration token for first start.}"
  gosu runner ./config.sh \
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

unset RUNNER_REGISTRATION_TOKEN
exec gosu runner ./run.sh
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
      RUNNER_REGISTRATION_TOKEN: ${RUNNER_REGISTRATION_TOKEN:-}
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

Expected: structural test PASS and Compose configuration exit 0.

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

Create executable `tests/verify-manual-sonarqube-workflow.test.sh`. Require an
exact manual trigger and fail on every automatic event:

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow="${repo_root}/.github/workflows/sonarqube.yml"
[[ -f "${workflow}" ]] || {
  echo 'Missing .github/workflows/sonarqube.yml' >&2
  exit 1
}

grep -Fxq 'on: workflow_dispatch' "${workflow}"
if grep -Eq '(^|[[:space:]])(push|pull_request|schedule):|on:.*(push|pull_request|schedule)' "${workflow}"; then
  echo 'SonarQube workflow must be manual-only.' >&2
  exit 1
fi

for required in \
  'runs-on: [self-hosted, linux, ARM64, rvt-sonar]' \
  'aileron-forward_rvt-mono' \
  'aileron-forward' \
  'secrets.SONAR_TOKEN' \
  'RVT_TEST_POSTGRES_CONNECTION' \
  'RVT__POSTGRES_INTEGRATION_CONNECTION' \
  'Rvt.Mono.slnx' \
  'sonar.cs.vscoveragexml.reportsPaths=artifacts/coverage/coverage.xml' \
  'sonar.javascript.lcov.reportPaths=apps/portal/RvtPortal.Client/coverage/lcov.info' \
  'sonar.qualitygate.wait=true' \
  'sonar.qualitygate.timeout=600' \
  'dotnet-coverage collect' \
  'npm run test:coverage'; do
  grep -Fq "${required}" "${workflow}" || {
    printf 'Missing workflow contract: %s\n' "${required}" >&2
    exit 1
  }
done

if grep -Fq '/var/run/docker.sock' "${workflow}" || grep -Eq '^[[:space:]]+services:' "${workflow}"; then
  echo 'Workflow must use the isolated sibling database without Docker access.' >&2
  exit 1
fi

echo 'Manual SonarQube workflow verified.'
```

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

- [ ] **Step 8: Run workflow structural validation**

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
3. Start the stack without writing the token to disk:

   ```bash
   read -rsp "Short-lived runner registration token: " RUNNER_REGISTRATION_TOKEN
   echo
   export RUNNER_REGISTRATION_TOKEN
   docker compose -f .github/runner/docker-compose.yml up -d --build
   unset RUNNER_REGISTRATION_TOKEN
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

8. Explain that Docker Desktop and the development machine must remain awake,
   the `SONAR_TOKEN` stays in GitHub repository secrets, and removing the named
   volume requires re-registration.

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

Expected: all shell tests PASS, Compose parses, and no whitespace errors.

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
