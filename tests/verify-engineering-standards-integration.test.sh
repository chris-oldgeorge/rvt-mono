#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_root="$(mktemp -d)"

cleanup() {
  rm -rf "${temp_root}"
}
trap cleanup EXIT

mkdir -p "${temp_root}/scripts" "${temp_root}/.github/workflows"
cp "${repo_root}/scripts/build-mono.sh" "${temp_root}/scripts/build-mono.sh"
cp "${repo_root}/.github/workflows/sonarqube.yml" \
  "${temp_root}/.github/workflows/sonarqube.yml"

checker="${temp_root}/verify-order.rb"
cat > "${checker}" <<'RUBY'
require "psych"

class VerificationFailure < StandardError
end

def assert(condition, message)
  raise VerificationFailure, message unless condition
end

def scalar(node, context)
  assert(node.is_a?(Psych::Nodes::Scalar), "#{context} must be a scalar")
  node.value
end

def mapping(node, context)
  assert(node.is_a?(Psych::Nodes::Mapping), "#{context} must be a mapping")
  node.children.each_slice(2).to_h do |key, value|
    [scalar(key, "#{context} key"), value]
  end
end

def sequence(node, context)
  assert(node.is_a?(Psych::Nodes::Sequence), "#{context} must be a sequence")
  node.children
end

def logical_shell_commands(source)
  logical_lines = []
  pending = ""

  source.each_line do |physical_line|
    stripped = physical_line.strip
    next if stripped.empty? || stripped.start_with?("#")

    continued = stripped.end_with?("\\")
    fragment = continued ? stripped.delete_suffix("\\").rstrip : stripped
    pending = [pending, fragment].reject(&:empty?).join(" ")

    next if continued

    logical_lines << pending
    pending = ""
  end

  logical_lines << pending unless pending.empty?
  logical_lines
    .flat_map { |line| line.split(/[[:space:]]*(?:&&|\|\||;)[[:space:]]*/) }
    .map { |command| command.strip.gsub(/[[:space:]]+/, " ") }
    .reject(&:empty?)
end

def monorepo_phase_occurrences(command, phase)
  phase_pattern = Regexp.escape(phase)
  command.scan(
    /(?<![A-Za-z0-9_.-])dotnet[[:space:]]+#{phase_pattern}[[:space:]]+(?:"\$\{solution\}"|"?\$\{repo_root\}\/Rvt\.Mono\.slnx"?|Rvt\.Mono\.slnx)(?=[[:space:]"']|\z)/
  ).length
end

