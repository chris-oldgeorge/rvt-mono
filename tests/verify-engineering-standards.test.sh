#!/usr/bin/env bash
set -euo pipefail

source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-engineering-standards.XXXXXX")"
temp_root="$(cd "$temp_root" && pwd -P)"
fake_bin="$temp_root/fake tools"
sleep 30 &
harness_pid_probe=$!
harness_pid="$(ps -o ppid= -p "$harness_pid_probe" | tr -d '[:space:]')"
kill "$harness_pid_probe"
wait "$harness_pid_probe" 2>/dev/null || true
case_number=0
last_repo=
last_output=
last_status=0
dotnet_log_token=dotnet
clock_source_path='src/Clock.cs'
clock_log_argument='<src/Clock.cs>'
portal_head_line=$'export const headOnly = true;\n'
changed_portal_line=$'export const changed = 43;\n'
portal_head_message='requested Portal head'
csharp_head_message='requested C# head'
hour_to_day_substitution='s/public int Hour/public int Day/'
hour_to_head_substitution='s/public int Hour/public int HeadHour/'
second_to_millisecond_substitution='s/public int Second/public int Millisecond/'
minimal_project_xml='<Project Sdk="Microsoft.NET.Sdk"></Project>'
private_package_json='{"private":true}'
empty_package_lock_json='{"lockfileVersion":3,"packages":{}}'
tracked_portal_inputs_message='tracked Portal inputs'
approved_baseline_message='approved baseline'
changed_surface_message='changed surface'
symlink_message='symlink'
unexpected_path_message='unexpected path'
ide0055_line_one='1:IDE0055'
ide0055_line_five='5:IDE0055'
empty_baseline_json='{"version":1,"entries":[]}'
clock_baseline_one_json='{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":1}]}'
clock_baseline_two_json='{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":2}]}'
clock_exception_json='{"version":1,"exceptions":[{"id":"EX-CLOCK","ruleId":"IDE0055","owner":"team","path":"src/Clock.cs","justification":"temporary migration","introducedOn":"2026-07-28","reviewOn":"2026-08-30","removalCondition":"remove diagnostic","validation":"verified"}]}'

# Test overrides model local invocation by default, even when this harness is
# itself launched by a CI runner. Focused cases opt back into GitHub Actions.
export GITHUB_ACTIONS=false

cleanup() {
  sleep 30 &
  cleanup_pid_probe=$!
  current_shell_pid="$(ps -o ppid= -p "$cleanup_pid_probe" | tr -d '[:space:]')"
  kill "$cleanup_pid_probe"
  wait "$cleanup_pid_probe" 2>/dev/null || true
  if [[ "$current_shell_pid" != "$harness_pid" ]]; then
    return 0
  fi
  rm -rf "$temp_root"
}
trap cleanup EXIT

fail() {
  printf 'FAIL: %s\n' "$*" >&2
  exit 1
}

assert_status() {
  local expected="$1"
  [[ "$last_status" -eq "$expected" ]] ||
    fail "${last_repo}: expected exit $expected, got $last_status: $last_output"
}

assert_output() {
  [[ "$last_output" == *"$1"* ]] ||
    fail "expected output to contain '$1': $last_output"
}

assert_output_absent() {
  [[ "$last_output" != *"$1"* ]] ||
    fail "expected output not to contain '$1': $last_output"
}

assert_log_contains() {
  [[ -f "$last_repo/tool.log" ]] || fail "tool log is missing"
  rg -F -q -- "$1" "$last_repo/tool.log" ||
    fail "expected tool log to contain '$1': $(<"$last_repo/tool.log")"
}

assert_log_absent() {
  [[ ! -e "$last_repo/tool.log" ]] ||
    fail "source tools unexpectedly ran: $(<"$last_repo/tool.log")"
}

assert_range_rejected_before_provisioning() {
  assert_status 2
  assert_output "caller dependency input"
  assert_log_absent
  local worktree_count
  worktree_count="$(git -C "$last_repo" worktree list --porcelain | rg -c '^worktree ')"
  [[ "$worktree_count" -eq 1 ]] ||
    fail "rejected range verification leaked an isolated worktree"
}

write_json() {
  local destination="$1"
  local contents="$2"
  printf '%s\n' "$contents" > "$destination"
}

start_lock_sentinel() {
  local token="$1"
  node -e 'setInterval(() => {}, 1000)' \
    "rvt-standards-lock-sentinel=$token" &
  lock_sentinel_pid=$!
  for _ in {1..200}; do
    if ps -ww -o command= -p "$lock_sentinel_pid" |
      rg -F -q -- "rvt-standards-lock-sentinel=$token"; then
      return 0
    fi
    sleep 0.01
  done
  kill "$lock_sentinel_pid" 2>/dev/null || true
  wait "$lock_sentinel_pid" 2>/dev/null || true
  fail "lock sentinel did not expose its ownership token"
}

create_repo() {
  local name="$1"
  case_number=$((case_number + 1))
  last_repo="$temp_root/${case_number}-${name}"
  mkdir -p "$last_repo/scripts/engineering-standards"
  mkdir -p "$last_repo/tests/fixtures/engineering-standards"
  mkdir -p "$last_repo/apps/portal/RvtPortal.Client/src"
  mkdir -p "$last_repo/apps/portal/RvtPortal.Client/public/images"
  mkdir -p "$last_repo/src"

  cp "$source_root/scripts/engineering-standards/model.mjs" \
    "$last_repo/scripts/engineering-standards/model.mjs"
  cp "$source_root/scripts/engineering-standards/verify.mjs" \
    "$last_repo/scripts/engineering-standards/verify.mjs"
  cp "$source_root/scripts/verify-engineering-standards.sh" \
    "$last_repo/scripts/verify-engineering-standards.sh"

  printf '<Solution />\n' > "$last_repo/Rvt.Mono.slnx"
  cat > "$last_repo/src/Clock.cs" <<'EOF'
namespace Sample;

public sealed class Clock
{
    public int Hour { get; init; }

    public int Minute { get; init; }

    public int Second { get; init; }
}
EOF
  cat > "$last_repo/apps/portal/RvtPortal.Client/src/app.ts" <<'EOF'
export const answer = 42;
EOF
  cp "$source_root/tests/fixtures/engineering-standards/exceptions.json" \
    "$last_repo/exceptions.json"
  printf '\0binary portal asset\n' \
    > "$last_repo/apps/portal/RvtPortal.Client/public/images/logo.png"
  write_json "$last_repo/baseline.json" '{"version":1,"generatedAt":"2026-07-27","entries":[]}'

  git -C "$last_repo" init -q -b main
  git -C "$last_repo" config user.name "Verifier Test"
  git -C "$last_repo" config user.email "verifier@example.test"
  git -C "$last_repo" add .
  git -C "$last_repo" commit -q -m "initial"
  git -C "$last_repo" update-ref refs/remotes/origin/main HEAD
}

run_verify() {
  local dotnet_command
  local prettier_command
  local eslint_command
  dotnet_command="${RVT_TEST_DOTNET_COMMAND:-$(
    printf '["%s","--sentinel","two words"]' "$fake_bin/fake-dotnet"
  )}"
  prettier_command="$(printf '["%s","--sentinel","two words"]' "$fake_bin/fake-prettier")"
  eslint_command="$(printf '["%s","--sentinel","two words"]' "$fake_bin/fake-eslint")"

  set +e
  last_output="$(
    cd "$last_repo"
    RVT_STANDARDS_DOTNET_COMMAND="$dotnet_command" \
    RVT_STANDARDS_PRETTIER_COMMAND="$prettier_command" \
    RVT_STANDARDS_ESLINT_COMMAND="$eslint_command" \
    RVT_STANDARDS_BASELINE_PATH="${RVT_TEST_BASELINE_PATH:-$last_repo/baseline.json}" \
    RVT_STANDARDS_EXCEPTIONS_PATH="${RVT_TEST_EXCEPTIONS_PATH:-$last_repo/exceptions.json}" \
    RVT_FAKE_DELAY="${RVT_FAKE_DELAY:-}" \
    RVT_FAKE_STARTED_MARKER="${RVT_FAKE_STARTED_MARKER:-}" \
    RVT_FAKE_EXPECT_CONTENT="${RVT_FAKE_EXPECT_CONTENT:-}" \
    RVT_FAKE_LOG="$last_repo/tool.log" \
    RVT_FAKE_DOTNET_REPORT="${RVT_FAKE_DOTNET_REPORT:-}" \
    RVT_FAKE_DOTNET_CANONICAL_REPORT="${RVT_FAKE_DOTNET_CANONICAL_REPORT:-0}" \
    RVT_FAKE_DOTNET_STATUS="${RVT_FAKE_DOTNET_STATUS:-0}" \
    RVT_FAKE_DOTNET_FAIL_PHASE="${RVT_FAKE_DOTNET_FAIL_PHASE:-}" \
    RVT_FAKE_DOTNET_SKIP_REPORT="${RVT_FAKE_DOTNET_SKIP_REPORT:-0}" \
    RVT_FAKE_PRETTIER_STATUS="${RVT_FAKE_PRETTIER_STATUS:-0}" \
    RVT_FAKE_PRETTIER_OUTPUT="${RVT_FAKE_PRETTIER_OUTPUT:-}" \
    RVT_FAKE_ESLINT_STATUS="${RVT_FAKE_ESLINT_STATUS:-0}" \
    RVT_FAKE_ESLINT_REPORT="${RVT_FAKE_ESLINT_REPORT:-}" \
    RVT_FAKE_ESLINT_CANONICAL_REPORT="${RVT_FAKE_ESLINT_CANONICAL_REPORT:-0}" \
      scripts/verify-engineering-standards.sh "$@" 2>&1
  )"
  last_status=$?
  set -e
}

