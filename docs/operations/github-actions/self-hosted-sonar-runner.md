# Self-Hosted SonarQube Runner

Use this guide to bootstrap and operate the dedicated development-machine runner
for the manually dispatched SonarQube Cloud workflow. GitHub warns that
self-hosted runners must be restricted to private repositories and trusted code:
workflow code executes on the machine that hosts the runner. Keep this
repository private and run only a trusted ref.

## Register and start

1. Confirm that the repository remains private and Docker Desktop is running.
2. In GitHub, open **Settings → Actions → Runners → New self-hosted runner**
   and copy the short-lived registration token.
3. From the repository root, start the stack without writing the token to disk:

   ```bash
   read -rsp "Short-lived runner registration token: " RUNNER_REGISTRATION_TOKEN
   echo
   export RUNNER_REGISTRATION_TOKEN
   docker compose -f .github/runner/docker-compose.yml up -d --build
   unset RUNNER_REGISTRATION_TOKEN
   ```

4. Confirm that runner `rvt-sonar-dev` is online and has the `rvt-sonar` label.
5. Start **Actions → SonarQube → Run workflow** and select the trusted ref.

The development machine and Docker Desktop must remain awake while the workflow
runs. `SONAR_TOKEN` stays in GitHub repository secrets; never provide it to the
runner registration command or store it in this repository.

## Inspect and stop

Inspect runner and database logs with:

```bash
docker compose -f .github/runner/docker-compose.yml logs --tail=200
```

Stop the stack without deleting registration state with:

```bash
docker compose -f .github/runner/docker-compose.yml stop
```

The runner registration is persisted in the named `runner-state` volume. If
that volume is removed, obtain a new short-lived registration token and repeat
the registration and start steps.
