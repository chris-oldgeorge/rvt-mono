# Self-Hosted SonarQube Runner

Use this guide to bootstrap and operate the dedicated development-machine runner
for the manually dispatched SonarQube Cloud workflow. GitHub warns that
self-hosted runners must be restricted to private repositories and trusted code:
workflow code executes on the machine that hosts the runner. Keep this
repository private and run only a trusted ref.

## Bootstrap and start

1. Confirm that the repository remains private and Docker Desktop is running.
2. In GitHub, open **Settings → Actions → Runners → New self-hosted runner**
   and copy the short-lived registration token.
3. From the repository root, bootstrap registration in a transient container:

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

4. Confirm that runner `rvt-sonar-dev` is online and has the `rvt-sonar` label.
5. Start **Actions → SonarQube → Run workflow** and select the trusted ref.

The development machine and Docker Desktop must remain awake while the workflow
runs. `SONAR_TOKEN` stays in GitHub repository secrets; never provide it to the
runner registration command or store it in this repository. The registration
token is not stored in repository files, shell history, or the persistent runner
container configuration; it exists transiently in the auto-removed bootstrap
container. Docker may write its normal transient-container metadata while that
bootstrap container exists.

The runner listener operates as the unprivileged `runner` user. The workflow
therefore sets `DOTNET_INSTALL_DIR` to `${{ runner.temp }}/dotnet` for
`actions/setup-dotnet`; its Linux default, `/usr/share/dotnet`, is not writable
by this runner and must not be restored.

The runner service uses Cloudflare (`1.1.1.1`) and Google (`8.8.8.8`) as
explicit DNS resolvers. Resolver attempts are limited to three with a two-second
timeout so a stalled Docker Desktop resolver cannot block GitHub job-lease
renewal until the lease expires. After changing `dns` or `dns_opt`, recreate
only the runner service so Docker writes the new resolver configuration:

```bash
docker compose -f .github/runner/docker-compose.yml up -d \
  --no-deps --force-recreate rvt-sonar-runner
```

The `runner-state` volume preserves registration during this recreation. The
database service is not restarted or recreated.

## Inspect, stop, and restart

Inspect runner and database logs with:

```bash
docker compose -f .github/runner/docker-compose.yml logs --tail=200
```

Stop the stack without deleting registration state with:

```bash
docker compose -f .github/runner/docker-compose.yml stop
```

Restart it normally, with no registration token:

```bash
docker compose -f .github/runner/docker-compose.yml up -d
```

The persistent runner service has no registration-token environment variable.
It restores its registration files from the named volume and starts the
listener.

## Per-analysis database isolation

Compose declares only `runner-state` as a named volume. It declares no named
database volume; the TimescaleDB base image may use Docker-managed anonymous
storage. `rvt_sonar_ci` is only the Compose seed/admin database, never a test
target. Each manual workflow derives a database name from its GitHub run ID and
attempt, force-drops a stale database with that name, creates it, installs
`timescaledb` and `pgcrypto`, and exports the four job-scoped test/deployment
connections. After the Release build it applies the three EF migration contexts
(`RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext`) with job-local
`dotnet-ef` `10.0.7`, then runs `RVT.SchemaDeploy` before coverage. The final
`always()` workflow step removes only that job database; it does not use Docker.

## Replace a runner

Use this procedure when intentionally replacing `rvt-sonar-dev` (for example,
after changing the runner identity). It invalidates the local registration and
the old GitHub-side runner record.

1. Stop and remove the local persistent runner container:

   ```bash
   docker compose -f .github/runner/docker-compose.yml stop rvt-sonar-runner
   docker compose -f .github/runner/docker-compose.yml rm -f rvt-sonar-runner
   ```

2. In GitHub, open **Settings → Actions → Runners**, select the stale
   `rvt-sonar-dev` record, and remove it.
3. Delete only the runner registration volume:

   ```bash
   docker volume rm rvt-sonar-runner_runner-state
   ```

4. Obtain a fresh short-lived registration token, repeat the **Bootstrap and
   start** command above, then confirm the replacement runner is online.

Deleting `rvt-sonar-runner_runner-state` is destructive only to the three
persisted GitHub runner registration files. It does not remove the database
container, Docker-managed database storage, or repository files, but it makes
the local runner unable to connect until it is registered again with a fresh
token.

## Recover damaged or permanently offline state

If the runner state is damaged, or GitHub shows the runner permanently offline,
treat it as a replacement: stop and remove the persistent runner container,
remove the stale GitHub runner record, delete only
`rvt-sonar-runner_runner-state`, obtain a fresh token, bootstrap in the
transient container, and start the stack. Do not reuse the old token or delete
unrelated Compose volumes.