run_verify_default_commands() {
  set +e
  last_output="$(
    cd "$last_repo"
    PATH="$default_bin:$PATH" \
    RVT_STANDARDS_BASELINE_PATH="${RVT_TEST_BASELINE_PATH:-$last_repo/baseline.json}" \
    RVT_STANDARDS_EXCEPTIONS_PATH="${RVT_TEST_EXCEPTIONS_PATH:-$last_repo/exceptions.json}" \
    RVT_FAKE_EXPECT_CONTENT="${RVT_FAKE_EXPECT_CONTENT:-}" \
    RVT_FAKE_REQUIRE_ASSET="${RVT_FAKE_REQUIRE_ASSET:-}" \
    RVT_FAKE_LOG="$last_repo/tool.log" \
    RVT_FAKE_DOTNET_REPORT="" RVT_FAKE_DOTNET_CANONICAL_REPORT=0 RVT_FAKE_DOTNET_STATUS=0 \
    RVT_FAKE_DOTNET_FAIL_PHASE="" RVT_FAKE_DOTNET_SKIP_REPORT=0 \
    RVT_FAKE_PRETTIER_STATUS=0 RVT_FAKE_PRETTIER_OUTPUT="" \
    RVT_FAKE_ESLINT_STATUS=0 RVT_FAKE_ESLINT_REPORT="" RVT_FAKE_ESLINT_CANONICAL_REPORT=0 \
      scripts/verify-engineering-standards.sh "$@" 2>&1
  )"
  last_status=$?
  set -e
}

run_verify_in_github_actions_with_override() {
  local override_name="$1"
  local override_value="$2"

  set +e
  last_output="$(
    cd "$last_repo"
    unset RVT_STANDARDS_DOTNET_COMMAND
    unset RVT_STANDARDS_PRETTIER_COMMAND
    unset RVT_STANDARDS_ESLINT_COMMAND
    unset RVT_STANDARDS_BASELINE_PATH
    unset RVT_STANDARDS_EXCEPTIONS_PATH
    export GITHUB_ACTIONS=true
    export "${override_name}=${override_value}"
    scripts/verify-engineering-standards.sh --working-tree 2>&1
  )"
  last_status=$?
  set -e
}

write_dotnet_report() {
  local destination="$1"
  local file_path="$2"
  shift 2
  {
    printf '[{"FilePath":"%s","FileChanges":[' "$file_path"
    local separator=
    local item
    for item in "$@"; do
      local line="${item%%:*}"
      local rule="${item#*:}"
      printf '%s{"LineNumber":%s,"DiagnosticId":"%s","FormatDescription":"diagnostic %s"}' \
        "$separator" "$line" "$rule" "$rule"
      separator=,
    done
    printf ']}]\n'
  } > "$destination"
}

write_eslint_report() {
  local destination="$1"
  local file_path="$2"
  local line="$3"
  cat > "$destination" <<EOF
[{"filePath":"$file_path","messages":[{"ruleId":"@typescript-eslint/no-unused-vars","severity":2,"message":"unused","line":$line}]}]
EOF
}

mkdir -p "$fake_bin"
default_bin="$temp_root/default tools"
mkdir -p "$default_bin"
cat > "$fake_bin/fake-dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ -n "${RVT_FAKE_STARTED_MARKER:-}" ]]; then
  : > "$RVT_FAKE_STARTED_MARKER"
fi
if [[ -n "${RVT_FAKE_DELAY:-}" ]]; then
  sleep "$RVT_FAKE_DELAY"
fi
if [[ -n "${RVT_FAKE_EXPECT_CONTENT:-}" ]] &&
   ! rg -F -q -- "$RVT_FAKE_EXPECT_CONTENT" src/Clock.cs; then
  exit 7
fi
if [[ -n "${RVT_FAKE_REQUIRE_ASSET:-}" &&
      ! -f "$RVT_FAKE_REQUIRE_ASSET" ]]; then
  printf 'missing required no-restore asset: %s\n' "$RVT_FAKE_REQUIRE_ASSET" >&2
  exit 8
fi
printf 'dotnet' >> "$RVT_FAKE_LOG"
for argument in "$@"; do
  printf ' <%s>' "$argument" >> "$RVT_FAKE_LOG"
done
printf '\n' >> "$RVT_FAKE_LOG"

phase=
report=
while (($#)); do
  case "$1" in
    whitespace|style|analyzers) phase="$1" ;;
    --report)
      shift
      report="${1:-}"
      ;;
  esac
  shift || true
done

if [[ "$RVT_FAKE_DOTNET_SKIP_REPORT" != 1 &&
      -n "$report" &&
      "$RVT_FAKE_DOTNET_CANONICAL_REPORT" == 1 &&
      ( -z "$RVT_FAKE_DOTNET_FAIL_PHASE" || "$phase" == "$RVT_FAKE_DOTNET_FAIL_PHASE" ) ]]; then
  printf '[{"FilePath":"%s/src/Clock.cs","FileChanges":[{"LineNumber":5,"DiagnosticId":"IDE0055","FormatDescription":"canonical diagnostic"}]}]\n' \
    "$(pwd -P)" > "$report"
elif [[ "$RVT_FAKE_DOTNET_SKIP_REPORT" != 1 &&
      -n "$report" &&
      -n "$RVT_FAKE_DOTNET_REPORT" &&
      ( -z "$RVT_FAKE_DOTNET_FAIL_PHASE" || "$phase" == "$RVT_FAKE_DOTNET_FAIL_PHASE" ) ]]; then
  cp "$RVT_FAKE_DOTNET_REPORT" "$report"
fi
if [[ -n "$RVT_FAKE_DOTNET_FAIL_PHASE" && "$phase" == "$RVT_FAKE_DOTNET_FAIL_PHASE" ]]; then
  exit "$RVT_FAKE_DOTNET_STATUS"
fi
exit 0
EOF

cat > "$fake_bin/fake-prettier" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
unsupported_svg=0
printf 'prettier cwd=<%s>' "$PWD" >> "$RVT_FAKE_LOG"
for argument in "$@"; do
  printf ' <%s>' "$argument" >> "$RVT_FAKE_LOG"
  if [[ "$argument" == *.svg ]]; then
    unsupported_svg=1
  fi
done
printf '\n' >> "$RVT_FAKE_LOG"
if [[ "$unsupported_svg" -eq 1 ]]; then
  printf '[error] No parser could be inferred for SVG input.\n' >&2
  exit 2
fi
if [[ -n "$RVT_FAKE_PRETTIER_OUTPUT" ]]; then
  printf '%s\n' "$RVT_FAKE_PRETTIER_OUTPUT"
fi
exit "$RVT_FAKE_PRETTIER_STATUS"
EOF

cat > "$fake_bin/fake-eslint" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
has_ignored_schema=0
has_ordinary_warning=0
suppress_ignored_warning=0
quiet=0
printf 'eslint cwd=<%s>' "$PWD" >> "$RVT_FAKE_LOG"
for argument in "$@"; do
  printf ' <%s>' "$argument" >> "$RVT_FAKE_LOG"
  case "$argument" in
    --no-warn-ignored) suppress_ignored_warning=1 ;;
    --quiet) quiet=1 ;;
    src/api/schema.d.ts) has_ignored_schema=1 ;;
    src/warning.ts) has_ordinary_warning=1 ;;
  esac
done
printf '\n' >> "$RVT_FAKE_LOG"
if [[ -n "$RVT_FAKE_ESLINT_REPORT" ]]; then
  cat "$RVT_FAKE_ESLINT_REPORT"
elif [[ "$RVT_FAKE_ESLINT_CANONICAL_REPORT" == 1 ]]; then
  printf '[{"filePath":"%s/src/app.ts","messages":[{"ruleId":"@typescript-eslint/no-unused-vars","severity":2,"message":"canonical diagnostic","line":2}]}]\n' \
    "$(pwd -P)"
elif [[ "$has_ignored_schema" -eq 1 && "$suppress_ignored_warning" -eq 0 ]]; then
  printf '[{"filePath":"%s/src/api/schema.d.ts","messages":[{"ruleId":null,"fatal":false,"severity":1,"message":"File ignored because of a matching ignore pattern. Use \\"--no-ignore\\" to disable file ignore settings or use \\"--no-warn-ignored\\" to suppress this warning.","nodeType":null}]}]\n' "$PWD"
