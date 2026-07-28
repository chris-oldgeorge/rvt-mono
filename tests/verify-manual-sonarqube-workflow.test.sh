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

assert(!analyze.key?("env"), "database connections must be exported after a unique per-run database is created")

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

dotnet_setup = uses_steps.find do |step|
  scalar(step.fetch("uses"), "action reference").start_with?("actions/setup-dotnet@")
end
dotnet_environment = mapping(dotnet_setup.fetch("env"), "Set up .NET 10 env")
assert(dotnet_environment.keys == ["DOTNET_INSTALL_DIR"], "Set up .NET 10 must only override the install directory")
assert(
  scalar(dotnet_environment.fetch("DOTNET_INSTALL_DIR"), "DOTNET_INSTALL_DIR") == "${{ runner.temp }}/dotnet",
  "Set up .NET 10 must install into the runner-owned temporary directory"
)

step_named = lambda do |name|
  steps.find { |step| step.key?("name") && scalar(step.fetch("name"), "step name") == name }
end
run = lambda do |name|
  step = step_named.call(name)
  assert(step, "missing #{name} step")
  scalar(step.fetch("run"), "#{name} run command")
end

def workflow_steps(source)
  root = mapping(Psych.parse(source).root, "workflow root")
  jobs = mapping(root.fetch("jobs"), "jobs")
  analyze = mapping(jobs.fetch("analyze"), "analyze job")
  sequence(analyze.fetch("steps"), "analyze steps").map { |node| mapping(node, "step") }
end

def assert_database_lifecycle(steps)
  named_step = lambda do |name|
    steps.find { |step| step.key?("name") && scalar(step.fetch("name"), "step name") == name }
  end
  run = lambda do |name|
    step = named_step.call(name)
    assert(step, "missing #{name} step")
    scalar(step.fetch("run"), "#{name} run command")
  end

  database_setup = run.call("Prepare integration database")
  database_identity = 'database_name="rvt_sonar_${{ github.run_id }}_${{ github.run_attempt }}"'
  assert(database_setup.include?(database_identity), "database setup must derive a unique database name from the run identity")
  assert(database_setup.include?("pg_isready -h rvt-sonar-db -U postgres -d postgres"), "database setup must wait for rvt-sonar-db through the admin database")
  assert(database_setup.include?('DROP DATABASE IF EXISTS :"database_name";'), "database setup must force-drop a stale job database")
  assert(database_setup.include?('CREATE DATABASE :"database_name";'), "database setup must create the job database")
  assert(database_setup.include?("CREATE EXTENSION IF NOT EXISTS timescaledb;"), "database setup must create timescaledb")
  assert(database_setup.include?("CREATE EXTENSION IF NOT EXISTS pgcrypto;"), "database setup must create pgcrypto")
  ["RVT_TEST_POSTGRES_CONNECTION", "RVT__POSTGRES_INTEGRATION_CONNECTION", "RVT_EF_CONNECTION", "RVT_DEPLOY_CONNECTION"].each do |variable|
    assert(database_setup.include?("#{variable}=Host=rvt-sonar-db;Port=5432;Database=${database_name};Username=postgres;Password=postgres"), "database setup must export #{variable}")
  end

  deploy = run.call("Deploy Portal database")
  expected_migrations = [
    ["RVTDbContext", "apps/portal/RVT.DataAccess/RVT.DataAccess.csproj", "apps/portal/RVT.DataAccess/RVT.DataAccess.csproj"],
    ["RVTSearchContext", "apps/portal/RVT.DataAccess/RVT.DataAccess.csproj", "apps/portal/RVT.DataAccess/RVT.DataAccess.csproj"],
    ["ApplicationDbContext", "apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj", "apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj"]
  ]
  migration_commands = deploy.scan(%r{\./\.sonar/dotnet-ef database update.*?(?=\./\.sonar/dotnet-ef database update|dotnet run|\z)}m)
  assert(migration_commands.length == 3, "database deployment must run exactly three EF migration commands")
  actual_migrations = migration_commands.map do |command|
    [
      command[/--context\s+(\S+)/, 1],
      command[/--project\s+(\S+)/, 1],
      command[/--startup-project\s+(\S+)/, 1]
    ]
  end
  assert(actual_migrations == expected_migrations, "database deployment must use the canonical EF contexts, projects, and startup projects")
  assert(migration_commands.all? { |command| command.include?("--no-build --configuration Release") }, "database deployment must use the job-local EF tool")
  assert(deploy.include?("dotnet run --project apps/portal/RVT.SchemaDeploy/RVT.SchemaDeploy.csproj"), "database deployment must run RVT.SchemaDeploy")
  assert(deploy.include?("--connection \"${RVT_DEPLOY_CONNECTION}\""), "database deployment must pass the deployment connection explicitly")

  cleanup = named_step.call("Drop integration database")
  assert(cleanup, "missing Drop integration database step")
  assert(scalar(cleanup.fetch("if"), "Drop integration database condition") == "always()", "database cleanup must run unconditionally")
  cleanup_run = scalar(cleanup.fetch("run"), "Drop integration database run command")
  assert(cleanup_run.include?(database_identity), "database cleanup must target the job-specific database")
  assert(cleanup_run.include?('DROP DATABASE IF EXISTS :"database_name";'), "database cleanup must force-drop the job database")
  assert(!cleanup_run.match?(/\bdocker\b/i), "database cleanup must not invoke Docker")

  begin_index = steps.index(named_step.call("Begin SonarQube analysis"))
  restore_index = steps.index(named_step.call("Restore monorepo"))
  standards_index = steps.index(named_step.call("Verify engineering standards"))
  build_index = steps.index(named_step.call("Build monorepo (Release)"))
  deploy_index = steps.index(named_step.call("Deploy Portal database"))
  coverage_index = steps.index(named_step.call("Collect .NET coverage"))
  assert(
    begin_index < restore_index &&
      restore_index < standards_index &&
      standards_index < build_index &&
      build_index < deploy_index &&
      deploy_index < coverage_index,
    "database deployment must occur after scanner begin, restore, standards verification, and build, before .NET coverage"
  )
