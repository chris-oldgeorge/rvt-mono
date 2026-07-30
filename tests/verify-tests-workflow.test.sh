#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow_path="${repo_root}/.github/workflows/tests.yml"

if [[ ! -f "${workflow_path}" ]]; then
  printf 'FAIL: missing automatic tests workflow.\n' >&2
  exit 1
fi

ruby - "${workflow_path}" <<'RUBY'
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

def steps_of(job, context)
  sequence(job.fetch("steps"), "#{context} steps").map { |node| mapping(node, "#{context} step") }
end

def run_commands(steps)
  steps.select { |step| step.key?("run") }.map { |step| scalar(step.fetch("run"), "run command") }
end

REQUIRED_JOBS = %w[client dotnet repository-guards].freeze

REQUIRED_GUARD_SCRIPTS = %w[
  scripts/verify-postgresql-only.sh
  scripts/verify-mono-layout.sh
  scripts/verify-mono-solution.sh
  scripts/verify-rvt-common-source-boundary.sh
  scripts/verify-documentation-layout.sh
].freeze

PINNED_ACTIONS = {
  "actions/checkout" => "d23441a48e516b6c34aea4fa41551a30e30af803",
  "actions/setup-dotnet" => "26b0ec14cb23fa6904739307f278c14f94c95bf1",
  "actions/setup-node" => "249970729cb0ef3589644e2896645e5dc5ba9c38"
}.freeze

def verify_workflow(source)
  root = mapping(Psych.parse(source).root, "workflow root")
  assert(
    root.keys.sort == %w[concurrency jobs name on permissions],
    "workflow root keys must be exactly concurrency, jobs, name, on, permissions; got #{root.keys.sort.join(', ')}"
  )

  assert(
    root.fetch("on").is_a?(Psych::Nodes::Scalar) &&
      scalar(root.fetch("on"), "workflow event") == "pull_request",
    "tests must run automatically for ordinary pull requests"
  )

  permissions = mapping(root.fetch("permissions"), "permissions")
  assert(permissions.keys == ["contents"], "workflow must grant only contents permission")
  assert(
    scalar(permissions.fetch("contents"), "contents permission") == "read",
    "contents permission must be read-only"
  )

  concurrency = mapping(root.fetch("concurrency"), "concurrency")
  assert(
    scalar(concurrency.fetch("group"), "concurrency group") ==
      "tests-${{ github.event.pull_request.number }}",
    "unexpected workflow concurrency group"
  )
  assert(
    scalar(concurrency.fetch("cancel-in-progress"), "cancel-in-progress") == "true",
    "superseded pull-request test runs must be cancelled"
  )

  jobs = mapping(root.fetch("jobs"), "jobs")
  assert(
    jobs.keys.sort == REQUIRED_JOBS,
    "workflow must define exactly #{REQUIRED_JOBS.join(', ')}; got #{jobs.keys.sort.join(', ')}"
  )

  jobs.each do |job_name, job_node|
    job = mapping(job_node, "#{job_name} job")
    assert(
      scalar(job.fetch("runs-on"), "#{job_name} runner") == "ubuntu-latest",
      "#{job_name} must run on a GitHub-hosted runner"
    )
    assert(job.key?("timeout-minutes"), "#{job_name} must declare a timeout")
    assert(
      !job.key?("permissions"),
      "#{job_name} must not widen the read-only workflow permissions"
    )
    assert(
      !job.key?("continue-on-error"),
      "#{job_name} must block the pull request when it fails"
    )

    steps = steps_of(job, job_name)
    steps.each do |step|
      name = step.key?("name") ? scalar(step.fetch("name"), "step name") : "unnamed step"
      assert(
        !step.key?("continue-on-error"),
        "#{job_name} step '#{name}' must block the pull request when it fails"
      )
      assert(
        !step.key?("if"),
        "#{job_name} step '#{name}' must not be conditional"
      )
      next unless step.key?("uses")

      action = scalar(step.fetch("uses"), "action reference")
      assert(
        action.match?(%r{\Aactions/[A-Za-z0-9_.-]+@[0-9a-f]{40}\z}),
        "action must be pinned to a commit SHA: #{action}"
      )
      action_name, commit = action.split("@")
      assert(
        PINNED_ACTIONS.fetch(action_name, commit) == commit,
        "#{action_name} must stay pinned to #{PINNED_ACTIONS[action_name]}"
      )
    end

    checkout = steps.select do |step|
      step.key?("uses") && scalar(step.fetch("uses"), "action reference").start_with?("actions/checkout@")
    end
    assert(checkout.length == 1, "#{job_name} must check out the repository exactly once")
    assert(
      scalar(mapping(checkout.fetch(0).fetch("with"), "checkout inputs").fetch("fetch-depth"), "fetch-depth") == "0",
      "#{job_name} must check out full history"
    )
  end

  dotnet = mapping(jobs.fetch("dotnet"), "dotnet job")
  dotnet_steps = steps_of(dotnet, "dotnet")
  services = mapping(dotnet.fetch("services"), "dotnet services")
  assert(services.keys == ["timescaledb"], "the .NET job must supply exactly the integration database")
  database = mapping(services.fetch("timescaledb"), "timescaledb service")
  assert(
    scalar(database.fetch("image"), "database image") == "timescale/timescaledb:2.28.3-pg17",
    "unexpected integration database image"
  )
  assert(
    sequence(database.fetch("ports"), "database ports").map { |node| scalar(node, "port") } == ["55432:5432"],
    "the integration database must be published on the established port"
  )

  test_step = dotnet_steps.find do |step|
    step.key?("env") && mapping(step.fetch("env"), "step environment").key?("RVT__POSTGRES_INTEGRATION_CONNECTION")
  end
  assert(test_step, "the .NET job must supply the integration connection to the test step")
  connection = scalar(
    mapping(test_step.fetch("env"), "test environment").fetch("RVT__POSTGRES_INTEGRATION_CONNECTION"),
    "integration connection"
  )
  assert(
    connection.include?("Port=55432") && connection.include?("Database=rvt_integration"),
    "the integration connection must point at the job's service container"
  )

  dotnet_commands = run_commands(dotnet_steps)
  assert(
    dotnet_commands.any? { |command| command.include?("--locked-mode") },
    "the .NET job must restore with locked dependencies"
  )
  assert(
    dotnet_commands.any? { |command| command.start_with?("dotnet test Rvt.Mono.slnx") },
    "the .NET job must run the whole monorepo test suite"
  )

  client_commands = run_commands(steps_of(mapping(jobs.fetch("client"), "client job"), "client"))
  assert(
    client_commands.any? { |command| command.include?("npm ci --ignore-scripts") },
    "the client job must install with the committed lockfile"
  )
  assert(
    client_commands.any? { |command| command.include?("tsc -b") },
    "the client job must type-check the portal client"
  )
  assert(
    client_commands.any? { |command| command.include?("npm run test:run") },
    "the client job must run the portal client tests"
  )

  guard_commands = run_commands(steps_of(mapping(jobs.fetch("repository-guards"), "guards job"), "repository-guards"))
  REQUIRED_GUARD_SCRIPTS.each do |script|
    assert(
      guard_commands.any? { |command| command.include?(script) },
      "the repository-guards job must run #{script}"
    )
  end
  assert(
    guard_commands.any? { |command| command.include?("tests/*.test.sh") },
    "the repository-guards job must run every repository contract test"
  )