elif [[ "$has_ordinary_warning" -eq 1 && "$quiet" -eq 0 ]]; then
  printf '[{"filePath":"%s/src/warning.ts","messages":[{"ruleId":"react-refresh/only-export-components","fatal":false,"severity":1,"message":"Fast refresh only works when a file only exports components.","line":1}]}]\n' "$PWD"
else
  printf '[]\n'
fi
exit "$RVT_FAKE_ESLINT_STATUS"
EOF
chmod +x "$fake_bin/fake-dotnet" "$fake_bin/fake-prettier" "$fake_bin/fake-eslint"
ln -s "$fake_bin/fake-dotnet" "$default_bin/dotnet"

# GitHub Actions must reject every command and policy-path override before
# policy loading or source tools can be influenced.
for override_name in \
  RVT_STANDARDS_DOTNET_COMMAND \
  RVT_STANDARDS_PRETTIER_COMMAND \
  RVT_STANDARDS_ESLINT_COMMAND \
  RVT_STANDARDS_BASELINE_PATH \
  RVT_STANDARDS_EXCEPTIONS_PATH; do
  create_repo "github-actions-${override_name}"
  run_verify_in_github_actions_with_override "$override_name" "unsafe"
  assert_status 2
  assert_output "$override_name"
  assert_output "GITHUB_ACTIONS=true"
  assert_log_absent
done

# The same test-only injection boundary remains available outside CI.
create_repo local-override-support
printf '\n' >> "$last_repo/src/Clock.cs"
printf '%s' "$changed_portal_line" \
  >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
run_verify --working-tree
assert_status 0
assert_log_contains "dotnet <--sentinel> <two words>"
assert_log_contains "prettier cwd=<$last_repo/apps/portal/RvtPortal.Client>"
assert_log_contains "eslint cwd=<$last_repo/apps/portal/RvtPortal.Client>"

# Clean changed-scope checks do not invoke source tools.
create_repo clean
run_verify --working-tree
assert_status 0
assert_log_absent

# Working-tree mode combines staged, unstaged, and untracked content relative to HEAD.
create_repo working-tree
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
sed -i.bak 's/public int Minute/public int Month/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
printf 'namespace Sample;\npublic sealed class NewClock {}\n' > "$last_repo/src/NewClock.cs"
run_verify --working-tree
assert_status 0
assert_log_contains "dotnet <--sentinel> <two words> <format> <Rvt.Mono.slnx> <whitespace>"
assert_log_contains "<style>"
assert_log_contains "<analyzers>"
assert_log_contains "$clock_log_argument"
assert_log_contains "<src/NewClock.cs>"

# Staged and unstaged hunks are independently part of the changed surface.
create_repo staged-and-unstaged-hunks
write_json "$last_repo/baseline.json" \
  "$clock_baseline_two_json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
sed -i.bak 's/public int Minute/public int Month/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "$ide0055_line_five" "7:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "src/Clock.cs:5"
assert_output "src/Clock.cs:7"

# Metacharacters inside a valid JSON command token remain one inert argument.
create_repo valid-metacharacter-token
printf '\n' >> "$last_repo/src/Clock.cs"
metacharacter_token=';touch unsafe-valid-token'
metacharacter_command="$(
  node -e 'console.log(JSON.stringify(process.argv.slice(1)))' \
    "$fake_bin/fake-dotnet" "$metacharacter_token"
)"
RVT_TEST_DOTNET_COMMAND="$metacharacter_command" run_verify --working-tree
assert_status 0
assert_log_contains "<$metacharacter_token>"
[[ ! -e "$last_repo/unsafe-valid-token" ]] ||
  fail "valid JSON command token evaluated shell syntax"

# Portal TypeScript changes run Prettier and ESLint from the client directory.
create_repo portal
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
run_verify --working-tree
assert_status 0
assert_log_contains "prettier cwd=<$last_repo/apps/portal/RvtPortal.Client>"
assert_log_contains "<--list-different> <src/app.ts>"
assert_log_contains "eslint cwd=<$last_repo/apps/portal/RvtPortal.Client>"
assert_log_contains "<--format> <json>"
assert_log_contains "<src/app.ts>"

# Removing --no-warn-ignored from the production ESLint invocation makes the
# fake pinned-engine boundary emit ESLint 9.39.4's nonfatal null-rule notice,
# which the verifier must continue to reject rather than normalize heuristically.
create_repo eslint-structurally-ignored-generated-file
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/src/api"
printf 'export interface GeneratedSchema {}\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/api/schema.d.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/api/schema.d.ts
git -C "$last_repo" commit -q -m "tracked generated schema"
run_verify --all
assert_status 0
assert_log_contains "<--no-warn-ignored>"
assert_log_contains "<src/api/schema.d.ts>"

# Adding --quiet to the production invocation would make this fake pinned-engine
# boundary suppress a legitimate non-ignored warning and this changed-file check fail.
create_repo eslint-ordinary-warning
printf 'export const warning = 1;\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/warning.ts"
run_verify --working-tree
assert_status 1
assert_output "react-refresh/only-export-components"

# Every Prettier-supported Portal text extension is formatted; explicitly selected
# unsupported text remains validated without being sent to Prettier.
create_repo portal-supported-extensions
declare -a prettier_extensions=(
  css html js jsx json md mdx scss ts tsx yaml yml
  mjs cjs mts cts graphql gql
)
for extension in "${prettier_extensions[@]}"; do
  candidate="$last_repo/apps/portal/RvtPortal.Client/src/extension.$extension"
  case "$extension" in
    json) printf '{}\n' > "$candidate" ;;
    yaml|yml) printf 'value: true\n' > "$candidate" ;;
    graphql|gql) printf 'query Viewer { viewer { id } }\n' > "$candidate" ;;
    *) printf 'export const extensionValue = true;\n' > "$candidate" ;;
  esac
done
printf '<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/extension.svg"
run_verify --working-tree
assert_status 0
for extension in "${prettier_extensions[@]}"; do
  assert_log_contains "<src/extension.$extension>"
done
if rg -F -q -- "<src/extension.svg>" "$last_repo/tool.log"; then
  fail "Prettier received explicitly selected unsupported SVG input"
fi
for extension in ts tsx mts cts; do
  rg -F "eslint cwd=" "$last_repo/tool.log" |
    rg -F -q -- "<src/extension.$extension>" ||
    fail "ESLint did not receive TypeScript extension .$extension"
done

# A diagnostic in a new file is rejected even when its whole-path count is stable.
create_repo new-file-diagnostic
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Added.cs","count":1}]}'
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
printf 'namespace Sample;\npublic sealed class Added {}\n' > "$last_repo/src/Added.cs"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Added.cs" "$ide0055_line_one"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"
assert_output "src/Added.cs:1"

# A diagnostic on a changed line is rejected at a stable total.
create_repo changed-line
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"
assert_output "src/Clock.cs:5"

# An unchanged-line legacy diagnostic is allowed only at or below baseline.
create_repo unchanged-line
cp "$source_root/tests/fixtures/engineering-standards/baseline.json" "$last_repo/baseline.json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 0

# A whole-path increase fails with baseline and observed counts.
create_repo increase
cp "$source_root/tests/fixtures/engineering-standards/baseline.json" "$last_repo/baseline.json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "$ide0055_line_five" "7:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "baseline=1"
assert_output "observed=2"

# A decrease is reported without failing.
create_repo decrease
write_json "$last_repo/baseline.json" \
  "$clock_baseline_two_json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 0
assert_output "decrease"
assert_output "baseline=2"
assert_output "observed=1"

# Invalid or expired policy documents fail closed before any source tool runs.
create_repo expired-exception
write_json "$last_repo/exceptions.json" \
  '{"version":1,"exceptions":[{"id":"EX-OLD","ruleId":"IDE0055","owner":"team","path":"src/Clock.cs","justification":"legacy","introducedOn":"2026-07-01","reviewOn":"2026-07-26","removalCondition":"remove","validation":"verified"}]}'
printf '\n' >> "$last_repo/src/Clock.cs"
run_verify --working-tree
assert_status 2
assert_output "Expired exception EX-OLD"
assert_log_absent

create_repo malformed-baseline
write_json "$last_repo/baseline.json" '{"version":1,"entries":[{"count":-1}]}'
printf '\n' >> "$last_repo/src/Clock.cs"
run_verify --working-tree
assert_status 2
assert_log_absent

# Intermediate symlinks cannot escape source or policy containment.
create_repo applicable-intermediate-symlink
mkdir -p "$last_repo/src/nested"
cp "$last_repo/src/Clock.cs" "$last_repo/src/nested/Through.cs"
git -C "$last_repo" add src/nested/Through.cs
git -C "$last_repo" commit -q -m "tracked nested source"
mkdir -p "$temp_root/applicable-outside"
cp "$last_repo/src/Clock.cs" "$temp_root/applicable-outside/Through.cs"
rm -rf "$last_repo/src/nested"
ln -s "$temp_root/applicable-outside" "$last_repo/src/nested"
run_verify --all
assert_status 2
assert_output "$symlink_message"
assert_log_absent