def standards_occurrences(command)
  command.scan(
    %r{scripts/verify-engineering-standards\.sh(?=[[:space:]"']|\z)}
  ).length
end

def npm_ci_occurrences(command)
  command.scan(/\Anpm[[:space:]]+ci(?=[[:space:]]|\z)/).length
end

def boundary_occurrences(command)
  command.scan(
    /\Abash[[:space:]]+scripts\/verify-postgresql-only\.sh(?=[[:space:]]|\z)/
  ).length
end

def exactly_one_invocation_index(commands, description)
  indexes = []
  commands.each_index do |index|
    yield(commands.fetch(index)).times { indexes << index }
  end
  assert(indexes.length == 1, "#{description} must occur exactly once")
  indexes.fetch(0)
end

def assert_canonical_command(commands, canonical, description)
  assert(
    commands.count(canonical) == 1,
    "#{description} must use exactly one canonical invocation"
  )
end

def verify_build_script(path)
  commands = logical_shell_commands(File.read(path, encoding: "utf-8"))

  canonical = {
    boundary: "bash scripts/verify-postgresql-only.sh .",
    restore: 'dotnet restore "${solution}" --disable-parallel',
    standards: '"${repo_root}/scripts/verify-engineering-standards.sh" --working-tree',
    build: 'dotnet build "${solution}" --no-restore --nologo -m:1',
    test: 'dotnet test "${solution}" --no-build --nologo'
  }
  canonical.each do |phase, command|
    assert_canonical_command(commands, command, "aggregate #{phase}")
  end

  boundary_index = exactly_one_invocation_index(
    commands,
    "PostgreSQL boundary verification"
  ) { |command| boundary_occurrences(command) }
  restore_index = exactly_one_invocation_index(
    commands,
    "aggregate restore"
  ) { |command| monorepo_phase_occurrences(command, "restore") }
  standards_index = exactly_one_invocation_index(
    commands,
    "aggregate standards verification"
  ) { |command| standards_occurrences(command) }
  build_index = exactly_one_invocation_index(
    commands,
    "aggregate build"
  ) { |command| monorepo_phase_occurrences(command, "build") }
  test_index = exactly_one_invocation_index(
    commands,
    "aggregate test"
  ) { |command| monorepo_phase_occurrences(command, "test") }

  assert(
    boundary_index < restore_index,
    "PostgreSQL boundary verification must remain before restore"
  )
  assert(
    restore_index < standards_index,
    "aggregate restore must precede standards verification"
  )
  assert(
    standards_index < build_index && standards_index < test_index,
    "aggregate standards verification must precede build and test"
  )
  assert(build_index < test_index, "aggregate build must precede test")
end

def workflow_steps(path)
  document = Psych.parse_file(path)
  root = mapping(document.root, "workflow root")
  jobs = mapping(root.fetch("jobs"), "jobs")
  analyze = mapping(jobs.fetch("analyze"), "analyze job")
  sequence(analyze.fetch("steps"), "analyze steps").map do |node|
    mapping(node, "step")
  end
end

def step_name(step)
  step.key?("name") ? scalar(step.fetch("name"), "step name") : nil
end

def named_step_index(steps, name)
  indexes = steps.each_index.select { |index| step_name(steps[index]) == name }
  assert(indexes.length == 1, "#{name} step must occur exactly once")
  indexes.fetch(0)
end

def run_lines(step, context)
  assert(step.key?("run"), "#{context} must define a run command")
  logical_shell_commands(scalar(step.fetch("run"), "#{context} run command"))
end

def workflow_invocation_positions(steps)
  positions = []
  steps.each_index do |step_index|
    step = steps.fetch(step_index)
    next unless step.key?("run")

    commands = run_lines(step, step_name(step) || "unnamed step")
    commands.each_index do |command_index|
      yield(commands.fetch(command_index)).times do |occurrence_index|
        positions << [step_index, command_index, occurrence_index]
      end
    end
  end
  positions
end

def exactly_one_workflow_invocation(steps, description, &block)
  positions = workflow_invocation_positions(steps, &block)
  assert(positions.length == 1, "#{description} must occur exactly once")
  positions.fetch(0)
end

def before?(left, right)
  (left <=> right) == -1
end

def assert_unconditional_step(step, context)
  assert(!step.key?("if"), "#{context} must not define an if condition")
  assert(
    !step.key?("continue-on-error"),
    "#{context} must not allow failures with continue-on-error"
  )
end

def verify_workflow(path)
  steps = workflow_steps(path)

  node_indexes = steps.each_index.select do |index|
    step = steps[index]
    step.key?("uses") &&
      scalar(step.fetch("uses"), "action reference").match?(%r{\Aactions/setup-node@[0-9a-f]{40}\z})
  end
  assert(node_indexes.length == 1, "SHA-pinned Node setup must occur exactly once")
  node_index = node_indexes.fetch(0)

  install_index = named_step_index(steps, "Install Portal client dependencies")
  restore_index = named_step_index(steps, "Restore monorepo")
  standards_index = named_step_index(steps, "Verify engineering standards")
  build_index = named_step_index(steps, "Build monorepo (Release)")
  test_index = named_step_index(steps, "Collect .NET coverage")
  coverage_index = named_step_index(steps, "Collect Portal client coverage")

  assert(
    install_index == node_index + 1,
    "Portal client dependencies must be installed immediately after Node setup"
  )

  install_step = steps.fetch(install_index)
  restore_step = steps.fetch(restore_index)
  standards_step = steps.fetch(standards_index)
  build_step = steps.fetch(build_index)
  test_step = steps.fetch(test_index)
  [
    [install_step, "Install Portal client dependencies"],
    [restore_step, "Restore monorepo"],
    [standards_step, "Verify engineering standards"],
    [build_step, "Build monorepo (Release)"]
  ].each do |step, context|
    assert_unconditional_step(step, context)
  end

  assert(
    install_step.key?("working-directory") &&
      scalar(install_step.fetch("working-directory"), "Portal install working directory") ==
        "apps/portal/RvtPortal.Client",
    "Portal dependency installation must run in the Portal client directory"
  )

  canonical_commands = [
    [install_step, ["npm ci"], "Portal dependency installation"],
    [
      restore_step,
      ["dotnet restore Rvt.Mono.slnx --disable-parallel"],
      "workflow monorepo restore"
    ],
    [
      standards_step,
      ["scripts/verify-engineering-standards.sh --base auto --head HEAD"],
      "workflow standards verification"
    ],
    [
      build_step,
      [
        "dotnet build Rvt.Mono.slnx --configuration Release --no-restore " \
          "--no-incremental --nologo -m:1"
      ],
      "workflow Release build"
    ]
  ]
  canonical_commands.each do |step, expected, description|
    assert(
      run_lines(step, description) == expected,
      "#{description} must use exactly one canonical invocation"
    )
  end

  npm_position = exactly_one_workflow_invocation(
    steps,
    "workflow npm ci"
  ) { |command| npm_ci_occurrences(command) }
  restore_position = exactly_one_workflow_invocation(
    steps,
    "workflow monorepo restore"
  ) { |command| monorepo_phase_occurrences(command, "restore") }
  standards_position = exactly_one_workflow_invocation(
    steps,
    "workflow standards verification"
  ) { |command| standards_occurrences(command) }
  build_position = exactly_one_workflow_invocation(
    steps,
    "workflow Release build"
  ) { |command| monorepo_phase_occurrences(command, "build") }
  test_position = exactly_one_workflow_invocation(
    steps,
    "workflow monorepo test"
  ) { |command| monorepo_phase_occurrences(command, "test") }

  assert(
    before?(restore_position, standards_position) &&
      before?(npm_position, standards_position),
    ".NET restore and npm ci must precede standards verification"
  )
  assert(
    before?(standards_position, build_position) &&
      before?(build_position, test_position),
    "standards verification must precede the Release build and monorepo test"
  )

  test_commands = run_lines(test_step, "Collect .NET coverage")
  canonical_test =
    "dotnet test Rvt.Mono.slnx --configuration Release --no-build " \
      "--no-restore --nologo -m:1"
  canonical_test_pattern = /"#{Regexp.escape(canonical_test)}"/
  assert(
    test_commands.sum { |command| command.scan(canonical_test_pattern).length } == 1,
    "workflow monorepo test must use exactly one canonical invocation"
  )

  coverage_lines = run_lines(steps.fetch(coverage_index), "Collect Portal client coverage")
  assert(
    coverage_lines.sum { |command| npm_ci_occurrences(command) }.zero?,
    "Portal coverage must not run a second npm ci"
  )
end

begin
  verify_build_script(ARGV.fetch(0))
  verify_workflow(ARGV.fetch(1))
rescue VerificationFailure, KeyError, Psych::SyntaxError => error
  warn "FAIL: #{error.message}"
  exit 1
end
RUBY

build_copy="${temp_root}/scripts/build-mono.sh"
workflow_copy="${temp_root}/.github/workflows/sonarqube.yml"
ruby "${checker}" "${build_copy}" "${workflow_copy}"

mutation_failures=0

assert_workflow_mutation_rejected() {
  local mutation_key="$1"
  local label="$2"
  local mutation_root="${temp_root}/${mutation_key}"
  local mutated_workflow="${mutation_root}/.github/workflows/sonarqube.yml"

  mkdir -p "${mutation_root}/scripts" "${mutation_root}/.github/workflows" \
    "${mutation_root}/tests"
  cp "${build_copy}" "${mutation_root}/scripts/build-mono.sh"
  cp "${workflow_copy}" "${mutated_workflow}"
  cp "${repo_root}/tests/verify-manual-sonarqube-workflow.test.sh" \
    "${mutation_root}/tests/verify-manual-sonarqube-workflow.test.sh"

  ruby - "${mutation_key}" "${mutated_workflow}" <<'RUBY'
mutation_key = ARGV.fetch(0)
path = ARGV.fetch(1)
source = File.read(path, encoding: "utf-8")

needle, replacement = case mutation_key
when "npm-options"
  needle = "          npm run test:coverage\n"
  [needle, "          npm ci --ignore-scripts\n#{needle}"]
when "npm-inline-comment"
  needle = "          npm run test:coverage\n"
  [needle, "          npm ci # duplicate\n#{needle}"]
when "npm-continuation"
  needle = "          npm run test:coverage\n"
  [needle, "          npm \\\n            ci\n#{needle}"]
when "npm-chain"
  needle = "          npm run test:coverage\n"
  [needle, "          npm ci && npm ci\n#{needle}"]
when "standards-continue-on-error"
  needle = "      - name: Verify engineering standards\n"
  [needle, "#{needle}        continue-on-error: true\n"]
when "standards-false-condition"
  needle = "      - name: Verify engineering standards\n"
  [needle, "#{needle}        if: ${{ false }}\n"]
when "pre-standards-release-build"
  needle = "      - name: Verify engineering standards\n"
  replacement =
    "      - name: Premature Release build\n" \
    "        run: dotnet build Rvt.Mono.slnx -c Release --nologo\n" \
    "\n" \
    "      - name: Verify engineering standards\n"
  [needle, replacement]
else
  abort "unknown workflow mutation: #{mutation_key}"
end

mutated = source.sub(needle, replacement)
abort "#{mutation_key} mutation did not change #{path}" if mutated == source
File.write(path, mutated, mode: "w", encoding: "utf-8")
RUBY

  if ruby "${checker}" "${mutation_root}/scripts/build-mono.sh" \
    "${mutated_workflow}" >/dev/null 2>&1; then
    printf 'FAIL: integration checker accepted %s mutation.\n' "${label}" >&2
    mutation_failures=$((mutation_failures + 1))
  else
    printf 'Integration checker rejected %s mutation.\n' "${label}"
  fi

  if bash "${mutation_root}/tests/verify-manual-sonarqube-workflow.test.sh" \
    >/dev/null 2>&1; then
    printf 'FAIL: manual Sonar guard accepted %s mutation.\n' "${label}" >&2
    mutation_failures=$((mutation_failures + 1))
  else
    printf 'Manual Sonar guard rejected %s mutation.\n' "${label}"
  fi
}

assert_build_mutation_rejected() {
  local mutation_key="$1"
  local label="$2"
  local mutation_root="${temp_root}/${mutation_key}"
  local mutated_build="${mutation_root}/scripts/build-mono.sh"

  mkdir -p "${mutation_root}/scripts" "${mutation_root}/.github/workflows"
  cp "${build_copy}" "${mutated_build}"
  cp "${workflow_copy}" "${mutation_root}/.github/workflows/sonarqube.yml"

  ruby - "${mutated_build}" <<'RUBY'
path = ARGV.fetch(0)
source = File.read(path, encoding: "utf-8")
needle = "bash scripts/verify-postgresql-only.sh .\n"
replacement = "dotnet restore Rvt.Mono.slnx --verbosity quiet\n\n#{needle}"
mutated = source.sub(needle, replacement)
abort "pre-boundary restore mutation did not change #{path}" if mutated == source
File.write(path, mutated, mode: "w", encoding: "utf-8")
RUBY

  if ruby "${checker}" "${mutated_build}" \
    "${mutation_root}/.github/workflows/sonarqube.yml" >/dev/null 2>&1; then
    printf 'FAIL: integration checker accepted %s mutation.\n' "${label}" >&2
    mutation_failures=$((mutation_failures + 1))
  else
    printf 'Integration checker rejected %s mutation.\n' "${label}"
  fi
}

assert_workflow_mutation_rejected "npm-options" "npm ci with options"
assert_workflow_mutation_rejected "npm-inline-comment" "npm ci with an inline comment"
assert_workflow_mutation_rejected "npm-continuation" "continued npm ci"
assert_workflow_mutation_rejected "npm-chain" "chained duplicate npm ci"
assert_workflow_mutation_rejected \
  "standards-continue-on-error" "non-blocking standards step"
assert_workflow_mutation_rejected \
  "standards-false-condition" "conditionally skipped standards step"
assert_build_mutation_rejected \
  "pre-boundary-restore" "monorepo restore before the PostgreSQL boundary"
assert_workflow_mutation_rejected \
  "pre-standards-release-build" "Release build before standards verification"

if ((mutation_failures > 0)); then
  printf 'FAIL: %d guard mutation acceptance(s) detected.\n' \
    "${mutation_failures}" >&2
  exit 1
fi

remove_standards_command() {
  local path="$1"
  ruby - "${path}" <<'RUBY'
path = ARGV.fetch(0)
lines = File.readlines(path, encoding: "utf-8")
filtered = lines.reject { |line| line.include?("verify-engineering-standards.sh") }
abort "standards mutation did not change #{path}" if filtered == lines
File.write(path, filtered.join, mode: "w", encoding: "utf-8")
RUBY
}

local_mutation_root="${temp_root}/local-mutation"
mkdir -p "${local_mutation_root}/scripts" "${local_mutation_root}/.github/workflows"
cp "${build_copy}" "${local_mutation_root}/scripts/build-mono.sh"
cp "${workflow_copy}" "${local_mutation_root}/.github/workflows/sonarqube.yml"
remove_standards_command "${local_mutation_root}/scripts/build-mono.sh"
if ruby "${checker}" \
  "${local_mutation_root}/scripts/build-mono.sh" \
  "${local_mutation_root}/.github/workflows/sonarqube.yml" >/dev/null 2>&1; then
  printf 'FAIL: local standards-command removal mutation was accepted.\n' >&2
  exit 1
fi
printf 'Local standards-command removal mutation rejected.\n'

workflow_mutation_root="${temp_root}/workflow-mutation"
mkdir -p "${workflow_mutation_root}/scripts" \
  "${workflow_mutation_root}/.github/workflows"
cp "${build_copy}" "${workflow_mutation_root}/scripts/build-mono.sh"
cp "${workflow_copy}" "${workflow_mutation_root}/.github/workflows/sonarqube.yml"
remove_standards_command \
  "${workflow_mutation_root}/.github/workflows/sonarqube.yml"
if ruby "${checker}" \
  "${workflow_mutation_root}/scripts/build-mono.sh" \
  "${workflow_mutation_root}/.github/workflows/sonarqube.yml" >/dev/null 2>&1; then
  printf 'FAIL: workflow standards-command removal mutation was accepted.\n' >&2
  exit 1
fi
printf 'Workflow standards-command removal mutation rejected.\n'

printf 'Engineering standards build and CI sequencing verified.\n'
