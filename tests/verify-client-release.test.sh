#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verifier="${repo_root}/scripts/verify-client-release.sh"
temp_dir="$(mktemp -d)"

cleanup() {
  local status=$?
  rm -rf "${temp_dir}"
  exit "${status}"
}
trap cleanup EXIT

create_payload() {
  local payload="$1"

  mkdir -p \
    "${payload}/.github/workflows" \
    "${payload}/apps/portal" \
    "${payload}/libs/rvt-monitor-common" \
    "${payload}/eng" \
    "${payload}/scripts" \
    "${payload}/tests" \
    "${payload}/docs/operations"

  printf '# Reviewable monorepo\n' > "${payload}/README.md"
  printf '<Solution />\n' > "${payload}/Rvt.Mono.slnx"
  printf '<Project />\n' > "${payload}/Directory.Build.props"
  printf '{"sdk":{"version":"10.0.302"}}\n' > "${payload}/global.json"
  printf 'name: verify\n' > "${payload}/.github/workflows/verify.yml"
  printf 'public class App {}\n' > "${payload}/apps/portal/App.cs"
  printf 'public class Shared {}\n' > "${payload}/libs/rvt-monitor-common/Shared.cs"
  printf '# Operations\n' > "${payload}/docs/operations/runbook.md"
  printf 'RVT__SENDGRID_API_KEY=${RVT__SENDGRID_API_KEY:-}\n' \
    > "${payload}/.env.example"
  printf '{"sourceCommit":"0123456789012345678901234567890123456789"}\n' \
    > "${payload}/RELEASE_SOURCE.json"
  find "${payload}" -type f \
    | sed "s#^${payload}/##" \
    | LC_ALL=C sort \
    > "${payload}/RELEASE_MANIFEST.txt"
}

assert_rejected_path() {
  local name="$1"
  local relative_path="$2"
  local fixture="${temp_dir}/${name}"
  local output

  create_payload "${fixture}"
  mkdir -p "$(dirname "${fixture}/${relative_path}")"
  printf 'internal fixture\n' > "${fixture}/${relative_path}"

  if output="$("${verifier}" --payload-dir "${fixture}" 2>&1)"; then
    printf 'FAIL: %s must be rejected.\n' "${relative_path}" >&2
    exit 1
  fi

  if [[ "${output}" != *"${relative_path}"* || "${output}" != *"[rule:"* ]]; then
    printf 'FAIL: %s rejection must report its path and rule, got:\n%s\n' \
      "${relative_path}" "${output}" >&2
    exit 1
  fi
}

assert_rejected_workflow() {
  local name="$1"
  local workflow_content="$2"
  local fixture="${temp_dir}/${name}"
  local relative_path=".github/workflows/quality-analysis.yml"
  local output

  create_payload "${fixture}"
  printf '%s\n' "${workflow_content}" > "${fixture}/${relative_path}"

  if output="$("${verifier}" --payload-dir "${fixture}" 2>&1)"; then
    printf 'FAIL: %s Sonar workflow fixture must be rejected.\n' "${name}" >&2
    exit 1
  fi

  if [[ "${output}" != *"${relative_path}"* \
    || "${output}" != *"[rule: Sonar client automation]"* ]]; then
    printf 'FAIL: %s must report only its path and Sonar rule, got:\n%s\n' \
      "${name}" "${output}" >&2
    exit 1
  fi

  if [[ "${output}" == *"${workflow_content}"* ]]; then
    printf 'FAIL: %s diagnostic exposed workflow contents.\n' "${name}" >&2
    exit 1
  fi
}

