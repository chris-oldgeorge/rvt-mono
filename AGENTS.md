<!-- sonar:begin:codex-secrets-on-read -->
# SonarQube secrets scanning for files protocol

Before reading any file in this workspace, scan it for secrets with the deterministic scanner:

```bash
sonar analyze secrets <path/to/file>
```

If the command reports that the file contains a secret, **do not read the file**. Instead:

1. Inform the user that the file appears to contain a secret or credential and that reading it would expose the value in chat history, logs, and any downstream telemetry.
2. Advise them to rotate the leaked credential at its source of truth and remove it from the file.
3. Do not proceed with the original request until the secret has been removed.

## Approved false-positive exception

The following exact test fixtures are each approved to contain the same three
non-secret test values detected as `secrets:S2068` (`Generic Password`):

`apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/testdata/measuring_points.json`

`apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/testdata/sensors.json`

Each file may be read only when `sonar analyze secrets` reports exactly three
`secrets:S2068` findings and no findings from any other rule. Do not display
the matched values. Any changed count, rule, or path remains blocking.
<!-- sonar:end:codex-secrets-on-read -->

# Maintainability guardrails

- Keep the Sonar rules promoted in the root `.editorconfig` at error severity;
  do not downgrade or broadly suppress them. Any suppression must target one
  compatibility or framework-shaped symbol and include a concrete
  `Justification`.
- In shell scripts, extract any nontrivial literal used three or more times
  into a clearly named, immutable top-level variable. Test fixtures are not
  exempt: reuse named fixture values so Sonar `shell:S1192` cannot recur.
