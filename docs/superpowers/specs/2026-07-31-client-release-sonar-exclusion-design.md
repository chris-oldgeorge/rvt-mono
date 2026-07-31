# Client Release Sonar Exclusion Design

## Purpose

The internal `rvt-mono` repository uses SonarCloud and a dedicated self-hosted
runner. The reviewable client repository,
`RVT-Group-LTD/rvt-monitors`, does not. Publishing the internal Sonar workflow
and its supporting runner, checks, and operator documentation creates unused
automation in the client repository and can invite accidental resource
consumption.

The client release boundary will remove operational Sonar machinery while
preserving the internal source repository's analysis, generic engineering
standards, ordinary GitHub Actions tests, and product-level suppression
rationale.

## Selected Scope

The selected scope is **operational Sonar removal**.

The client payload must not contain:

- the manual SonarQube GitHub Actions workflow;
- the dedicated self-hosted Sonar runner image and Compose stack;
- tests whose purpose is to validate that workflow or runner;
- the internal integration fixture that couples engineering-standards
  verification to the Sonar workflow;
- Sonar operator and SQL-analysis documentation;
- root documentation links that point at excluded Sonar operator material; or
- a renamed GitHub Actions workflow that still invokes the known Sonar
  execution surface.

The client payload must retain:

- `.github/workflows/tests.yml`;
- `.github/workflows/engineering-standards.yml`;
- application and integration tests;
- repository boundary guards used by the client workflows;
- `.editorconfig` analyzer severities and generic engineering standards;
- product code suppressions and their vendor-neutral justification document;
- `RELEASE_SOURCE.json` and `RELEASE_MANIFEST.txt`; and
- every existing secret, local-state, generated-output, and internal-release
  exclusion.

## Release Boundary

The source repository remains the single source of truth. Sonar continues to
run only there.

`docs/release/client-release-exclusions.txt` will add these client-only
exclusions:

```text
.github/workflows/sonarqube.yml
.github/runner/**
docs/operations/github-actions/self-hosted-sonar-runner.md
docs/development/portal/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md
tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-sonar-runner-stack.test.sh
tests/verify-engineering-standards-integration.test.sh
```

The globalization suppression rationale is not disposable Sonar machinery.
Production `[SuppressMessage]` justifications rely on it. It will move from
`docs/development/portal/sonar/globalization-suppressions.md` to the
vendor-neutral path
`docs/development/portal/globalization-suppressions.md`, and all live
references will move with it. Historical move records may continue to mention
the old path because they describe repository history rather than executable
client automation.

The root `README.md` and `docs/index.md` will stop linking to the excluded
self-hosted Sonar runner guide. The source-only guide remains available by its
direct path to internal operators.

## Renamed-Workflow Guard

Path exclusions alone protect known files but would allow a future Sonar
workflow to be renamed and exported. `scripts/verify-client-release.sh` will
therefore inspect only published GitHub workflow YAML files for known
operational signatures.

The client verifier will reject a workflow containing any of these
case-insensitive signatures:

```text
SONAR_TOKEN
sonarcloud.io
dotnet-sonarscanner
sonar-scanner
rvt-sonar
SonarQube
```

The diagnostic will report the relative workflow path and a stable rule name,
but never print matched values or workflow contents.

The ordinary test workflow currently contains one explanatory SonarQube
reference in a comment. That comment will be reworded in vendor-neutral terms
so the semantic guard describes the actual client contract without weakening
the guard.

The semantic scan is deliberately limited to `.github/workflows/*.yml` and
`.github/workflows/*.yaml`. Generated-directory exclusions such as `.sonar/`,
historical documentation, analyzer suppressions, and test fixtures containing
non-executable analysis terminology are not publication processes and will not
cause false failures.

## Export and Verification Flow

The existing flow remains structurally unchanged:

1. `scripts/export-client-release.sh` archives one committed source revision.
2. It removes every path matched by the release exclusion policy.
3. It generates source metadata and the deterministic manifest.
4. `scripts/verify-client-release.sh` rejects excluded paths, unsafe file
   types, saved secrets, and Sonar workflow signatures.
5. `scripts/publish-client-release.sh` prepares a client commit and verifies
   an independent post-push clone.

No export-time text rewriting, conditional template engine, or second client
source tree will be introduced. Shared documentation changes are ordinary
source edits, and client-only file removal stays declarative.

## Testing

Implementation will follow a red-green sequence.

First, release contract fixtures will add representative Sonar workflow,
runner, documentation, and test paths to a source fixture and assert that the
current exporter incorrectly includes them. A verifier fixture will add a
renamed workflow containing an operational signature and assert that the
current verifier incorrectly accepts it.

After the exclusions and semantic guard are implemented:

- `tests/export-client-release.test.sh` must prove all explicit Sonar paths are
  absent and ordinary workflows remain;
- `tests/verify-client-release.test.sh` must prove each excluded path and a
  renamed Sonar workflow are rejected with path-and-rule diagnostics;
- the release manifest must still exactly match the exported files;
- two exports of the same commit must remain byte-for-byte deterministic;
- `tests/publish-client-release.test.sh` must remain green;
- the documentation layout and engineering workflow contracts must remain
  green; and
- a prepare-only export of the current source commit must contain the normal
  test and engineering workflows while containing none of the excluded Sonar
  paths or workflow signatures.

## Publication

The tooling change will first merge into source `main` through its normal pull
request and required checks.

The merged source revision will then be exported to a client review branch,
not pushed directly over `release-candidate`. A pull request in
`RVT-Group-LTD/rvt-monitors` will show the removal of the Sonar workflow,
runner stack, dedicated checks, and documentation while retaining the ordinary
client CI workflows. The client branch will be merged only after its required
checks and an independent payload verification pass.

## Non-Goals

- Disabling or weakening Sonar in the internal source repository.
- Removing Roslyn analyzer configuration or generic engineering standards.
- Removing product suppressions whose rationale is independent of Sonar
  execution.
- Introducing configurable release profiles or a general template system.
- Changing the client repository's application behavior, runtime
  configuration, or deployment topology.

## Success Criteria

The change is complete when:

1. the internal source repository still contains and validates its Sonar
   workflow and runner stack;
2. a prepared client payload contains no Sonar workflow, runner stack,
   dedicated Sonar checks, or operator/SQL-analysis documentation;
3. no remaining published workflow contains an operational Sonar signature;
4. the client `Tests` and `Engineering standards` workflows remain present;
5. export, verifier, publisher, documentation, and workflow contract tests
   pass; and
6. the reviewed client pull request is merged and its resulting
   `release-candidate` payload independently passes the release verifier.