assert_rejected_secret() {
  local name="$1"
  local relative_path="$2"
  local content="$3"
  local expected_rule="$4"
  local secret_value="$5"
  local fixture="${temp_dir}/${name}"
  local output

  create_payload "${fixture}"
  mkdir -p "$(dirname "${fixture}/${relative_path}")"
  printf '%s\n' "${content}" > "${fixture}/${relative_path}"

  if output="$("${verifier}" --payload-dir "${fixture}" 2>&1)"; then
    printf 'FAIL: %s secret fixture must be rejected.\n' "${name}" >&2
    exit 1
  fi

  if [[ "${output}" != *"${relative_path}"* || "${output}" != *"[rule: ${expected_rule}]"* ]]; then
    printf 'FAIL: %s must report path and rule %s, got:\n%s\n' \
      "${name}" "${expected_rule}" "${output}" >&2
    exit 1
  fi

  if [[ "${output}" == *"${secret_value}"* ]]; then
    printf 'FAIL: %s diagnostic exposed the rejected value.\n' "${name}" >&2
    exit 1
  fi
}

safe_payload="${temp_dir}/safe"
create_payload "${safe_payload}"
"${verifier}" --payload-dir "${safe_payload}"

assert_rejected_path root-agent-instructions AGENTS.md
assert_rejected_path nested-agent-instructions apps/portal/AGENTS.md
assert_rejected_path project-state project_state.md
assert_rejected_path agent-state .agents/session.json
assert_rejected_path codex-state .codex/settings.json
assert_rejected_path codegraph-state apps/monitors/.codegraph/index.json
assert_rejected_path superpowers-plan docs/superpowers/plans/internal.md
assert_rejected_path history-plan docs/history/portal/plans/internal.md
assert_rejected_path internal-review docs/reviews/internal.md
assert_rejected_path nested-release-policy \
  apps/portal/docs/release/client-release-exclusions.txt
assert_rejected_path exact-env apps/portal/.env
assert_rejected_path development-settings \
  apps/portal/appsettings.Development.json
assert_rejected_path local-settings apps/portal/local.settings.json
assert_rejected_path private-key deploy/client.key
assert_rejected_path pem-key deploy/client.pem
assert_rejected_path pkcs12 deploy/client.p12
assert_rejected_path certificate-bundle deploy/client.pfx

assert_rejected_workflow sonar-token 'env: { SONAR_TOKEN: placeholder }'
assert_rejected_workflow sonar-cloud 'run: curl https://sonarcloud.io'
assert_rejected_workflow dotnet-sonar-scanner 'run: dotnet-sonarscanner begin'
assert_rejected_workflow generic-sonar-scanner 'run: sonar-scanner'
assert_rejected_workflow sonar-runner-label 'runs-on: rvt-sonar'
assert_rejected_workflow sonarqube-name 'name: SonarQube'

unsafe_symlink="${temp_dir}/unsafe-symlink"
create_payload "${unsafe_symlink}"
ln -s ../../outside "${unsafe_symlink}/apps/portal/outside-link"
if output="$("${verifier}" --payload-dir "${unsafe_symlink}" 2>&1)"; then
  printf 'FAIL: symlinks escaping the payload must be rejected.\n' >&2
  exit 1
fi
if [[ "${output}" != *"apps/portal/outside-link"* || "${output}" != *"[rule:"* ]]; then
  printf 'FAIL: unsafe symlink rejection must report its path and rule.\n' >&2
  exit 1
fi

special_file="${temp_dir}/special-file"
create_payload "${special_file}"
mkfifo "${special_file}/apps/portal/runtime.pipe"
if output="$("${verifier}" --payload-dir "${special_file}" 2>&1)"; then
  printf 'FAIL: special files must be rejected.\n' >&2
  exit 1
fi
if [[ "${output}" != *"apps/portal/runtime.pipe"* || "${output}" != *"[rule:"* ]]; then
  printf 'FAIL: special-file rejection must report its path and rule.\n' >&2
  exit 1
fi

