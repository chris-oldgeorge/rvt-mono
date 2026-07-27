#!/usr/bin/env bash
set -euo pipefail

source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-engineering-standards.XXXXXX")"
temp_root="$(cd "$temp_root" && pwd -P)"
fake_bin="$temp_root/fake tools"
case_number=0
last_repo=
last_output=
last_status=0

cleanup() {
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

assert_log_contains() {
  [[ -f "$last_repo/tool.log" ]] || fail "tool log is missing"
  rg -F -q -- "$1" "$last_repo/tool.log" ||
    fail "expected tool log to contain '$1': $(<"$last_repo/tool.log")"
}

assert_log_absent() {
  [[ ! -e "$last_repo/tool.log" ]] ||
    fail "source tools unexpectedly ran: $(<"$last_repo/tool.log")"
}

write_json() {
  local destination="$1"
  local contents="$2"
  printf '%s\n' "$contents" > "$destination"
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
    RVT_FAKE_DOTNET_STATUS="${RVT_FAKE_DOTNET_STATUS:-0}" \
    RVT_FAKE_DOTNET_FAIL_PHASE="${RVT_FAKE_DOTNET_FAIL_PHASE:-}" \
    RVT_FAKE_DOTNET_SKIP_REPORT="${RVT_FAKE_DOTNET_SKIP_REPORT:-0}" \
    RVT_FAKE_PRETTIER_STATUS="${RVT_FAKE_PRETTIER_STATUS:-0}" \
    RVT_FAKE_PRETTIER_OUTPUT="${RVT_FAKE_PRETTIER_OUTPUT:-}" \
    RVT_FAKE_ESLINT_STATUS="${RVT_FAKE_ESLINT_STATUS:-0}" \
    RVT_FAKE_ESLINT_REPORT="${RVT_FAKE_ESLINT_REPORT:-}" \
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
    RVT_FAKE_DOTNET_REPORT="" RVT_FAKE_DOTNET_STATUS=0 \
    RVT_FAKE_DOTNET_FAIL_PHASE="" RVT_FAKE_DOTNET_SKIP_REPORT=0 \
    RVT_FAKE_PRETTIER_STATUS=0 RVT_FAKE_PRETTIER_OUTPUT="" \
    RVT_FAKE_ESLINT_STATUS=0 RVT_FAKE_ESLINT_REPORT="" \
      scripts/verify-engineering-standards.sh "$@" 2>&1
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
printf 'prettier cwd=<%s>' "$PWD" >> "$RVT_FAKE_LOG"
for argument in "$@"; do
  printf ' <%s>' "$argument" >> "$RVT_FAKE_LOG"
done
printf '\n' >> "$RVT_FAKE_LOG"
if [[ -n "$RVT_FAKE_PRETTIER_OUTPUT" ]]; then
  printf '%s\n' "$RVT_FAKE_PRETTIER_OUTPUT"
fi
exit "$RVT_FAKE_PRETTIER_STATUS"
EOF

cat > "$fake_bin/fake-eslint" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'eslint cwd=<%s>' "$PWD" >> "$RVT_FAKE_LOG"
for argument in "$@"; do
  printf ' <%s>' "$argument" >> "$RVT_FAKE_LOG"
done
printf '\n' >> "$RVT_FAKE_LOG"
if [[ -n "$RVT_FAKE_ESLINT_REPORT" ]]; then
  cat "$RVT_FAKE_ESLINT_REPORT"
else
  printf '[]\n'
fi
exit "$RVT_FAKE_ESLINT_STATUS"
EOF
chmod +x "$fake_bin/fake-dotnet" "$fake_bin/fake-prettier" "$fake_bin/fake-eslint"
ln -s "$fake_bin/fake-dotnet" "$default_bin/dotnet"

# Clean changed-scope checks do not invoke source tools.
create_repo clean
run_verify --working-tree
assert_status 0
assert_log_absent

# Working-tree mode combines staged, unstaged, and untracked content relative to HEAD.
create_repo working-tree
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
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
assert_log_contains "<src/Clock.cs>"
assert_log_contains "<src/NewClock.cs>"

# Staged and unstaged hunks are independently part of the changed surface.
create_repo staged-and-unstaged-hunks
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
sed -i.bak 's/public int Minute/public int Month/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":2}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "5:IDE0055" "7:IDE0055"
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
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
run_verify --working-tree
assert_status 0
assert_log_contains "prettier cwd=<$last_repo/apps/portal/RvtPortal.Client>"
assert_log_contains "<--list-different> <src/app.ts>"
assert_log_contains "eslint cwd=<$last_repo/apps/portal/RvtPortal.Client>"
assert_log_contains "<--format> <json> <src/app.ts>"

# Every supported Portal text extension is formatted; TS module forms are linted.
create_repo portal-supported-extensions
declare -a prettier_extensions=(
  css html js jsx json md mdx scss ts tsx yaml yml
  mjs cjs mts cts svg graphql gql
)
for extension in "${prettier_extensions[@]}"; do
  candidate="$last_repo/apps/portal/RvtPortal.Client/src/extension.$extension"
  case "$extension" in
    json) printf '{}\n' > "$candidate" ;;
    yaml|yml) printf 'value: true\n' > "$candidate" ;;
    svg) printf '<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>\n' > "$candidate" ;;
    graphql|gql) printf 'query Viewer { viewer { id } }\n' > "$candidate" ;;
    *) printf 'export const extensionValue = true;\n' > "$candidate" ;;
  esac
