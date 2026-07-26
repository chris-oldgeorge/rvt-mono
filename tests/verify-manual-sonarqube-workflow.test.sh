#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow_path="${repo_root}/.github/workflows/sonarqube.yml"

if [[ ! -f "${workflow_path}" ]]; then
  printf 'Missing .github/workflows/sonarqube.yml\n' >&2
  exit 1
fi

ruby - "${workflow_path}" <<'RUBY'
require "psych"

workflow_path = ARGV.fetch(0)
document = Psych.parse_file(workflow_path)

def assert(condition, message)
  raise message unless condition
end

def scalar(node, context)
  assert(node.is_a?(Psych::Nodes::Scalar), "#{context} must be a scalar")
  node.value
end

def mapping(node, context)
  assert(node.is_a?(Psych::Nodes::Mapping), "#{context} must be a mapping")
  pairs = {}
  node.children.each_slice(2) do |key, value|
    pairs[scalar(key, "#{context} key")] = value
  end
  pairs
end

def sequence(node, context)
  assert(node.is_a?(Psych::Nodes::Sequence), "#{context} must be a sequence")
  node.children
end

def text_values(node)
  case node
  when Psych::Nodes::Scalar
    [node.value]
  else
    node.children.flat_map { |child| text_values(child) }
  end
end

root = mapping(document.root, "workflow root")
assert(root.fetch("on").is_a?(Psych::Nodes::Scalar), "workflow on event must be a scalar")
assert(scalar(root.fetch("on"), "workflow on event") == "workflow_dispatch", "workflow must only use workflow_dispatch")

permissions = mapping(root.fetch("permissions"), "permissions")
assert(permissions == { "contents" => permissions.fetch("contents") }, "permissions must only grant contents")
assert(scalar(permissions.fetch("contents"), "contents permission") == "read", "contents permission must be read")

concurrency = mapping(root.fetch("concurrency"), "concurrency")
assert(scalar(concurrency.fetch("group"), "concurrency group") == "sonar-${{ github.ref }}", "unexpected concurrency group")
assert(scalar(concurrency.fetch("cancel-in-progress"), "cancel-in-progress") == "false", "analysis runs must not cancel in progress")

jobs = mapping(root.fetch("jobs"), "jobs")
assert(jobs.keys == ["analyze"], "workflow must define only the analyze job")
analyze = mapping(jobs.fetch("analyze"), "analyze job")
assert(!analyze.key?("services"), "analyze job must not define services")
runner_labels = sequence(analyze.fetch("runs-on"), "runs-on").map { |node| scalar(node, "runner label") }
assert(runner_labels == ["self-hosted", "linux", "ARM64", "rvt-sonar"], "unexpected self-hosted runner labels")
assert(scalar(analyze.fetch("timeout-minutes"), "timeout-minutes") == "120", "analysis timeout must be 120 minutes")

job_environment = mapping(analyze.fetch("env"), "analyze job env")
expected_connection = "Host=rvt-sonar-db;Port=5432;Database=rvt_sonar_ci;Username=postgres;Password=postgres"
assert(scalar(job_environment.fetch("RVT_TEST_POSTGRES_CONNECTION"), "test PostgreSQL connection") == expected_connection, "unexpected test PostgreSQL connection")
assert(scalar(job_environment.fetch("RVT__POSTGRES_INTEGRATION_CONNECTION"), "integration PostgreSQL connection") == expected_connection, "unexpected integration PostgreSQL connection")

steps = sequence(analyze.fetch("steps"), "analyze steps").map { |node| mapping(node, "step") }
uses_steps = steps.select { |step| step.key?("uses") }
assert(uses_steps.length == 4, "workflow must use four setup actions")
uses_steps.each do |step|
  action = scalar(step.fetch("uses"), "action reference")
  assert(action.match?(%r{\Aactions/[A-Za-z0-9_.-]+@[0-9a-f]{40}\z}), "action must be pinned to a commit SHA: #{action}")
end

expected_actions = {
  "actions/checkout" => "34e114876b0b11c390a56381ad16ebd13914f8d5",
  "actions/setup-java" => "c1e323688fd81a25caa38c78aa6df2d33d3e20d9",
  "actions/setup-dotnet" => "67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
  "actions/setup-node" => "49933ea5288caeca8642d1e84afbd3f7d6820020"
}
expected_actions.each do |action_name, commit|
  assert(uses_steps.any? { |step| scalar(step.fetch("uses"), "action reference") == "#{action_name}@#{commit}" }, "missing pinned #{action_name} action")
end

step_named = lambda do |name|
  steps.find { |step| step.key?("name") && scalar(step.fetch("name"), "step name") == name }
