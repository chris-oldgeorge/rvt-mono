#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

dockerfile=.github/runner/Dockerfile
entrypoint=.github/runner/entrypoint.sh
compose_file=.github/runner/docker-compose.yml

for runner_file in "${dockerfile}" "${entrypoint}" "${compose_file}"; do
  if [[ ! -f "${runner_file}" ]]; then
    printf 'Missing self-hosted runner file: %s\n' "${runner_file}" >&2
    exit 1
  fi
done

test_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-sonar-runner.XXXXXX")"
trap 'rm -rf "${test_root}"' EXIT

config_json="${test_root}/compose.json"
docker compose -f "${compose_file}" config --format json >"${config_json}"
python3 - "${config_json}" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    compose = json.load(stream)

services = compose.get("services", {})
database = services.get("rvt-sonar-db")
runner = services.get("rvt-sonar-runner")
assert database is not None, "Compose must expose rvt-sonar-db as the database hostname"
assert runner is not None, "Compose must expose rvt-sonar-runner"
assert database.get("image") == "timescale/timescaledb:2.28.3-pg17", "unexpected database image"
assert database.get("hostname", "rvt-sonar-db") == "rvt-sonar-db", "unexpected database hostname"

environment = runner.get("environment", {})
if isinstance(environment, list):
    environment = dict(item.split("=", 1) for item in environment)
assert environment.get("RUNNER_LABELS") == "rvt-sonar", "runner must provide the rvt-sonar label"
assert "RUNNER_REGISTRATION_TOKEN" not in environment, "persistent runner service must not receive a registration token"

assert runner.get("dns") == ["1.1.1.1", "8.8.8.8"], "runner must use explicit public DNS resolvers"
assert runner.get("dns_opt") == ["timeout:2", "attempts:3"], "runner DNS retries must remain bounded"

depends_on = runner.get("depends_on", {})
assert depends_on.get("rvt-sonar-db", {}).get("condition") == "service_healthy", "runner must wait for the database health check"

volumes = runner.get("volumes", [])
assert any(
    volume.get("type") == "volume"
    and volume.get("source") == "runner-state"
    and volume.get("target") == "/runner-state"
    for volume in volumes
), "runner-state must be mounted as a named volume"
assert not any(volume.get("type") == "bind" for volume in volumes), "runner must not use bind mounts"
assert not runner.get("ports"), "runner must not publish ports"
assert not database.get("ports"), "database must not publish ports"
assert not runner.get("privileged", False), "runner must not be privileged"
assert not database.get("privileged", False), "database must not be privileged"
assert not any(
    "docker.sock" in str(value)
    for service in services.values()
    for volume in service.get("volumes", [])
    for value in volume.values()
), "Compose must not expose the Docker socket"
PY

python3 - "${dockerfile}" <<'PY'
import re
import sys

def assert_https_only_runner_download(dockerfile):
    runner_download_prefix = "RUN curl -fsSLo actions-runner.tar.gz " + chr(92)
    download_start = dockerfile.find(runner_download_prefix)
    assert download_start != -1, "runner archive download command must stay present"
    checksum_start = dockerfile.find("\n    && echo ", download_start)
    assert checksum_start != -1, "runner archive checksum verification must immediately follow the download"
    download_command = dockerfile[download_start:checksum_start]
    assert "\n      --proto '=https' " + chr(92) in download_command, "runner archive download must accept HTTPS only"
    assert "\n      --proto-redir '=https' " + chr(92) in download_command, "runner archive download redirects must remain HTTPS only"


dockerfile = open(sys.argv[1], encoding="utf-8").read()
assert "ARG RUNNER_VERSION=2.334.0" in dockerfile, "runner version must stay pinned"
assert "ARG RUNNER_SHA256=f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4" in dockerfile, "runner checksum must stay pinned"
assert_https_only_runner_download(dockerfile)
decoy_protocol = dockerfile.replace("--proto '=https'", "# --proto '=https'", 1) + "\nRUN echo \"--proto '=https'\"\n"
try:
    assert_https_only_runner_download(decoy_protocol)
except AssertionError:
    print("Rejected runner archive decoy protocol flag outside the download command.")
else:
    raise AssertionError("runner archive protocol guard accepted a decoy protocol flag outside the download command")