end

source = File.read(ARGV.fetch(0), encoding: "utf-8")
verify_workflow(source)

mutations = {
  "manual trigger" => ["on: pull_request", "on: workflow_dispatch"],
  "write permission" => ["  contents: read", "  contents: write"],
  "no cancellation" => ["  cancel-in-progress: true", "  cancel-in-progress: false"],
  "shallow checkout" => ["          fetch-depth: 0", "          fetch-depth: 1"],
  "unpinned checkout" => [
    "actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803",
    "actions/checkout@v6"
  ],
  "rolled-back checkout" => [
    "actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803",
    "actions/checkout@0000000000000000000000000000000000000000"
  ],
  "self-hosted runner" => ["    runs-on: ubuntu-latest", "    runs-on: self-hosted"],
  "nonblocking test step" => [
    "      - name: Run .NET tests\n",
    "      - name: Run .NET tests\n        continue-on-error: true\n"
  ],
  "conditional test step" => [
    "      - name: Run .NET tests\n",
    "      - name: Run .NET tests\n        if: ${{ false }}\n"
  ],
  "unlocked restore" => [" --locked-mode --disable-parallel", " --disable-parallel"],
  "narrowed test scope" => [
    "run: dotnet test Rvt.Mono.slnx --no-restore --verbosity minimal",
    "run: dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj"
  ],
  "unpinned database image" => [
    "image: timescale/timescaledb:2.28.3-pg17",
    "image: timescale/timescaledb:latest-pg17"
  ],
  "removed integration connection" => [
    "        env:\n" \
      "          # Throwaway credentials for the ephemeral service container above.\n" \
      "          RVT__POSTGRES_INTEGRATION_CONNECTION: Host=localhost;Port=55432;Database=rvt_integration;Username=postgres;Password=postgres\n",
    ""
  ],
  "removed client type check" => [
    "      - name: Type-check Portal client\n" \
      "        working-directory: apps/portal/RvtPortal.Client\n" \
      "        run: node node_modules/typescript/bin/tsc -b\n\n",
    ""
  ],
  "removed client tests" => [
    "      - name: Run Portal client tests\n" \
      "        working-directory: apps/portal/RvtPortal.Client\n" \
      "        run: npm run test:run\n",
    ""
  ],
  "removed documentation guard" => [
    "      - name: Verify documentation layout\n" \
      "        run: scripts/verify-documentation-layout.sh\n",
    ""
  ],
  "narrowed contract-test glob" => [
    "for contract_test in tests/*.test.sh; do",
    "for contract_test in tests/verify-mono-layout.test.sh; do"
  ],
  "removed guards job" => [
    "  repository-guards:\n    name: Repository guards\n",
    "  repository-guards-disabled:\n    name: Repository guards\n"
  ],
  "job permission override" => [
    "    timeout-minutes: 60\n",
    "    timeout-minutes: 60\n    permissions:\n      contents: write\n"
  ]
}

accepted_mutations = []
mutations.each do |label, (needle, replacement)|
  mutated = source.sub(needle, replacement)
  raise "#{label} mutation did not change workflow" if mutated == source

  begin
    verify_workflow(mutated)
  rescue VerificationFailure, KeyError, Psych::SyntaxError
    next
  end
  accepted_mutations << label
end
raise "mutations were accepted: #{accepted_mutations.join(', ')}" unless accepted_mutations.empty?

puts "verify-tests-workflow: PASS"
RUBY