create_repo ignored-intermediate-symlink
mkdir -p "$last_repo/src/node_modules"
cp "$last_repo/src/Clock.cs" "$last_repo/src/node_modules/Ignored.cs"
git -C "$last_repo" add src/node_modules/Ignored.cs
git -C "$last_repo" commit -q -m "tracked ignored nested source"
mkdir -p "$temp_root/ignored-outside"
cp "$last_repo/src/Clock.cs" "$temp_root/ignored-outside/Ignored.cs"
rm -rf "$last_repo/src/node_modules"
ln -s "$temp_root/ignored-outside" "$last_repo/src/node_modules"
run_verify --all
assert_status 2
assert_output "$symlink_message"
assert_log_absent

create_repo baseline-intermediate-symlink
mkdir -p "$temp_root/baseline-policy-outside"
write_json "$temp_root/baseline-policy-outside/baseline.json" \
  "$empty_baseline_json"
ln -s "$temp_root/baseline-policy-outside" "$last_repo/policy-link"
RVT_TEST_BASELINE_PATH="$last_repo/policy-link/baseline.json" \
  run_verify --working-tree
assert_status 2
assert_output "$symlink_message"
assert_log_absent

create_repo exceptions-intermediate-symlink
mkdir -p "$temp_root/exceptions-policy-outside"
write_json "$temp_root/exceptions-policy-outside/exceptions.json" \
  '{"version":1,"exceptions":[]}'
ln -s "$temp_root/exceptions-policy-outside" "$last_repo/policy-link"
RVT_TEST_EXCEPTIONS_PATH="$last_repo/policy-link/exceptions.json" \
  run_verify --working-tree
assert_status 2
assert_output "$symlink_message"
assert_log_absent

# Initialization creates an absent baseline once and refuses every existing file.
create_repo initialize
RVT_TEST_BASELINE_PATH="$last_repo/initialized.json" run_verify --all --initialize-baseline
assert_status 0
[[ -f "$last_repo/initialized.json" ]] || fail "initialization did not create baseline"
RVT_TEST_BASELINE_PATH="$last_repo/initialized.json" run_verify --all --initialize-baseline
assert_status 1
assert_output "already exists"
write_json "$last_repo/existing-empty.json" "$empty_baseline_json"
RVT_TEST_BASELINE_PATH="$last_repo/existing-empty.json" run_verify --all --initialize-baseline
assert_status 1

create_repo initialize-with-diagnostics
diagnostic_baseline="$last_repo/diagnostic-baseline.json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_TEST_BASELINE_PATH="$diagnostic_baseline" \
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --all --initialize-baseline
assert_status 0
node -e '
  const document = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
  if (document.entries.length !== 1 || document.entries[0].count !== 1) process.exit(1);
' "$diagnostic_baseline" || fail "initialization did not record observed diagnostics"

create_repo codepoint-baseline-order
ordered_baseline="$last_repo/ordered.json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "5:ä-rule" "7:Z-rule" "9:a-rule"
RVT_TEST_BASELINE_PATH="$ordered_baseline" \
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --all --initialize-baseline
assert_status 0
node -e '
  const document = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
  const actual = document.entries.map((entry) => entry.ruleId).join(",");
  if (actual !== "Z-rule,a-rule,ä-rule") process.exit(1);
' "$ordered_baseline" || fail "baseline order is not locale-independent code-point order"

create_repo initialize-race
race_baseline="$last_repo/race-baseline.json"
(
  trap - EXIT
  RVT_FAKE_DELAY=0.1 RVT_TEST_BASELINE_PATH="$race_baseline" \
    run_verify --all --initialize-baseline
  printf '%s\n' "$last_status" > "$last_repo/race-status-1"
) &
race_pid_1=$!
(
  trap - EXIT
  RVT_FAKE_DELAY=0.1 RVT_TEST_BASELINE_PATH="$race_baseline" \
    run_verify --all --initialize-baseline
  printf '%s\n' "$last_status" > "$last_repo/race-status-2"
) &
race_pid_2=$!
wait "$race_pid_1"
wait "$race_pid_2"
race_statuses="$(sort "$last_repo/race-status-1" "$last_repo/race-status-2" | tr '\n' ' ')"
[[ "$race_statuses" == "0 1 " ]] ||
  fail "concurrent initialization must succeed exactly once, got: $race_statuses"

# Inventory update refuses increases, preserves bytes, then atomically writes a decrease.
create_repo update
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
cp "$last_repo/baseline.json" "$last_repo/baseline.before"
cp "$last_repo/exceptions.json" "$last_repo/exceptions.before"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "$ide0055_line_five" "7:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --all --update-baseline
assert_status 1
cmp -s "$last_repo/baseline.before" "$last_repo/baseline.json" ||
  fail "refused baseline update changed the file"
cmp -s "$last_repo/exceptions.before" "$last_repo/exceptions.json" ||
  fail "baseline update widened exceptions"

# A semantically unchanged update preserves the installed document across dates.
write_json "$last_repo/baseline.json" \
  '{"version":1,"generatedAt":"2000-01-01","entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":1}]}'
cp "$last_repo/baseline.json" "$last_repo/baseline.before"
baseline_hash_before="$(git hash-object "$last_repo/baseline.json")"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --all --update-baseline
assert_status 0
baseline_hash_after="$(git hash-object "$last_repo/baseline.json")"
set +e
cmp -s "$last_repo/baseline.before" "$last_repo/baseline.json"
baseline_cmp_status=$?
set -e
[[ "$baseline_cmp_status" -eq 0 && "$baseline_hash_before" == "$baseline_hash_after" ]] ||
  fail "unchanged cross-date baseline update rewrote bytes: cmp=$baseline_cmp_status before=$baseline_hash_before after=$baseline_hash_after"

write_json "$last_repo/baseline.json" \
  "$clock_baseline_two_json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --all --update-baseline
assert_status 0
node -e '
  const fs = require("fs");
  const document = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
  if (document.entries.length !== 1 || document.entries[0].count !== 1) process.exit(1);
' "$last_repo/baseline.json" || fail "baseline decrease was not written"
[[ -z "$(find "$last_repo" -maxdepth 1 -name '*.tmp' -print)" ]] ||
  fail "atomic update left a temporary file"
cmp -s "$last_repo/exceptions.before" "$last_repo/exceptions.json" ||
  fail "baseline update widened exceptions"

# Concurrent updates cannot reinstall a stale, wider candidate.
create_repo update-race
write_json "$last_repo/baseline.json" \
  "$clock_baseline_two_json"
write_dotnet_report "$last_repo/slow.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
(
  trap - EXIT
  RVT_FAKE_STARTED_MARKER="$last_repo/slow.started" RVT_FAKE_DELAY=0.2 \
  RVT_FAKE_DOTNET_REPORT="$last_repo/slow.json" \
  RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
    run_verify --all --update-baseline
  printf '%s\n' "$last_status" > "$last_repo/slow.status"
) &
slow_pid=$!
for _ in {1..200}; do
  [[ -e "$last_repo/slow.started" ]] && break
  sleep 0.01
done
[[ -e "$last_repo/slow.started" ]] || fail "slow update never started"
(
  trap - EXIT
  run_verify --all --update-baseline
  printf '%s\n' "$last_status" > "$last_repo/fast.status"
) &
fast_pid=$!
wait "$fast_pid"
wait "$slow_pid"
[[ "$(<"$last_repo/fast.status")" == "0" ]] ||
  fail "clean concurrent update did not succeed"
[[ "$(<"$last_repo/slow.status")" == "1" ]] ||
  fail "stale wider concurrent update was not refused"
node -e '
  const document = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
  if (document.entries.length !== 0) process.exit(1);
' "$last_repo/baseline.json" || fail "concurrent update widened installed baseline"
run_verify --all --update-baseline
assert_status 0

# Update lock identity follows the canonical baseline target, not a worktree Git dir.
create_repo canonical-baseline-lock
canonical_lock="$last_repo/baseline.json.update.lock"
mkdir "$canonical_lock"
live_token="live-owner-token"
start_lock_sentinel "$live_token"
live_sentinel_pid="$lock_sentinel_pid"
write_json "$canonical_lock/owner.json" \
  "{\"version\":2,\"pid\":$$,\"sentinelPid\":$live_sentinel_pid,\"token\":\"$live_token\",\"createdAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
(
  trap - EXIT
  LC_ALL=C run_verify --all --update-baseline
  printf '%s\n' "$last_status" > "$last_repo/live-owner.status"
) &
live_waiter=$!
sleep 1.2
kill -0 "$live_waiter" 2>/dev/null ||
  fail "live tokenized sentinel owner was stolen under a different locale"
[[ -d "$canonical_lock" ]] ||
  fail "live canonical baseline lock was removed"
