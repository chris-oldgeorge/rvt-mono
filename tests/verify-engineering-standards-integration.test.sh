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

def normalized_lines(source)
  source.lines
    .map(&:strip)
    .reject { |line| line.empty? || line.start_with?("#") }
    .map { |line| line.gsub(/[[:space:]]+/, " ") }
end

def matching_indexes(lines, pattern)
  lines.each_index.select { |index| lines[index].match?(pattern) }
end

def exactly_one_index(lines, pattern, description)
  indexes = matching_indexes(lines, pattern)
  assert(indexes.length == 1, "#{description} must occur exactly once")
  indexes.fetch(0)
end

def verify_build_script(path)
  lines = normalized_lines(File.read(path, encoding: "utf-8"))

  boundary_index = exactly_one_index(
    lines,
    /\Abash[[:space:]]+scripts\/verify-postgresql-only\.sh[[:space:]]+\.\z/,
    "PostgreSQL boundary verification"
  )
  restore_index = exactly_one_index(
    lines,
    /\Adotnet[[:space:]]+restore[[:space:]]+"\$\{solution\}"[[:space:]]+--disable-parallel\z/,
    "aggregate restore"
  )
  standards_index = exactly_one_index(
    lines,
    %r{\A"\$\{repo_root\}/scripts/verify-engineering-standards\.sh"[[:space:]]+--working-tree\z},
    "aggregate standards verification"
  )
  build_index = exactly_one_index(
    lines,
    /\Adotnet[[:space:]]+build[[:space:]]+"\$\{solution\}"[[:space:]]+--no-restore[[:space:]]+--nologo[[:space:]]+-m:1\z/,
    "aggregate build"
  )
  test_index = exactly_one_index(
    lines,
    /\Adotnet[[:space:]]+test[[:space:]]+"\$\{solution\}"[[:space:]]+--no-build[[:space:]]+--nologo\z/,
    "aggregate test"
  )

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
  normalized_lines(scalar(step.fetch("run"), "#{context} run command"))
end

def count_matching_commands(steps, pattern)
  steps.sum do |step|
    next 0 unless step.key?("run")

    run_lines(step, step_name(step) || "unnamed step").count do |line|
      line.match?(pattern)
    end
  end
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
  coverage_index = named_step_index(steps, "Collect Portal client coverage")

  assert(
    install_index == node_index + 1,
    "Portal client dependencies must be installed immediately after Node setup"
  )

  install_step = steps.fetch(install_index)
  assert(
    install_step.key?("working-directory") &&
      scalar(install_step.fetch("working-directory"), "Portal install working directory") ==
        "apps/portal/RvtPortal.Client",
    "Portal dependency installation must run in the Portal client directory"
  )
  assert(
    run_lines(install_step, "Install Portal client dependencies") == ["npm ci"],
    "Portal dependency installation must run exactly one npm ci command"
  )

  restore_pattern =
    /\Adotnet[[:space:]]+restore[[:space:]]+Rvt\.Mono\.slnx[[:space:]]+--disable-parallel\z/
  standards_pattern =
    %r{\Ascripts/verify-engineering-standards\.sh[[:space:]]+--base[[:space:]]+auto[[:space:]]+--head[[:space:]]+HEAD\z}
  build_pattern =
    /\Adotnet[[:space:]]+build[[:space:]]+Rvt\.Mono\.slnx[[:space:]]+--configuration[[:space:]]+Release[[:space:]]+--no-restore[[:space:]]+--no-incremental[[:space:]]+--nologo[[:space:]]+-m:1\z/
  npm_ci_pattern = /\Anpm[[:space:]]+ci\z/

  assert(
    count_matching_commands(steps, restore_pattern) == 1,
    "workflow monorepo restore must occur exactly once"
  )
  assert(
    count_matching_commands(steps, standards_pattern) == 1,
    "workflow standards verification must occur exactly once"
  )
  assert(
    count_matching_commands(steps, build_pattern) == 1,
    "workflow Release build must occur exactly once"
  )
  assert(
    count_matching_commands(steps, npm_ci_pattern) == 1,
    "workflow must contain exactly one npm ci command"
  )

  assert(
    restore_index < standards_index && install_index < standards_index,
    ".NET restore and npm ci must precede standards verification"
  )
  assert(
    standards_index < build_index,
    "standards verification must precede the Release build"
  )

  coverage_lines = run_lines(steps.fetch(coverage_index), "Collect Portal client coverage")
  assert(
    coverage_lines.none? { |line| line.match?(npm_ci_pattern) },
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
