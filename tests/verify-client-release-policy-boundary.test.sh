#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
policy_boundary_verifier="$root_dir/scripts/verify-client-release-policy-boundary.sh"
temp_dir="$(mktemp -d)"
readonly prepared_client_source_commit="0123456789abcdef0123456789abcdef01234567"
readonly prepared_client_source_timestamp="2026-07-31T12:34:56+00:00"

cleanup() {
  rm -rf "$temp_dir"
}
trap cleanup EXIT

assert_success() {
  local description="$1"
  shift

  if ! "$@"; then
    printf 'FAIL: %s.\n' "$description" >&2
    exit 1
  fi
}

assert_failure() {
  local description="$1"
  local expected_output="$2"
  shift 2
  local output

  if output="$("$@" 2>&1)"; then
    printf 'FAIL: %s.\n' "$description" >&2
    exit 1
  fi

  if ! grep -Fqx "$expected_output" <<< "$output"; then
    printf 'FAIL: %s; expected %q, got %q.\n' \
      "$description" "$expected_output" "$output" >&2
    exit 1
  fi
}

policy_path="$temp_dir/client-release-exclusions.txt"
release_root="$temp_dir/release"
mkdir -p "$release_root"

printf 'policy\n' > "$policy_path"
assert_success \
  'source policy should retain the Dockerignore correlation path' \
  "$policy_boundary_verifier" "$policy_path" "$release_root"

rm "$policy_path"
assert_failure \
  'a missing policy without a prepared-client marker must fail' \
  'FAIL: client-release policy is missing and RELEASE_SOURCE.json does not identify a prepared client release.' \
  "$policy_boundary_verifier" "$policy_path" "$release_root"

cat > "$release_root/RELEASE_SOURCE.json" <<EOF
{"sourceCommit":"$prepared_client_source_commit","sourceCommitTimestamp":"$prepared_client_source_timestamp"}
EOF
assert_success \
  'a prepared client marker should allow the source-only policy correlation to skip' \
  "$policy_boundary_verifier" "$policy_path" "$release_root"

printf 'Client release policy-boundary fixtures verified.\n'
