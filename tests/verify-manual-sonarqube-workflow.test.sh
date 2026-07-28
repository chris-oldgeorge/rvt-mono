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

def shell_command_words(command)
  # Deliberately conservative: quoted/subshell command text is tokenized too,
  # so ambiguous embedded invocations fail closed instead of evading the guard.
  command.tr(%q{"'()}, "    ").split
end

def executable_basename(word)
  word.to_s.tr("\\", "/").split("/").last.to_s.downcase
end

def dotnet_executable_word?(word)
  %w[dotnet dotnet.exe].include?(executable_basename(word))
end

def npm_executable_word?(word)
  %w[npm npm.cmd].include?(executable_basename(word))
end

def bounded_executable_token_occurrences(command, token)
  words = shell_command_words(command)
  words.each_index.count do |executable_index|
    next false unless yield(words[executable_index])

    next_executable_index = ((executable_index + 1)...words.length).find do |index|
      yield(words[index])
    end || words.length

    words[(executable_index + 1)...next_executable_index].include?(token)
  end
end

def monorepo_phase_occurrences(command, phase)
  # Intentionally fail closed for targetless/root-directory invocations and
  # quoted embedded command text; canonical command checks reject ambiguity.
  bounded_executable_token_occurrences(command, phase) do |word|
    dotnet_executable_word?(word)
  end
end

def standards_occurrences(command)
  command.scan(
    %r{scripts/verify-engineering-standards\.sh(?=[[:space:]"']|\z)}
  ).length
end

def npm_ci_occurrences(command)
  # Intentionally fail closed for wrappers, arbitrary option/value pairs, and
  # quoted embedded command text; an exact ci token before the next npm counts.
  bounded_executable_token_occurrences(command, "ci") do |word|
    npm_executable_word?(word)
  end
end

def step_name(step)
  step.key?("name") ? scalar(step.fetch("name"), "step name") : nil
end

def named_step_index(steps, name)
  indexes = steps.each_index.select { |index| step_name(steps.fetch(index)) == name }
  assert(indexes.length == 1, "#{name} step must occur exactly once")
  indexes.fetch(0)
end

def run_commands(step, context)
  assert(step.key?("run"), "#{context} must define a run command")
  logical_shell_commands(scalar(step.fetch("run"), "#{context} run command"))
end

def workflow_invocation_positions(steps)
  positions = []
  steps.each_index do |step_index|
    step = steps.fetch(step_index)
    next unless step.key?("run")

    commands = run_commands(step, step_name(step) || "unnamed step")
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

def assert_engineering_standards_gate(steps)
  node_indexes = steps.each_index.select do |index|
    step = steps.fetch(index)
    step.key?("uses") &&
      scalar(step.fetch("uses"), "action reference").match?(
        %r{\Aactions/setup-node@[0-9a-f]{40}\z}
      )
  end
  assert(node_indexes.length == 1, "SHA-pinned Node setup must occur exactly once")
  node_index = node_indexes.fetch(0)

  install_index = named_step_index(steps, "Install Portal client dependencies")
  restore_index = named_step_index(steps, "Restore monorepo")
  standards_index = named_step_index(steps, "Verify engineering standards")
  build_index = named_step_index(steps, "Build monorepo (Release)")
  test_index = named_step_index(steps, "Collect .NET coverage")
  portal_coverage_index = named_step_index(steps, "Collect Portal client coverage")

  assert(
    install_index == node_index + 1,
    "Portal dependencies must be installed immediately after Node setup"
  )

  install_step = steps.fetch(install_index)
  restore_step = steps.fetch(restore_index)
  standards_step = steps.fetch(standards_index)
  build_step = steps.fetch(build_index)
  test_step = steps.fetch(test_index)
  portal_coverage_step = steps.fetch(portal_coverage_index)

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
    "unexpected Portal install working directory"
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
      run_commands(step, description) == expected,
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

  canonical_test =
    "dotnet test Rvt.Mono.slnx --configuration Release --no-build " \
      "--no-restore --nologo -m:1"
  canonical_test_pattern = /"#{Regexp.escape(canonical_test)}"/
  assert(
    run_commands(test_step, "Collect .NET coverage").sum do |command|
      command.scan(canonical_test_pattern).length
    end == 1,
    "workflow monorepo test must use exactly one canonical invocation"
  )
  assert(
    run_commands(portal_coverage_step, "Collect Portal client coverage")
      .sum { |command| npm_ci_occurrences(command) }
      .zero?,
    "Portal coverage must not reinstall dependencies"
  )
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

assert_engineering_standards_gate(steps)
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

coverage = run.call("Collect .NET coverage")
assert(coverage.include?("dotnet test Rvt.Mono.slnx --configuration Release --no-build --no-restore --nologo -m:1"), "workflow must test the monorepo serially")
assert(coverage.include?("-o artifacts/coverage/coverage.xml"), "workflow must emit .NET coverage XML")
assert(coverage.include?("test -s artifacts/coverage/coverage.xml"), "workflow must require nonempty .NET coverage")

portal_coverage = step_named.call("Collect Portal client coverage")
assert(portal_coverage, "missing Collect Portal client coverage step")
assert(scalar(portal_coverage.fetch("working-directory"), "Portal coverage working directory") == "apps/portal/RvtPortal.Client", "unexpected Portal coverage working directory")
portal_coverage_run = scalar(portal_coverage.fetch("run"), "Portal coverage run command")
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
