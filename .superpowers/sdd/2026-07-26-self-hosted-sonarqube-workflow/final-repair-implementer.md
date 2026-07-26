# Self-hosted SonarQube final repair — implementer report

## Commit

`e1c8def` — `Repair isolated Sonar runner workflow`

## Files changed

- `.github/runner/Dockerfile`
- `.github/workflows/sonarqube.yml`
- `tests/verify-sonar-runner-stack.test.sh`
- `tests/verify-manual-sonarqube-workflow.test.sh`
- `docs/superpowers/specs/2026-07-26-manual-sonarqube-workflow-design.md`
- `docs/superpowers/plans/2026-07-26-self-hosted-sonarqube-workflow.md`
- `docs/operations/github-actions/self-hosted-sonar-runner.md`
- `project_state.md`

## RED / GREEN evidence

- Runner guard RED: the unchanged Dockerfile failed with `Ubuntu Noble runner
  image must install libssl3t64` after the guard required Noble t64 SSL/LTTng
  plus Kerberos/GSSAPI runtime packages.
- Workflow guard RED: the unchanged workflow failed with `database connections
  must be exported after a unique per-run database is created` after the guard
  rejected job-global test connections.
- GREEN: both focused guards passed after the Dockerfile used `libssl3t64`,
  `liblttng-ust1t64`, `libkrb5-3`, and `libgssapi-krb5-2`, and the workflow
  created/deployed/cleaned a run-ID/attempt-scoped database. The workflow
  guard's in-process negative mutations also reject extension-only preparation
  and missing `RVT.SchemaDeploy` deployment.

## Validation

- `tests/verify-sonar-runner-stack.test.sh` — PASS
- `tests/verify-manual-sonarqube-workflow.test.sh` — PASS
- Every `tests/verify-*.test.sh` guard — PASS
- `docker compose -f .github/runner/docker-compose.yml config --quiet` — PASS
- `bash -n .github/runner/entrypoint.sh tests/verify-sonar-runner-stack.test.sh tests/verify-manual-sonarqube-workflow.test.sh` — PASS
- `git diff --check` — PASS

## External concern

Docker Desktop's daemon was unavailable (`docker info` could not connect), so
the real ARM64 runner image build and `Runner.Listener --version` smoke test
were not run. Do not start or register the runner. The controller must perform
that build/smoke validation when Docker is available.