done
run_verify --working-tree
assert_status 0
for extension in "${prettier_extensions[@]}"; do
  assert_log_contains "<src/extension.$extension>"
done
for extension in ts tsx mts cts; do
  rg -F "eslint cwd=" "$last_repo/tool.log" |
    rg -F -q -- "<src/extension.$extension>" ||
    fail "ESLint did not receive TypeScript extension .$extension"
done

# A diagnostic in a new file is rejected even when its whole-path count is stable.
create_repo new-file-diagnostic
printf 'namespace Sample;\npublic sealed class Added {}\n' > "$last_repo/src/Added.cs"
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Added.cs","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Added.cs" "1:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "changed surface"
assert_output "src/Added.cs:1"

# A diagnostic on a changed line is rejected at a stable total.
create_repo changed-line
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "changed surface"
assert_output "src/Clock.cs:5"

# An unchanged-line legacy diagnostic is allowed only at or below baseline.
create_repo unchanged-line
sed -i.bak 's/public int Second/public int Millisecond/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
cp "$source_root/tests/fixtures/engineering-standards/baseline.json" "$last_repo/baseline.json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 0

# A whole-path increase fails with baseline and observed counts.
create_repo increase
sed -i.bak 's/public int Second/public int Millisecond/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
cp "$source_root/tests/fixtures/engineering-standards/baseline.json" "$last_repo/baseline.json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "5:IDE0055" "7:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 1
assert_output "baseline=1"
assert_output "observed=2"

# A decrease is reported without failing.
create_repo decrease
sed -i.bak 's/public int Second/public int Millisecond/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":2}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
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
assert_output "symlink"
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
assert_output "symlink"
assert_log_absent

create_repo baseline-intermediate-symlink
mkdir -p "$temp_root/baseline-policy-outside"
write_json "$temp_root/baseline-policy-outside/baseline.json" \
  '{"version":1,"entries":[]}'
ln -s "$temp_root/baseline-policy-outside" "$last_repo/policy-link"
RVT_TEST_BASELINE_PATH="$last_repo/policy-link/baseline.json" \
  run_verify --working-tree
assert_status 2
assert_output "symlink"
assert_log_absent

create_repo exceptions-intermediate-symlink
mkdir -p "$temp_root/exceptions-policy-outside"
write_json "$temp_root/exceptions-policy-outside/exceptions.json" \
  '{"version":1,"exceptions":[]}'