end

assert_database_lifecycle(steps)

install_tools = run.call("Install analysis tools")
assert(install_tools.include?("dotnet-sonarscanner --tool-path .sonar --version 11.2.1"), "scanner version must be pinned")
assert(install_tools.include?("dotnet-coverage --tool-path .sonar --version 18.9.0"), "coverage tool version must be pinned")
assert(install_tools.include?("dotnet-ef --tool-path .sonar --version 10.0.7"), "EF tool version must be pinned")

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

node_setup_index = steps.index do |step|
  step.key?("uses") &&
    scalar(step.fetch("uses"), "action reference").start_with?("actions/setup-node@")
end
portal_install_index = steps.index(step_named.call("Install Portal client dependencies"))
assert(portal_install_index == node_setup_index + 1, "Portal dependencies must be installed immediately after Node setup")

portal_install = step_named.call("Install Portal client dependencies")
assert(
  scalar(portal_install.fetch("working-directory"), "Portal install working directory") ==
    "apps/portal/RvtPortal.Client",
  "unexpected Portal install working directory"
)
portal_install_run = scalar(portal_install.fetch("run"), "Portal install run command")
assert(portal_install_run.lines.map(&:strip).reject(&:empty?) == ["npm ci"], "Portal install must run exactly one npm ci")

restore = run.call("Restore monorepo")
assert(
  restore.lines.map(&:strip).reject(&:empty?) ==
    ["dotnet restore Rvt.Mono.slnx --disable-parallel"],
  "workflow must restore the monorepo solution exactly once"
)