safe_placeholders="${temp_dir}/safe-placeholders"
create_payload "${safe_placeholders}"
cat > "${safe_placeholders}/docs/operations/configuration.md" <<'EOF'
RVT__SENDGRID_API_KEY=
RVT__SENDGRID_API_KEY=${RVT__SENDGRID_API_KEY:-}
apiKey: <placeholder>
password: changeme
GitHub Actions reads ${{ secrets.SENDGRID_API_KEY }} at runtime.
Example URL: https://example.invalid/path
EOF
cat > "${safe_placeholders}/apps/portal/Options.cs" <<'EOF'
ClientSecret = configuration["RVT__MICROSOFT_CLIENT_SECRET"];
cancellationToken = cancellationTokenSource.Token;
_connectionString = options.ConnectionString;
EOF
cat > "${safe_placeholders}/tests/ConnectionFixture.cs" <<'EOF'
const string fixture = "Host=localhost;Database=test;Username=test;Password=top-secret";
EOF
cat >> "${safe_placeholders}/docs/operations/configuration.md" <<'EOF'
Disposable documentation example:
Host=localhost;Database=example;Username=example;Password=<pw>
EOF
mkdir -p "${safe_placeholders}/apps/monitors/VendorMonitorTests/testdata"
cat > "${safe_placeholders}/apps/monitors/VendorMonitorTests/testdata/device.json" <<'EOF'
{
  "apiSecret": "fixture-secret",
  "token": "fixture-token",
  "device": "test-device"
}
EOF
mkdir -p "${safe_placeholders}/apps/portal/docs/deploy"
cat > "${safe_placeholders}/apps/portal/docs/deploy/set-dev-secrets.ps1" <<'EOF'
$password = ConvertTo-SecureString $GraphClientSecret -AsPlainText -Force
$SmtpPassword = Read-Host "SMTP password"
EOF
"${verifier}" --payload-dir "${safe_placeholders}"

pem_marker="-----BEGIN PRIVATE KEY-----"
assert_rejected_secret \
  pem-private-key \
  deploy/inline-key.txt \
  "${pem_marker}
ZmFrZS1wcml2YXRlLWtleS1tYXRlcmlhbA==
-----END PRIVATE KEY-----" \
  "PEM private key" \
  "${pem_marker}"

github_token="ghp_""0123456789abcdefghijklmnopqrstuvwxyz"
assert_rejected_secret \
  github-token \
  config/github.txt \
  "token=${github_token}" \
  "GitHub token" \
  "${github_token}"

aws_access_key="AKIA""0123456789ABCDEF"
assert_rejected_secret \
  aws-access-key \
  config/aws.txt \
  "accessKey=${aws_access_key}" \
  "AWS access key" \
  "${aws_access_key}"

slack_token="xoxb""-123456789012-123456789012-abcdefghijklmnopqrstuvwx"
assert_rejected_secret \
  slack-token \
  config/slack.txt \
  "token=${slack_token}" \
  "Slack token" \
  "${slack_token}"

connection_password="SuperSecret123!"
assert_rejected_secret \
  connection-string-password \
  config/database.txt \
  "Host=db;Username=app;Password=${connection_password};Database=rvt" \
  "connection-string password" \
  "${connection_password}"

url_password="UrlPassword123!"
assert_rejected_secret \
  credentialed-url \
  config/service.txt \
  "endpoint=https://service-user:${url_password}@example.invalid/api" \
  "credentialed URL" \
  "${url_password}"

config_password="actual-config-password"
assert_rejected_secret \
  configuration-connection-password \
  apps/portal/appsettings.json \
  "{\"ConnectionStrings\":{\"Default\":\"Host=db;Username=app;Password=${config_password}\"}}" \
  "connection-string password" \
  "${config_password}"

generic_secret="non-placeholder-secret-value"
assert_rejected_secret \
  recognized-secret-assignment \
  config/runtime.txt \
  "RVT__SENDGRID_API_KEY=${generic_secret}" \
  "secret assignment" \
  "${generic_secret}"

printf 'Client release path-boundary fixtures verified.\n'