ln -s "$temp_root/exceptions-policy-outside" "$last_repo/policy-link"
RVT_TEST_EXCEPTIONS_PATH="$last_repo/policy-link/exceptions.json" \
  run_verify --working-tree
assert_status 2
assert_output "symlink"
assert_log_absent

# Initialization creates an absent baseline once and refuses every existing file.
create_repo initialize
RVT_TEST_BASELINE_PATH="$last_repo/initialized.json" run_verify --all --initialize-baseline
assert_status 0
[[ -f "$last_repo/initialized.json" ]] || fail "initialization did not create baseline"
RVT_TEST_BASELINE_PATH="$last_repo/initialized.json" run_verify --all --initialize-baseline
assert_status 1
assert_output "already exists"
write_json "$last_repo/existing-empty.json" '{"version":1,"entries":[]}'
RVT_TEST_BASELINE_PATH="$last_repo/existing-empty.json" run_verify --all --initialize-baseline
assert_status 1

create_repo initialize-with-diagnostics
diagnostic_baseline="$last_repo/diagnostic-baseline.json"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
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
  RVT_FAKE_DELAY=0.1 RVT_TEST_BASELINE_PATH="$race_baseline" \
    run_verify --all --initialize-baseline
  printf '%s\n' "$last_status" > "$last_repo/race-status-1"
) &
race_pid_1=$!
(
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
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":1}]}'
cp "$last_repo/baseline.json" "$last_repo/baseline.before"
cp "$last_repo/exceptions.json" "$last_repo/exceptions.before"
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" \
  "5:IDE0055" "7:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --all --update-baseline
assert_status 1
cmp -s "$last_repo/baseline.before" "$last_repo/baseline.json" ||
  fail "refused baseline update changed the file"
cmp -s "$last_repo/exceptions.before" "$last_repo/exceptions.json" ||
  fail "baseline update widened exceptions"

write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":2}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
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
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/Clock.cs","count":2}]}'
write_dotnet_report "$last_repo/slow.json" "$last_repo/src/Clock.cs" "5:IDE0055"
(
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
write_json "$canonical_lock/owner.json" \
  "{\"version\":1,\"pid\":$$,\"token\":\"live-owner\",\"createdAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
live_dotnet_command="$(printf '["%s"]' "$fake_bin/fake-dotnet")"
live_prettier_command="$(printf '["%s"]' "$fake_bin/fake-prettier")"
live_eslint_command="$(printf '["%s"]' "$fake_bin/fake-eslint")"
RVT_STANDARDS_DOTNET_COMMAND="$live_dotnet_command" \
RVT_STANDARDS_PRETTIER_COMMAND="$live_prettier_command" \
RVT_STANDARDS_ESLINT_COMMAND="$live_eslint_command" \
RVT_STANDARDS_BASELINE_PATH="$last_repo/baseline.json" \
RVT_STANDARDS_EXCEPTIONS_PATH="$last_repo/exceptions.json" \
RVT_FAKE_LOG="$last_repo/tool.log" \
RVT_FAKE_DOTNET_REPORT="" RVT_FAKE_DOTNET_STATUS=0 \
RVT_FAKE_DOTNET_FAIL_PHASE="" RVT_FAKE_DOTNET_SKIP_REPORT=0 \
RVT_FAKE_DELAY="" RVT_FAKE_STARTED_MARKER="" \
RVT_FAKE_EXPECT_CONTENT="" RVT_FAKE_REQUIRE_ASSET="" \
RVT_FAKE_PRETTIER_STATUS=0 RVT_FAKE_PRETTIER_OUTPUT="" \
RVT_FAKE_ESLINT_STATUS=0 RVT_FAKE_ESLINT_REPORT="" \
  node -e '
    const { spawnSync } = require("child_process");
    const result = spawnSync(process.argv[1], ["--all", "--update-baseline"], {
      cwd: process.argv[2], env: process.env, timeout: 200
    });
    if (result.error?.code !== "ETIMEDOUT") process.exit(1);
  ' "$last_repo/scripts/verify-engineering-standards.sh" "$last_repo" ||
  fail "live canonical baseline lock was stolen"
[[ -d "$canonical_lock" ]] ||
  fail "live canonical baseline lock was removed"
rm -rf "$canonical_lock"

# A demonstrably dead owner is reclaimed and the new owner cleans up its lock.
create_repo dead-baseline-lock-owner
dead_lock="$last_repo/baseline.json.update.lock"
sleep 30 &
killed_owner_pid=$!
kill "$killed_owner_pid"
wait "$killed_owner_pid" 2>/dev/null || true
mkdir "$dead_lock"
write_json "$dead_lock/owner.json" \
  "{\"version\":1,\"pid\":$killed_owner_pid,\"token\":\"dead-owner\",\"createdAt\":\"2000-01-01T00:00:00Z\"}"
run_verify --all --update-baseline
assert_status 0
[[ ! -e "$dead_lock" ]] ||
  fail "dead canonical baseline lock was not reclaimed and cleaned"

# An incomplete lock-creation crash is recoverable only after its bounded grace.
create_repo partial-baseline-lock-owner
partial_lock="$last_repo/baseline.json.update.lock"
mkdir "$partial_lock"
touch -t 200001010000 "$partial_lock"
run_verify --all --update-baseline
assert_status 0
[[ ! -e "$partial_lock" ]] ||
  fail "stale metadata-free baseline lock was not reclaimed"

# Explicit and automatic committed ranges resolve without shell interpretation.
create_repo explicit-range
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "change"
run_verify --base "$base_revision" --head HEAD
assert_status 0
assert_log_contains "<src/Clock.cs>"

create_repo auto-feature
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "feature"
run_verify --base auto --head HEAD
assert_status 0
assert_log_contains "<src/Clock.cs>"

create_repo auto-main
sed -i.bak 's/public int Hour/public int Day/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
git -C "$last_repo" add src/Clock.cs
git -C "$last_repo" commit -q -m "main change"
git -C "$last_repo" update-ref refs/remotes/origin/main HEAD
run_verify --base auto --head HEAD
assert_status 0
assert_log_contains "<src/Clock.cs>"

# A committed range analyzes the exact requested head, never checkout or dirty content.
create_repo exact-range-head
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak 's/public int Hour/public int HeadHour/' "$last_repo/src/Clock.cs"
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

# Default range commands receive compatible ignored prerequisites.
create_repo exact-range-default-assets
write_json "$last_repo/apps/portal/RvtPortal.Client/package.json" \
  '{"private":true,"devDependencies":{"eslint":"1.0.0","prettier":"1.0.0"}}'
write_json "$last_repo/apps/portal/RvtPortal.Client/package-lock.json" \
  '{"lockfileVersion":3,"packages":{}}'
write_json "$last_repo/src/Sample.csproj" \
  '<Project Sdk="Microsoft.NET.Sdk"></Project>'
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
sed -i.bak 's/public int Hour/public int HeadHour/' "$last_repo/src/Clock.cs"
rm "$last_repo/src/Clock.cs.bak"
printf 'export const headOnly = true;\n' \
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
assert_log_contains "dotnet"
assert_log_contains "prettier cwd="
assert_log_contains "eslint cwd="

create_repo exact-range-missing-assets
write_json "$last_repo/src/Sample.csproj" \
  '<Project Sdk="Microsoft.NET.Sdk"></Project>'
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "tracked project"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
sed -i.bak 's/public int Hour/public int HeadHour/' "$last_repo/src/Clock.cs"
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
  '<Project Sdk="Microsoft.NET.Sdk"></Project>'
git -C "$last_repo" add src/Sample.csproj
git -C "$last_repo" commit -q -m "base project inputs"
base_revision="$(git -C "$last_repo" rev-parse HEAD)"
write_json "$last_repo/src/Sample.csproj" \
  '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><LangVersion>preview</LangVersion></PropertyGroup></Project>'
sed -i.bak 's/public int Hour/public int HeadHour/' "$last_repo/src/Clock.cs"
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

# Inventory mode ratchets report-based whitespace, ESLint, and Prettier findings.
create_repo inventory
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-whitespace","ruleId":"IDE0055","path":"src/Clock.cs","count":1},{"tool":"eslint","ruleId":"@typescript-eslint/no-unused-vars","path":"apps/portal/RvtPortal.Client/src/app.ts","count":1},{"tool":"prettier","ruleId":"prettier/format","path":"apps/portal/RvtPortal.Client/src/app.ts","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Clock.cs" "5:IDE0055"
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
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_OUTPUT="src/app.ts" RVT_FAKE_PRETTIER_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "Prettier"

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
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Other.cs" "1:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=whitespace RVT_FAKE_DOTNET_STATUS=7 \
  run_verify --working-tree
assert_status 2
assert_output "unexpected path"

create_repo prettier-internal-no-filenames
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_STATUS=2 run_verify --working-tree
assert_status 2

create_repo prettier-internal-with-filenames
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_PRETTIER_STATUS=2 RVT_FAKE_PRETTIER_OUTPUT="src/app.ts" \
  run_verify --working-tree
assert_status 2

create_repo eslint-internal-with-diagnostics
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_eslint_report "$last_repo/eslint.json" \
  "$last_repo/apps/portal/RvtPortal.Client/src/app.ts" 1
RVT_FAKE_ESLINT_STATUS=2 RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" \
  run_verify --working-tree
assert_status 2

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
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/Other.cs" "1:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 run_verify --working-tree
assert_status 2
assert_output "unexpected path"

create_repo malformed-eslint
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_json "$last_repo/eslint.json" 'not-json'
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" RVT_FAKE_ESLINT_STATUS=1 \
  run_verify --working-tree
assert_status 2

create_repo empty-nonzero-eslint
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
RVT_FAKE_ESLINT_STATUS=2 run_verify --working-tree
assert_status 2
assert_output "internal/configuration"

create_repo unexpected-eslint-path
printf 'export const other = 1;\n' \
  > "$last_repo/apps/portal/RvtPortal.Client/src/other.ts"
git -C "$last_repo" add apps/portal/RvtPortal.Client/src/other.ts
git -C "$last_repo" commit -q -m "other TypeScript source"
printf 'export const changed = 43;\n' >> "$last_repo/apps/portal/RvtPortal.Client/src/app.ts"
write_eslint_report "$last_repo/eslint.json" \
  "$last_repo/apps/portal/RvtPortal.Client/src/other.ts" 1
RVT_FAKE_ESLINT_REPORT="$last_repo/eslint.json" RVT_FAKE_ESLINT_STATUS=1 \
  run_verify --working-tree
assert_status 2
assert_output "unexpected path"

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
write_dotnet_report "$last_repo/dotnet.json" "$temp_root/outside.cs" "1:IDE0055"
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

create_repo real-rename
git -C "$last_repo" mv src/Clock.cs src/RenamedClock.cs
sed -i.bak 's/public int Hour/public int RenamedHour/' "$last_repo/src/RenamedClock.cs"
rm "$last_repo/src/RenamedClock.cs.bak"
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/RenamedClock.cs","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/RenamedClock.cs" "5:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "changed surface"
assert_log_contains "<src/RenamedClock.cs>"

create_repo real-copy
cp "$last_repo/src/Clock.cs" "$last_repo/src/CopiedClock.cs"
git -C "$last_repo" add src/CopiedClock.cs
write_json "$last_repo/baseline.json" \
  '{"version":1,"entries":[{"tool":"dotnet-format-style","ruleId":"IDE0055","path":"src/CopiedClock.cs","count":1}]}'
write_dotnet_report "$last_repo/dotnet.json" "$last_repo/src/CopiedClock.cs" "5:IDE0055"
RVT_FAKE_DOTNET_REPORT="$last_repo/dotnet.json" \
RVT_FAKE_DOTNET_FAIL_PHASE=style RVT_FAKE_DOTNET_STATUS=1 \
  run_verify --working-tree
assert_status 1
assert_output "changed surface"
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