standards = run.call("Verify engineering standards")
assert(
  standards.lines.map(&:strip).reject(&:empty?) ==
    ["scripts/verify-engineering-standards.sh --base auto --head HEAD"],
  "workflow must run the changed-scope engineering standards gate exactly once"
)

build = run.call("Build monorepo (Release)")
assert(
  build.lines.map(&:strip).reject(&:empty?) ==
    ["dotnet build Rvt.Mono.slnx --configuration Release --no-restore --no-incremental --nologo -m:1"],
  "workflow must build the monorepo serially in Release exactly once"
)

coverage = run.call("Collect .NET coverage")
assert(coverage.include?("dotnet test Rvt.Mono.slnx --configuration Release --no-build --no-restore --nologo -m:1"), "workflow must test the monorepo serially")
assert(coverage.include?("-o artifacts/coverage/coverage.xml"), "workflow must emit .NET coverage XML")
assert(coverage.include?("test -s artifacts/coverage/coverage.xml"), "workflow must require nonempty .NET coverage")

portal_coverage = step_named.call("Collect Portal client coverage")
assert(portal_coverage, "missing Collect Portal client coverage step")
assert(scalar(portal_coverage.fetch("working-directory"), "Portal coverage working directory") == "apps/portal/RvtPortal.Client", "unexpected Portal coverage working directory")
portal_coverage_run = scalar(portal_coverage.fetch("run"), "Portal coverage run command")
assert(!portal_coverage_run.lines.any? { |line| line.strip == "npm ci" }, "Portal coverage must not reinstall dependencies")
assert(portal_coverage_run.include?("npm run test:coverage"), "Portal coverage must run tests")
assert(portal_coverage_run.include?("test -s coverage/lcov.info"), "Portal coverage must require nonempty LCOV")

npm_ci_count = steps.sum do |step|
  next 0 unless step.key?("run")

  scalar(step.fetch("run"), "workflow step run command")
    .lines
    .count { |line| line.strip == "npm ci" }
end
assert(npm_ci_count == 1, "workflow must run npm ci exactly once")

end_step = step_named.call("End SonarQube analysis")
assert(end_step, "missing End SonarQube analysis step")
end_environment = mapping(end_step.fetch("env"), "End SonarQube analysis env")
assert(scalar(end_environment.fetch("SONAR_TOKEN"), "scanner completion token") == "${{ secrets.SONAR_TOKEN }}", "scanner completion must use the SONAR_TOKEN secret")
assert(scalar(end_step.fetch("run"), "End SonarQube analysis run command").include?("dotnet-sonarscanner end /d:sonar.token=\"${SONAR_TOKEN}\""), "workflow must complete SonarQube analysis")

step_text = steps.flat_map { |step| step.values.flat_map { |node| text_values(node) } }.join("\n")
assert(!step_text.match?(/docker\.sock/i), "workflow steps must not reference the Docker socket")
assert(!step_text.match?(/\bdocker\b/i), "workflow steps must not invoke Docker")

workflow_source = File.read(workflow_path, encoding: "utf-8")
extension_only_source = workflow_source
  .sub("          DROP DATABASE IF EXISTS :\"database_name\";\n", "")
  .sub("          CREATE DATABASE :\"database_name\";\n", "")
missing_schema_deploy_source = workflow_source.sub(
  <<'SCHEMA_DEPLOY',
          dotnet run --project apps/portal/RVT.SchemaDeploy/RVT.SchemaDeploy.csproj \
            --configuration Release --no-build -- \
            --connection "${RVT_DEPLOY_CONNECTION}"
SCHEMA_DEPLOY
  ""
)
[[extension_only_source, "extension-only database preparation"], [missing_schema_deploy_source, "missing schema deployment"]].each do |source, label|
  begin
    assert_database_lifecycle(workflow_steps(source))
  rescue RuntimeError
    next
  end
  raise "#{label} mutation was accepted"
end

puts "verify-manual-sonarqube-workflow: PASS"
RUBY