kill "$live_sentinel_pid"
wait "$live_sentinel_pid" 2>/dev/null || true
wait "$live_waiter"
[[ "$(<"$last_repo/live-owner.status")" == "0" ]] ||
  fail "live-lock waiter did not finish after owner release"

# A demonstrably dead owner is reclaimed and the new owner cleans up its lock.
create_repo dead-baseline-lock-owner
dead_lock="$last_repo/baseline.json.update.lock"
dead_token="dead-owner-token"
start_lock_sentinel "$dead_token"
dead_sentinel_pid="$lock_sentinel_pid"
kill "$dead_sentinel_pid"
wait "$dead_sentinel_pid" 2>/dev/null || true
mkdir "$dead_lock"
write_json "$dead_lock/owner.json" \
  "{\"version\":2,\"pid\":$$,\"sentinelPid\":$dead_sentinel_pid,\"token\":\"$dead_token\",\"createdAt\":\"2000-01-01T00:00:00Z\"}"
run_verify --all --update-baseline
assert_status 0
[[ ! -e "$dead_lock" ]] ||
  fail "dead canonical baseline lock was not reclaimed and cleaned"

# A same-window numeric PID collision without the random sentinel token is stale.
# This harness is the recorded sentinel PID, so it is unambiguously alive and its
# command line unambiguously lacks the token: the verdict is a property of the
# observation, not of the clock. The ownership answer is therefore read from the
# run's outcome rather than from how quickly it returned -- mistaking the recycled
# PID for the live owner exhausts the bounded acquisition retries and reports a
# lock timeout instead of reclaiming.
create_repo reused-pid-baseline-lock-owner
reused_lock="$last_repo/baseline.json.update.lock"
mkdir "$reused_lock"
write_json "$reused_lock/owner.json" \
  "{\"version\":2,\"pid\":$$,\"sentinelPid\":$$,\"token\":\"collision-token\",\"createdAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
run_verify --all --update-baseline
[[ "$last_output" != *"Timed out waiting for baseline-update lock"* ]] ||
  fail "same numeric PID without the sentinel token was treated as the owner"
[[ "$last_status" -eq 0 ]] ||
  fail "same-window numeric PID collision was not reclaimed: $last_output"
[[ ! -e "$reused_lock" ]] ||
  fail "same-window numeric PID collision lock was not cleaned"

# An incomplete lock-creation crash is recoverable only after its bounded grace.
create_repo partial-baseline-lock-owner
partial_lock="$last_repo/baseline.json.update.lock"
mkdir "$partial_lock"
touch -t 200001010000 "$partial_lock"
run_verify --all --update-baseline
assert_status 0
[[ ! -e "$partial_lock" ]] ||
  fail "stale metadata-free baseline lock was not reclaimed"

# A hostile PATH entry cannot interfere with stale-lock reclamation.
create_repo hostile-path-stale-baseline-lock-owner
hostile_lock="$last_repo/baseline.json.update.lock"
hostile_ps_marker="$last_repo/hostile-ps.marker"
mkdir "$hostile_lock"
write_json "$hostile_lock/owner.json" \
  "{\"version\":2,\"pid\":$$,\"sentinelPid\":$$,\"token\":\"collision-token\",\"createdAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
mkdir "$last_repo/hostile-bin"
cat > "$last_repo/hostile-bin/ps" <<EOF
#!/usr/bin/env bash
touch "$hostile_ps_marker"
exit 2
EOF
chmod +x "$last_repo/hostile-bin/ps"
PATH="$last_repo/hostile-bin:$PATH" run_verify --all --update-baseline
assert_status 0
[[ ! -e "$hostile_ps_marker" ]] ||
  fail "verifier resolved ps from hostile PATH"
[[ ! -e "$hostile_lock" ]] ||
  fail "stale lock was not reclaimed with hostile PATH"

# A live sentinel with an unavailable fixed probe fails closed and retains its lock.
create_repo unavailable-process-probe-baseline-lock-owner
unavailable_probe_lock="$last_repo/baseline.json.update.lock"
mkdir "$unavailable_probe_lock"
write_json "$unavailable_probe_lock/owner.json" \
  "{\"version\":2,\"pid\":$$,\"sentinelPid\":$$,\"token\":\"unobservable-token\",\"createdAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
node -e '
  const fs = require("fs");
  const file = process.argv[1];
  const source = fs.readFileSync(file, "utf8");
  const updated = source.replace(
    /const psCommand = process\.platform === '\''darwin'\'' \? '\''\/bin\/ps'\'' : '\''\/usr\/bin\/ps'\'';/,
    "const psCommand = \"/nonexistent/rvt-process-probe\";"
  );
  if (updated === source) process.exit(1);
  fs.writeFileSync(file, updated);
' "$last_repo/scripts/engineering-standards/verify.mjs" ||
  fail "could not install unavailable process-probe fixture"
run_verify --all --update-baseline
assert_status 2
assert_output "observe baseline-update lock sentinel"
[[ -d "$unavailable_probe_lock" ]] ||
  fail "unavailable fixed process probe reclaimed a live lock"
rm "$unavailable_probe_lock/owner.json"
rmdir "$unavailable_probe_lock"

# Once a successor reclaims a dead sentinel, a stopped predecessor cannot
# resume and mutate with its stale token.
create_repo baseline-lock-successor-safety
node -e '
  const fs = require("fs");
  const entries = [];
  for (let index = 0; index < 15000; index += 1) {
    entries.push({
      tool: "dotnet-format-style",
      ruleId: `R${String(index).padStart(5, "0")}`,
      path: "src/Clock.cs",
      count: 1
    });
  }
  fs.writeFileSync(process.argv[1], `${JSON.stringify({ version: 1, entries })}\n`);