assert "libssl3t64" in dockerfile, "Ubuntu Noble runner image must install libssl3t64"
assert "liblttng-ust1t64" in dockerfile, "Ubuntu Noble runner image must install liblttng-ust1t64"
assert "libkrb5-3" in dockerfile, "runner image must install the Kerberos runtime"
assert "libgssapi-krb5-2" in dockerfile, "runner image must install the GSSAPI runtime"
assert "tzdata" in dockerfile, "runner image must install timezone data"
assert "libssl3" not in re.sub(r"libssl3t64", "", dockerfile), "Ubuntu Noble runner image must not retain libssl3"
assert not re.search(r"(?:^|[\s,])(docker(?:\.io)?|docker-ce|docker-cli)(?:[\s,\\]|$)", dockerfile, re.IGNORECASE), "runner image must not install Docker"
PY

fake_dist="${test_root}/distribution"
runner_home="${test_root}/runner-home"
runner_state="${test_root}/runner-state"
fake_bin="${test_root}/bin"
test_log="${test_root}/logs"
mkdir -p "${fake_dist}/bin" "${fake_bin}" "${test_log}"

cat >"${fake_dist}/config.sh" <<'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >>"${TEST_LOG}/config-arguments"
printf 'registration\n' >.runner
printf 'credentials\n' >.credentials
printf 'rsa\n' >.credentials_rsaparams
printf 'keep\n' >runtime-file
SCRIPT
cat >"${fake_dist}/run.sh" <<'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${RUNNER_REGISTRATION_TOKEN+x}" == x ]]; then
  printf 'registration token reached listener\n' >&2
  exit 1
fi
printf 'listener\n' >>"${TEST_LOG}/listener"
SCRIPT
cat >"${fake_dist}/bin/Runner.Listener" <<'SCRIPT'
#!/usr/bin/env bash
exit 0
SCRIPT
cat >"${fake_bin}/gosu" <<'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail
shift
exec "$@"
SCRIPT
chmod +x "${fake_dist}/config.sh" "${fake_dist}/run.sh" "${fake_dist}/bin/Runner.Listener" "${fake_bin}/gosu"

runner_environment=(
  "PATH=${fake_bin}:${PATH}"
  "RUNNER_DIST_ROOT=${fake_dist}"
  "RUNNER_HOME=${runner_home}"
  "RUNNER_STATE_ROOT=${runner_state}"
  "RUNNER_USER=$(id -un)"
  "RUNNER_URL=https://github.com/chris-oldgeorge/rvt-mono"
  "RUNNER_NAME=rvt-sonar-test"
  "RUNNER_LABELS=rvt-sonar"
  "TEST_LOG=${test_log}"
)

if env -u RUNNER_REGISTRATION_TOKEN "${runner_environment[@]}" "${entrypoint}" >"${test_root}/missing-token.out" 2>"${test_root}/missing-token.err"; then
  printf 'First runner start unexpectedly succeeded without a registration token\n' >&2
  exit 1
fi
grep -Fq 'Set a short-lived repository runner registration token for first start.' "${test_root}/missing-token.err"

env RUNNER_BOOTSTRAP_ONLY=true RUNNER_REGISTRATION_TOKEN=temporary-token "${runner_environment[@]}" "${entrypoint}"
[[ "$(wc -l <"${test_log}/config-arguments" | tr -d ' ')" == 1 ]]
grep -Fq -- '--token temporary-token' "${test_log}/config-arguments"
[[ ! -e "${test_log}/listener" ]]

persisted_files="$(find "${runner_state}" -type f -exec basename {} \; | sort)"
expected_files=(.credentials .credentials_rsaparams .runner)
expected_persisted_files=$'.credentials\n.credentials_rsaparams\n.runner'
[[ "${persisted_files}" == "${expected_persisted_files}" ]]
for registration_file in "${expected_files[@]}"; do
  [[ -L "${runner_home}/${registration_file}" ]]
  [[ "$(readlink "${runner_home}/${registration_file}")" == "${runner_state}/${registration_file}" ]]
done
[[ -f "${runner_home}/runtime-file" ]]
[[ ! -e "${runner_state}/runtime-file" ]]

rm -rf "${runner_home}"
env -u RUNNER_REGISTRATION_TOKEN "${runner_environment[@]}" "${entrypoint}"
[[ "$(wc -l <"${test_log}/config-arguments" | tr -d ' ')" == 1 ]]
[[ "$(wc -l <"${test_log}/listener" | tr -d ' ')" == 1 ]]
for registration_file in "${expected_files[@]}"; do
  [[ -L "${runner_home}/${registration_file}" ]]
  [[ "$(readlink "${runner_home}/${registration_file}")" == "${runner_state}/${registration_file}" ]]
done

printf 'verify-sonar-runner-stack: PASS\n'