end
run = lambda do |name|
  step = step_named.call(name)
  assert(step, "missing #{name} step")
  scalar(step.fetch("run"), "#{name} run command")
end

database_setup = run.call("Prepare integration database")
assert(database_setup.include?("pg_isready -h rvt-sonar-db -U postgres -d rvt_sonar_ci"), "database setup must wait for rvt-sonar-db")
assert(database_setup.include?("psql -h rvt-sonar-db -U postgres -d rvt_sonar_ci -v ON_ERROR_STOP=1"), "database setup must fail closed")
assert(database_setup.include?("CREATE EXTENSION IF NOT EXISTS timescaledb;"), "database setup must create timescaledb")
assert(database_setup.include?("CREATE EXTENSION IF NOT EXISTS pgcrypto;"), "database setup must create pgcrypto")

install_tools = run.call("Install analysis tools")
assert(install_tools.include?("dotnet-sonarscanner --tool-path .sonar --version 11.2.1"), "scanner version must be pinned")
assert(install_tools.include?("dotnet-coverage --tool-path .sonar --version 18.9.0"), "coverage tool version must be pinned")

begin_step = step_named.call("Begin SonarQube analysis")
assert(begin_step, "missing Begin SonarQube analysis step")
begin_environment = mapping(begin_step.fetch("env"), "Begin SonarQube analysis env")
assert(scalar(begin_environment.fetch("SONAR_TOKEN"), "scanner token") == "${{ secrets.SONAR_TOKEN }}", "scanner must use the SONAR_TOKEN secret")
begin_analysis = scalar(begin_step.fetch("run"), "Begin SonarQube analysis run command")
[
  "/k:aileron-forward_rvt-mono",
  "/o:aileron-forward",
  "/d:sonar.host.url=https://sonarcloud.io",
  "/d:sonar.token=\"${SONAR_TOKEN}\"",
  "/d:sonar.cs.vscoveragexml.reportsPaths=artifacts/coverage/coverage.xml",
  "/d:sonar.javascript.lcov.reportPaths=apps/portal/RvtPortal.Client/coverage/lcov.info",
  "/d:sonar.qualitygate.wait=true",
  "/d:sonar.qualitygate.timeout=600"
].each { |required| assert(begin_analysis.include?(required), "scanner begin must contain #{required}") }

build = run.call("Restore and build monorepo")
assert(build.include?("dotnet restore Rvt.Mono.slnx --disable-parallel"), "workflow must restore the monorepo solution")
assert(build.include?("dotnet build Rvt.Mono.slnx --configuration Release --no-restore --no-incremental --nologo -m:1"), "workflow must build the monorepo serially")

coverage = run.call("Collect .NET coverage")
assert(coverage.include?("dotnet test Rvt.Mono.slnx --configuration Release --no-build --no-restore --nologo -m:1"), "workflow must test the monorepo serially")
assert(coverage.include?("-o artifacts/coverage/coverage.xml"), "workflow must emit .NET coverage XML")
assert(coverage.include?("test -s artifacts/coverage/coverage.xml"), "workflow must require nonempty .NET coverage")

portal_coverage = step_named.call("Collect Portal client coverage")
assert(portal_coverage, "missing Collect Portal client coverage step")
assert(scalar(portal_coverage.fetch("working-directory"), "Portal coverage working directory") == "apps/portal/RvtPortal.Client", "unexpected Portal coverage working directory")
portal_coverage_run = scalar(portal_coverage.fetch("run"), "Portal coverage run command")
assert(portal_coverage_run.include?("npm ci"), "Portal coverage must install locked dependencies")
assert(portal_coverage_run.include?("npm run test:coverage"), "Portal coverage must run tests")
assert(portal_coverage_run.include?("test -s coverage/lcov.info"), "Portal coverage must require nonempty LCOV")

end_step = step_named.call("End SonarQube analysis")
assert(end_step, "missing End SonarQube analysis step")
end_environment = mapping(end_step.fetch("env"), "End SonarQube analysis env")
assert(scalar(end_environment.fetch("SONAR_TOKEN"), "scanner completion token") == "${{ secrets.SONAR_TOKEN }}", "scanner completion must use the SONAR_TOKEN secret")
assert(scalar(end_step.fetch("run"), "End SonarQube analysis run command").include?("dotnet-sonarscanner end /d:sonar.token=\"${SONAR_TOKEN}\""), "workflow must complete SonarQube analysis")

step_text = steps.flat_map { |step| step.values.flat_map { |node| text_values(node) } }.join("\n")
assert(!step_text.match?(/docker\.sock/i), "workflow steps must not reference the Docker socket")
assert(!step_text.match?(/\bdocker\b/i), "workflow steps must not invoke Docker")

puts "verify-manual-sonarqube-workflow: PASS"
RUBY