' "$last_repo/baseline.json"
successor_lock="$last_repo/baseline.json.update.lock"
(
  trap - EXIT
  run_verify --all --update-baseline
  printf '%s\n' "$last_status" > "$last_repo/predecessor.status"
  printf '%s' "$last_output" > "$last_repo/predecessor.output"
) &
predecessor_controller=$!
predecessor_pid=
predecessor_sentinel_pid=
for _ in {1..2000}; do
  if [[ -f "$successor_lock/owner.json" ]]; then
    predecessor_owner_json="$(<"$successor_lock/owner.json")"
    if [[ "$predecessor_owner_json" =~ \"pid\":([0-9]+) ]]; then
      predecessor_pid="${BASH_REMATCH[1]}"
      if [[ "$predecessor_owner_json" =~ \"sentinelPid\":([0-9]+) ]]; then
        predecessor_sentinel_pid="${BASH_REMATCH[1]}"
        kill -STOP "$predecessor_pid"
        break
      fi
    fi
  fi
  sleep 0.001
done
[[ -n "$predecessor_pid" && -n "$predecessor_sentinel_pid" ]] ||
  fail "predecessor never acquired the update lock"
kill "$predecessor_sentinel_pid"
wait "$predecessor_sentinel_pid" 2>/dev/null || true
run_verify --all --update-baseline
assert_status 0
kill -CONT "$predecessor_pid"
wait "$predecessor_controller"
[[ "$(<"$last_repo/predecessor.status")" == "2" ]] ||
  fail "stale predecessor resumed and mutated after its successor"
rg -F -q "lock ownership" "$last_repo/predecessor.output" ||
  fail "stale predecessor did not report lost lock ownership"
node -e '
  const document = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
  if (document.entries.length !== 0) process.exit(1);
' "$last_repo/baseline.json" ||
  fail "stale predecessor overwrote the successor baseline"

# Explicit and automatic committed ranges resolve without shell interpretation.
create_repo explicit-range
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "change"
run_verify --base "$base_revision" --head HEAD
assert_status 0
assert_log_contains "$clock_log_argument"

# A committed range that removes code from a retained source file has no
# new-side ranges, but remains a valid changed surface.
create_repo explicit-range-deletion-only-hunk
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak '/public int Second/d' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "remove source member"
run_verify --base "$base_revision" --head HEAD
assert_status 0
assert_log_contains "$clock_log_argument"

create_repo auto-feature
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "feature"
run_verify --base auto --head HEAD
assert_status 0
assert_log_contains "$clock_log_argument"

create_repo auto-main
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "main change"
git -C "$last_repo" update-ref refs/remotes/origin/main HEAD
run_verify --base auto --head HEAD
assert_status 0
assert_log_contains "$clock_log_argument"

# A committed range analyzes the exact requested head, never checkout or dirty content.
create_repo exact-range-head
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "requested head"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
sed -i.bak 's/public int Hour/public int DirtyStagedHour/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
sed -i.bak 's/public int Minute/public int DirtyUnstagedMinute/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
RVT_FAKE_EXPECT_CONTENT="HeadHour" \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 0
rg -F -q "DirtyStagedHour" "$last_repo/src/Clock.cs" ||
  fail "range verification modified staged checkout content"
rg -F -q "DirtyUnstagedMinute" "$last_repo/src/Clock.cs" ||
  fail "range verification modified unstaged checkout content"
worktree_count="$(git -C "$last_repo" worktree list --porcelain | rg -c '^worktree ')"
[[ "$worktree_count" -eq 1 ]] ||
  fail "range verification leaked an isolated worktree"

# Committed-range policy comes from the requested head, never the caller checkout.
create_repo exact-range-policy-head
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "$approved_baseline_message"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "requested source head"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 0
requested_head_output="$last_output"
git -C "$last_repo" checkout -q --detach "$base_revision"
write_json "$last_repo/baseline.json" "$empty_baseline_json"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 0
[[ "$last_output" == "$requested_head_output" ]] ||
  fail "the same committed range changed result with the caller checkout"

# A baseline increase in a committed range is a policy violation even when it
# would make the simultaneous diagnostic pass.
create_repo exact-range-baseline-widening
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add baseline.json src/Clock.cs
git -C "$last_repo" commit -q -m "widen baseline with source"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "Baseline policy"

# A dirty working-tree baseline cannot widen the trusted HEAD policy.
create_repo working-tree-baseline-widening
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
run_verify --working-tree
assert_status 1
assert_output "Baseline policy"
assert_log_absent

# A new exact-path exception cannot authorize a source violation in the same range.
create_repo exact-range-same-change-exception
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/exceptions.json" \
  '{"version":1,"exceptions":[{"id":"EX-NEW","ruleId":"IDE0055","owner":"team","path":"src/Clock.cs","justification":"temporary migration","introducedOn":"2026-07-28","reviewOn":"2026-08-30","removalCondition":"remove diagnostic","validation":"verified"}]}'
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add exceptions.json src/Clock.cs
git -C "$last_repo" commit -q -m "add exception with violation"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "$changed_surface_message"

# An exception that is unchanged across a committed range remains active.
create_repo exact-range-existing-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve existing exception"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "change excepted source"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 0

# Removing an exception in a committed range takes effect immediately.
create_repo exact-range-removed-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve removable exception"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/exceptions.json" '{"version":1,"exceptions":[]}'
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add exceptions.json src/Clock.cs
git -C "$last_repo" commit -q -m "remove exception with source change"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "$changed_surface_message"

# Editing any authorization field creates a new, not-yet-trusted exception.
create_repo exact-range-edited-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve original exception"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/exceptions.json" \
  '{"version":1,"exceptions":[{"id":"EX-CLOCK","ruleId":"IDE0055","owner":"other-team","path":"src/Clock.cs","justification":"temporary migration","introducedOn":"2026-07-28","reviewOn":"2026-08-30","removalCondition":"remove diagnostic","validation":"verified"}]}'
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add exceptions.json src/Clock.cs
git -C "$last_repo" commit -q -m "edit exception owner with source change"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "$changed_surface_message"

# Working-tree policy uses the same current/trusted exception intersection.
create_repo working-tree-existing-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve working-tree exception"
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 0

create_repo working-tree-removed-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve removable working-tree exception"
write_json "$last_repo/exceptions.json" '{"version":1,"exceptions":[]}'
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"

create_repo working-tree-edited-exception
write_json "$last_repo/exceptions.json" \
  "$clock_exception_json"
git -C "$last_repo" add exceptions.json
git -C "$last_repo" commit -q -m "approve original working-tree exception"
write_json "$last_repo/exceptions.json" \
  '{"version":1,"exceptions":[{"id":"EX-CLOCK","ruleId":"IDE0055","owner":"other-team","path":"src/Clock.cs","justification":"temporary migration","introducedOn":"2026-07-28","reviewOn":"2026-08-30","removalCondition":"remove diagnostic","validation":"verified"}]}'
sed -i.bak "$hour_to_day_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"

# A baseline decrease is enforced from the requested head and remains allowed.
create_repo exact-range-baseline-decrease
write_json "$last_repo/baseline.json" \
  "$clock_baseline_two_json"
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "legacy baseline"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/baseline.json" \
  "$clock_baseline_one_json"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add baseline.json src/Clock.cs
git -C "$last_repo" commit -q -m "ratchet baseline down"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
write_dotnet_report "$last_repo/dotnet.json" "$clock_source_path" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 0
assert_output_absent "Baseline decrease:"

# Trusted transition policy is mandatory, valid JSON, and a regular file.
create_repo exact-range-missing-trusted-policy
git -C "$last_repo" rm -q baseline.json
git -C "$last_repo" commit -q -m "missing base policy"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/baseline.json" "$empty_baseline_json"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add baseline.json src/Clock.cs
git -C "$last_repo" commit -q -m "restore policy with source"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
run_verify --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "Policy path is missing"
assert_log_absent

create_repo exact-range-malformed-trusted-policy
write_json "$last_repo/baseline.json" '{"version":1,"entries":[{"count":-1}]}'
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "malformed base policy"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/baseline.json" "$empty_baseline_json"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add baseline.json src/Clock.cs
git -C "$last_repo" commit -q -m "repair policy with source"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
run_verify --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "baseline entry"
assert_log_absent

create_repo exact-range-symlinked-trusted-policy
write_json "$last_repo/policy-target.json" "$empty_baseline_json"
rm "$last_repo/baseline.json"
ln -s policy-target.json "$last_repo/baseline.json"
git -C "$last_repo" add baseline.json policy-target.json
git -C "$last_repo" commit -q -m "symlinked base policy"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
rm "$last_repo/baseline.json"
write_json "$last_repo/baseline.json" "$empty_baseline_json"
sed -i.bak "$second_to_millisecond_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add baseline.json src/Clock.cs
git -C "$last_repo" commit -q -m "replace policy with source"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
run_verify --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "$symlink_message"
assert_log_absent

create_repo exact-range-ignored-csharp
mkdir -p "$last_repo/src/node_modules"
printf 'namespace Ignored;\n' > "$last_repo/src/node_modules/Ignored.cs"
git -C "$last_repo" add src/node_modules/Ignored.cs
git -C "$last_repo" commit -q -m "tracked ignored C#"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf 'namespace IgnoredChanged;\n' > "$last_repo/src/node_modules/Ignored.cs"
git -C "$last_repo" add src/node_modules/Ignored.cs
git -C "$last_repo" commit -q -m "changed ignored C#"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_absent

create_repo exact-range-portal-binaries
printf '\0icon\n' > "$last_repo/apps/portal/RvtPortal.Client/src/icon.ico"
printf '\0font\n' > "$last_repo/apps/portal/RvtPortal.Client/src/font.woff2"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/icon.ico \
  apps/portal/RvtPortal.Client/src/font.woff2
git -C "$last_repo" commit -q -m "tracked Portal binaries"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '\0changed icon\n' > "$last_repo/apps/portal/RvtPortal.Client/src/icon.ico"
printf '\0changed font\n' > "$last_repo/apps/portal/RvtPortal.Client/src/font.woff2"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/icon.ico \
  apps/portal/RvtPortal.Client/src/font.woff2
git -C "$last_repo" commit -q -m "changed Portal binaries"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_absent

# Unsupported Portal text is still a validated source input, but an SVG-only
# committed range does not require or provision a Prettier installation.
create_repo exact-range-unsupported-portal-text
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/public/rvt-mark.svg"
git -C "$last_repo" add apps/portal/RvtPortal.Client/public/rvt-mark.svg
git -C "$last_repo" commit -q -m "tracked unsupported Portal text"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_absent

# Default range commands receive compatible ignored prerequisites.
create_repo exact-range-default-assets
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" \
  '{"private":true,"devDependencies":{"eslint":"1.0.0","prettier":"1.0.0"}}'
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  "$empty_package_lock_json"
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json src/Sample.csproj
git -C "$last_repo" commit -q -m "tracked dependency inputs"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
mkdir -p "$last_repo/src/obj"
: > "$last_repo/src/obj/restore.sentinel"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
printf '%s' "$portal_head_line" \
  >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add src/Clock.cs apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "requested source head"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
sed -i.bak 's/public int Hour/public int DirtyHour/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
RVT_FAKE_EXPECT_CONTENT="HeadHour" RVT_FAKE_REQUIRE_ASSET="src/obj/restore.sentinel" \
  run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_contains "$dotnet_log_token"
assert_log_contains "prettier cwd="
assert_log_contains "eslint cwd="

# macOS exposes temporary directories through both lexical (/var) and physical
# (/private/var) paths. Materialized revisions must use the physical root so
# tool reports remain contained for both .NET and Portal diagnostics.
create_repo exact-range-canonical-dotnet-report
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "$csharp_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
RVT_FAKE_DOTNET_CANONICAL_REPORT=1 \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "changed surface dotnet-format-style"
assert_log_contains "$dotnet_log_token"

create_repo exact-range-canonical-portal-report
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "$portal_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
RVT_FAKE_ESLINT_CANONICAL_REPORT=1 RVT_FAKE_ESLINT_STATUS=1 \
  run_verify --base "$base_revision" --head "$head_revision"
assert_status 1
assert_output "changed surface eslint"
assert_log_contains "prettier cwd="
assert_log_contains "eslint cwd="

# The repository-local NuGet package cache is a generated restore prerequisite,
# not a dirty dependency input, even when it contains dependency-shaped files.
create_repo exact-range-ignored-nuget-cache
printf 'apps/.nuget-packages/\n' > "$last_repo/.gitignore"
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add .gitignore src/Sample.csproj
git -C "$last_repo" commit -q -m "track local NuGet cache policy"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "$csharp_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/src/obj"
mkdir -p "$last_repo/apps/.nuget-packages/example/1.0.0/build"
write_json "$last_repo/apps/.nuget-packages/example/1.0.0/build/example.targets" \
  '<Project />'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_contains "$dotnet_log_token"

# Sonar's repository-local tool directory is generated analysis state, not a
# dirty dependency input, even when installed tools contain MSBuild targets.
create_repo exact-range-ignored-sonar-tools
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "track project input"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "$csharp_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/src/obj"
mkdir -p "$last_repo/.sonar/.store/scanner/1.0.0/build"
write_json "$last_repo/.sonar/.store/scanner/1.0.0/build/scanner.targets" \
  '<Project />'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 0
assert_log_contains "$dotnet_log_token"

create_repo exact-range-missing-assets
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "tracked project"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "requested head"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "range prerequisite"

create_repo exact-range-incompatible-assets
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "base project inputs"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/src/Sample.csproj" \
  '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><LangVersion>preview</LangVersion></PropertyGroup></Project>'
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Sample.csproj src/Clock.cs
git -C "$last_repo" commit -q -m "incompatible requested head"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
mkdir -p "$last_repo/src/obj"
: > "$last_repo/src/obj/restore.sentinel"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "incompatible"

create_repo exact-range-packages-lock-incompatible
write_json "$last_repo/src/Sample.csproj" "$minimal_project_xml"
write_json "$last_repo/src/packages.lock.json" '{"version":1}'
git -C "$last_repo" add src/Sample.csproj src/packages.lock.json
git -C "$last_repo" commit -q -m "locked restore inputs"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/src/packages.lock.json" '{"version":2}'
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/packages.lock.json src/Clock.cs
git -C "$last_repo" commit -q -m "changed restore lock"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
mkdir -p "$last_repo/src/obj"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output "packages.lock.json"

create_repo exact-range-npmrc-incompatible
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" "$private_package_json"
write_json "$last_repo/apps/portal/RvtPortal.Client/.npmrc" 'registry=https://base.invalid'
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/.npmrc
git -C "$last_repo" commit -q -m "portal install inputs"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/apps/portal/RvtPortal.Client/.npmrc" 'registry=https://head.invalid'
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/.npmrc \
  apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "changed portal install input"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
git -C "$last_repo" checkout -q --detach "$base_revision"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_status 2
assert_output ".npmrc"

# Caller assets are valid only when every relevant index/worktree input matches
# caller HEAD. Dirty dependency inputs fail before assets are linked or tools run.
create_repo exact-range-dirty-package-lock
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" "$private_package_json"
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  "$empty_package_lock_json"
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json
git -C "$last_repo" commit -q -m "$tracked_portal_inputs_message"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "$portal_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  '{"lockfileVersion":3,"packages":{"dirty":{}}}'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output "package-lock.json"

create_repo exact-range-dirty-npmrc
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" "$private_package_json"
write_json "$last_repo/apps/portal/RvtPortal.Client/.npmrc" 'registry=https://clean.invalid'
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/.npmrc
git -C "$last_repo" commit -q -m "$tracked_portal_inputs_message"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "$portal_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
write_json "$last_repo/apps/portal/RvtPortal.Client/.npmrc" \
  'registry=https://dirty.invalid'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output ".npmrc"

create_repo exact-range-dirty-project
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "tracked project input"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "$csharp_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/src/obj"
write_json "$last_repo/src/Sample.csproj" \
  '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><Dirty>true</Dirty></PropertyGroup></Project>'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output "Sample.csproj"

create_repo exact-range-staged-dependency
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" "$private_package_json"
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  "$empty_package_lock_json"
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json
git -C "$last_repo" commit -q -m "$tracked_portal_inputs_message"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "$portal_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  '{"lockfileVersion":3,"packages":{"staged":{}}}'
git -C "$last_repo" add apps/portal/RvtPortal.Client/package-lock.json
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output "package-lock.json"

create_repo exact-range-deleted-dependency
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" "$private_package_json"
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  "$empty_package_lock_json"
git -C "$last_repo" add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json
git -C "$last_repo" commit -q -m "$tracked_portal_inputs_message"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
printf '%s' "$portal_head_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/app.ts
git -C "$last_repo" commit -q -m "$portal_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin"
ln -s "$fake_bin/fake-prettier" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/prettier"
ln -s "$fake_bin/fake-eslint" \
  "$last_repo/apps/portal/RvtPortal.Client/node_modules/.bin/eslint"
rm "$last_repo/apps/portal/RvtPortal.Client/package-lock.json"
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output "package-lock.json"

create_repo exact-range-untracked-packages-lock
write_json "$last_repo/src/Sample.csproj" \
  "$minimal_project_xml"
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "tracked project input"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak "$hour_to_head_substitution" "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "$csharp_head_message"
head_revision="$(git -C "$last_repo" rev-parse HEAD)"
mkdir -p "$last_repo/src/obj"
write_json "$last_repo/src/packages.lock.json" '{"version":1}'
run_verify_default_commands --base "$base_revision" --head "$head_revision"
assert_range_rejected_before_provisioning
assert_output "packages.lock.json"

# Inventory mode ratchets report-based whitespace, ESLint, and Prettier findings.
create_repo inventory
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-whitespace","ruleId":"IDE0055","path":"src/Clock.cs","count":1},{"tool":"eslint","ruleId":"@typescript-eslint/no-unused-vars","path":"apps/portal/RvtPortal.Client/src/app.ts","count":1},{"tool":"prettier","ruleId":"prettier/format","path":"apps/portal/RvtPortal.Client/src/app.ts","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "$ide0055_line_five"
write_eslint_report "$last_repo/eslint.json" \
  "$last_repo/apps/portal/RvtPortal.Client/src/app.ts" 1
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=1 \
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" RVT_FAKE_ESLINT_STATUS=1 \
RVT_FAKE_PRETTIER_OUTPUT="src/app.ts" RVT_FAKE_PRETTIER_STATUS=1 \
  run_verify --all
assert_status 0

# Changed-scope whitespace and Prettier failures are immediate policy violations.
create_repo immediate-whitespace
printf '\n' >> "$last_repo/src/Clock.cs"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "11:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "whitespace"

create_repo immediate-prettier
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_OUTPUT="src/app.ts" RVT_FAKE_PRETTIER_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "Prettier"

# Supported GraphQL still fails closed for formatting differences and
# syntax/internal status 2 after unsupported text is filtered.
create_repo supported-prettier-graphql-format
printf 'query Viewer{viewer{id}}\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/query.graphql"
RVT_FAKE_PRETTIER_OUTPUT="src/query.graphql" RVT_FAKE_PRETTIER_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "Prettier"

create_repo supported-prettier-graphql-syntax
printf 'query Viewer {\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/query.graphql"
RVT_FAKE_PRETTIER_STATUS=2 run_verify --working-tree
assert_status 2
assert_output "internal/configuration status 2"

create_repo prettier-unexpected-skipped-svg-report
printf 'query Viewer { viewer { id } }\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/query.graphql"
printf '<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/skipped.svg"
RVT_FAKE_PRETTIER_OUTPUT="src/skipped.svg" RVT_FAKE_PRETTIER_STATUS=1 \
  run_verify --working-tree
assert_status 2
assert_output "$unexpected_path_message"

# Missing/malformed accepted reports and invalid test overrides are tool failures.
create_repo abnormal-whitespace-missing-report
printf '\n' >> "$last_repo/src/Clock.cs"
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=7 \
RVT_FAKE_DOTNET_SKIP_REPORT=1 run_verify --working-tree
assert_status 2
assert_output "report"

create_repo abnormal-whitespace-malformed-report
printf '\n' >> "$last_repo/src/Clock.cs"
write_json "$last_repo/dotnet.json" 'not-json'
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=7 \
  run_verify --working-tree
assert_status 2
assert_output "malformed"

create_repo abnormal-whitespace-unexpected-report
printf 'namespace Sample;\npublic sealed class Other {}\n' > "$last_repo/src/Other.cs"
git -C "$last_repo" add src/Other.cs
git -C "$last_repo" commit -q -m "other source"
printf '\n' >> "$last_repo/src/Clock.cs"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Other.cs" "$ide0055_line_one"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=7 \
  run_verify --working-tree
assert_status 2
assert_output "$unexpected_path_message"

create_repo prettier-internal-no-filenames
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_STATUS=2 run_verify --working-tree
assert_status 2

create_repo prettier-internal-with-filenames
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_STATUS=2 RVT_FAKE_PRETTIER_OUTPUT="src/app.ts" \
  run_verify --working-tree
assert_status 2

create_repo eslint-internal-with-diagnostics
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_eslint_report "$last_repo/eslint.json" \
  "$last_repo/apps/portal/RvtPortal.Client/src/app.ts" 1
RVT_FAKE_ESLINT_STATUS=2 RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" \
  run_verify --working-tree
assert_status 2
assert_output "internal/configuration status 2"

create_repo eslint-fatal-parser-diagnostic
printf 'export const broken = ;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_json "$last_repo/eslint.json" \
  "[{\"filePath\":\"$last_repo/apps/portal/RvtPortal.Client/src/app.ts\",\"messages\":[{\"ruleId\":null,\"fatal\":true,\"severity\":2,\"message\":\"Parsing error: Expression expected.\",\"line\":2}]}]"
RVT_FAKE_ESLINT_STATUS=1 RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" \
  run_verify --working-tree
assert_status 1
assert_output "eslint/fatal-parse-error"

create_repo eslint-nonfatal-null-rule
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_json "$last_repo/eslint.json" \
  "[{\"filePath\":\"$last_repo/apps/portal/RvtPortal.Client/src/app.ts\",\"messages\":[{\"ruleId\":null,\"fatal\":false,\"severity\":1,\"message\":\"Malformed nonfatal diagnostic.\",\"line\":2}]}]"
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" run_verify --working-tree
assert_status 2
assert_output "ESLint report is invalid"
assert_output "rule ID"

create_repo missing-report
printf '\n' >> "$last_repo/src/Clock.cs"
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
RVT_FAKE_DOTNET_SKIP_REPORT=1 run_verify --working-tree
assert_status 2
assert_output "report"

create_repo empty-nonzero-report
printf '\n' >> "$last_repo/src/Clock.cs"
write_json "$last_repo/dotnet.json" '[]'
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 2
assert_output "without diagnostics"

create_repo unexpected-report-path
printf 'namespace Sample;\npublic sealed class Other {}\n' > "$last_repo/src/Other.cs"
git -C "$last_repo" add src/Other.cs
git -C "$last_repo" commit -q -m "other source"
printf '\n' >> "$last_repo/src/Clock.cs"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Other.cs" "$ide0055_line_one"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 2
assert_output "$unexpected_path_message"

create_repo malformed-eslint
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_json "$last_repo/eslint.json" 'not-json'
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" RVT_FAKE_ESLINT_STATUS=1 \
  run_verify --working-tree
assert_status 2

create_repo empty-nonzero-eslint
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_ESLINT_STATUS=2 run_verify --working-tree
assert_status 2
assert_output "internal/configuration"

create_repo unexpected-eslint-path
printf 'export const other = 1;\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/other.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/other.ts
git -C "$last_repo" commit -q -m "other TypeScript source"
printf '%s' "$changed_portal_line" >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_eslint_report "$last_repo/eslint.json" \
  "$last_repo/apps/portal/RvtPortal.Client/src/other.ts" 1
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" RVT_FAKE_ESLINT_STATUS=1 \
  run_verify --working-tree
assert_status 2
assert_output "$unexpected_path_message"

create_repo invalid-command
printf '\n' >> "$last_repo/src/Clock.cs"
set +e
last_output="$(
  cd "$last_repo"
  RVT_STANDARDS_DOTNET_COMMAND='fake-dotnet --flag; touch unsafe' \
  RVT_STANDARDS_BASELINE_PATH="$last_repo/baseline.json" \
  RVT_STANDARDS_EXCEPTIONS_PATH="$last_repo/exceptions.json" \
    scripts/verify-engineering-standards.sh --working-tree 2>&1
)"
last_status=$?
set -e
assert_status 2
assert_output "JSON array"
[[ ! -e "$last_repo/unsafe" ]] || fail "override evaluated shell syntax"

# Exact root prefixes and the same nested directory segments are excluded.
create_repo ignored
mkdir -p "$last_repo/node_modules/pkg" "$last_repo/src/node_modules/pkg"
mkdir -p "$last_repo/src/cache/bin"
printf 'export const ignored = 1;\n' > "$last_repo/node_modules/pkg/root.ts"
printf 'export const ignored = 1;\n' > "$last_repo/src/node_modules/pkg/nested.ts"
printf 'namespace RootIgnored;\n' > "$last_repo/node_modules/pkg/RootIgnored.cs"
printf 'namespace NestedIgnored;\n' > "$last_repo/src/node_modules/pkg/NestedIgnored.cs"
printf 'namespace Sample;\n' > "$last_repo/src/cache/bin/Ignored.cs"
git -C "$last_repo" add .
git -C "$last_repo" commit -q -m "ignored files"
printf 'export const ignored = 2;\n' > "$last_repo/node_modules/pkg/root.ts"
printf 'export const ignored = 2;\n' > "$last_repo/src/node_modules/pkg/nested.ts"
printf 'namespace RootIgnoredChanged;\n' > "$last_repo/node_modules/pkg/RootIgnored.cs"
printf 'namespace NestedIgnoredChanged;\n' > "$last_repo/src/node_modules/pkg/NestedIgnored.cs"
printf 'namespace Changed;\n' > "$last_repo/src/cache/bin/Ignored.cs"
run_verify --working-tree
assert_status 0
assert_log_absent

create_repo binary-asset
printf '\0changed binary portal asset\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/public/images/logo.png"
printf '\0icon\n' > "$last_repo/apps/portal/RvtPortal.Client/src/icon.ico"
printf '\0font\n' > "$last_repo/apps/portal/RvtPortal.Client/src/font.woff2"
run_verify --working-tree
assert_status 0
assert_log_absent

# Exclusions cannot hide path escapes, and malformed Git paths fail closed.
create_repo ignored-path-escape
mkdir -p "$last_repo/src/node_modules"
printf 'outside\n' > "$temp_root/outside-source.cs"
ln -s "$temp_root/outside-source.cs" "$last_repo/src/node_modules/Escape.cs"
run_verify --working-tree
assert_status 2
assert_output "regular in-repository file"
assert_log_absent

create_repo malformed-git-path
bad_path=$'src/bad\nname.cs'
printf 'namespace Sample;\n' > "$last_repo/$bad_path"
run_verify --working-tree
assert_status 2
assert_output "quoted or malformed path"
assert_log_absent

# Outside-report paths, binary patches, and source paths without hunks fail closed.
create_repo outside-report
printf '\n' >> "$last_repo/src/Clock.cs"
write_dotnet_report "$last_repo/dotnet.json" "$temp_root/outside.cs" "$ide0055_line_one"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 2
assert_output "outside repository root"

create_repo binary
printf '\0binary\n' > "$last_repo/src/Clock.cs"
run_verify --working-tree
assert_status 2
assert_output "Binary"
assert_log_absent

create_repo binary-unsupported-portal-text
printf '\0binary SVG\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/public/rvt-mark.svg"
run_verify --working-tree
assert_status 2
assert_output "Binary"
assert_log_absent

create_repo inventory-binary-unsupported-portal-text
printf '\0tracked binary SVG\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/public/rvt-mark.svg"
git -C "$last_repo" add apps/portal/RvtPortal.Client/public/rvt-mark.svg
git -C "$last_repo" commit -q -m "tracked binary unsupported Portal text"
run_verify --all
assert_status 2
assert_output "Binary"
assert_log_absent

create_repo real-rename
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/RenamedClock.cs","count":1}]}'
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "approved renamed-path baseline"
git -C "$last_repo" mv src/Clock.cs src/RenamedClock.cs
sed -i.bak 's/public int Hour/public int RenamedHour/' "$last_repo/src/RenamedClock.cs"
rm "$last_repo/src/RenamedClock.cs.bak"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/RenamedClock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"
assert_log_contains "<src/RenamedClock.cs>"

create_repo real-copy
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/CopiedClock.cs","count":1}]}'
git -C "$last_repo" add baseline.json
git -C "$last_repo" commit -q -m "approved copied-path baseline"
cp "$last_repo/src/Clock.cs" "$last_repo/src/CopiedClock.cs"
git -C "$last_repo" add src/CopiedClock.cs
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/CopiedClock.cs" "$ide0055_line_five"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "$changed_surface_message"
assert_log_contains "<src/CopiedClock.cs>"

create_repo deletion-only
git -C "$last_repo" rm -q src/Clock.cs
run_verify --working-tree
assert_status 0
assert_log_absent

create_repo mode-only
chmod +x "$last_repo/src/Clock.cs"
run_verify --working-tree
assert_status 2
assert_output "new-side hunk"
assert_log_absent

# Invocation shape is exclusive and invalid revisions are invocation failures.
create_repo arguments
run_verify --working-tree --all
assert_status 2
run_verify --base HEAD
assert_status 2
run_verify --base 'HEAD;touch unsafe' --head HEAD
assert_status 2
[[ ! -e "$last_repo/unsafe" ]] || fail "revision evaluated shell syntax"

printf 'PASS: engineering standards verifier scenarios\n'
