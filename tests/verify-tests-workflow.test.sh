#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow_path="${repo_root}/.github/workflows/tests.yml"
action_path="${repo_root}/.github/actions/setup-monorepo/action.yml"

if [[ ! -f "${workflow_path}" ]]; then
  printf 'FAIL: missing automatic tests workflow.\n' >&2
  exit 1
fi

if [[ ! -f "${action_path}" ]]; then
  printf 'FAIL: missing shared setup-monorepo composite action.\n' >&2
  exit 1
fi

ruby - "${workflow_path}" "${action_path}" <<'RUBY'
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

REQUIRED_JOBS = %w[changes client dotnet repository-guards].freeze

# Jobs that may skip on documentation-only changes; every other job must run
# unconditionally (repository-guards is what verifies the documentation).
CHANGE_GATED_JOBS = %w[client dotnet].freeze

CHANGE_GATE = "needs.changes.outputs.code != 'false'".freeze

CLIENT_RELEASE_GATE = "${{ hashFiles('RELEASE_SOURCE.json') == '' }}".freeze

CLIENT_RELEASE_ONLY_SKIPS = [
  "Verify documentation layout",
  "Verify repository contract tests"
].freeze

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

SETUP_ACTION_REFERENCE = "./.github/actions/setup-monorepo".freeze

def assert_pinned_action(reference, context)
  assert(
    reference.match?(%r{\Aactions/[A-Za-z0-9_.-]+@[0-9a-f]{40}\z}),
    "#{context}: action must be pinned to a commit SHA: #{reference}"
  )
  action_name, commit = reference.split("@")
  assert(
    PINNED_ACTIONS.fetch(action_name, commit) == commit,
    "#{action_name} must stay pinned to #{PINNED_ACTIONS[action_name]}"
  )
end

def verify_workflow(source)
  root = mapping(Psych.parse(source).root, "workflow root")
  assert(
    root.keys.sort == %w[concurrency jobs name on permissions],
    "workflow root keys must be exactly concurrency, jobs, name, on, permissions; got #{root.keys.sort.join(', ')}"
  )

  on = mapping(root.fetch("on"), "workflow triggers")
  assert(
    on.keys.sort == %w[pull_request push],
    "tests must run for ordinary pull requests and pushes to main"
  )
  assert(
    on.fetch("pull_request").is_a?(Psych::Nodes::Scalar) &&
      scalar(on.fetch("pull_request"), "pull_request trigger").empty?,
    "the pull_request trigger must stay unfiltered (path filtering happens in the changes job)"
  )
  push = mapping(on.fetch("push"), "push trigger")
  assert(push.keys == ["branches"], "push trigger must only filter branches")
  assert(
    sequence(push.fetch("branches"), "push branches").map { |node| scalar(node, "branch") } == ["main"],
    "push trigger must cover exactly main"
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
      "tests-${{ github.event.pull_request.number || github.ref }}",
    "unexpected workflow concurrency group"
  )
  assert(
    scalar(concurrency.fetch("cancel-in-progress"), "cancel-in-progress") ==
      "${{ github.event_name == 'pull_request' }}",
    "superseded pull-request runs must be cancelled and pushes must not be"
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

    if CHANGE_GATED_JOBS.include?(job_name)
      assert(
        scalar(job.fetch("needs"), "#{job_name} needs") == "changes",
        "#{job_name} must wait for change detection"
      )
      assert(
        scalar(job.fetch("if"), "#{job_name} condition") == CHANGE_GATE,
        "#{job_name} may only skip on documentation-only changes"
      )
    else
      assert(
        !job.key?("if") && !job.key?("needs"),
        "#{job_name} must run unconditionally"
      )
    end

    steps = steps_of(job, job_name)
    steps.each do |step|
      name = step.key?("name") ? scalar(step.fetch("name"), "step name") : "unnamed step"
      assert(
        !step.key?("continue-on-error"),
        "#{job_name} step '#{name}' must block the pull request when it fails"
      )
      if CLIENT_RELEASE_ONLY_SKIPS.include?(name)
        assert(
          step.key?("if") &&
            scalar(step.fetch("if"), "#{job_name} step '#{name}' condition") == CLIENT_RELEASE_GATE,
          "#{job_name} step '#{name}' must skip only for curated client releases"
        )
      else
        assert(
          !step.key?("if"),
          "#{job_name} step '#{name}' must not be conditional"
        )
      end
      next unless step.key?("uses")

      reference = scalar(step.fetch("uses"), "action reference")
      next if reference == SETUP_ACTION_REFERENCE

      assert_pinned_action(reference, "#{job_name} step '#{name}'")
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

  changes = mapping(jobs.fetch("changes"), "changes job")
  outputs = mapping(changes.fetch("outputs"), "changes outputs")
  assert(outputs.keys == ["code"], "changes job must expose exactly the code output")
  assert(
    scalar(outputs.fetch("code"), "code output") == "${{ steps.detect.outputs.code }}",
    "code output must come from the detect step"
  )
  changes_steps = steps_of(changes, "changes")
  detect = changes_steps.find { |step| step.key?("id") && scalar(step.fetch("id"), "step id") == "detect" }
  assert(detect, "the changes job must have the detect step")
  assert(
    scalar(detect.fetch("run"), "detect command") ==
      'scripts/detect-code-changes.sh --base "${BASE_SHA}" --head "${GITHUB_SHA}"',
    "detect step must run the canonical change-detection script"
  )
  assert(
    scalar(mapping(detect.fetch("env"), "detect environment").fetch("BASE_SHA"), "BASE_SHA") ==
      "${{ github.event.pull_request.base.sha || github.event.before }}",
    "BASE_SHA must cover both pull-request and push events"
  )

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

  assert(
    dotnet_steps.any? do |step|
      step.key?("uses") && scalar(step.fetch("uses"), "action reference") == SETUP_ACTION_REFERENCE
    end,
    "the .NET job must set up via the shared setup-monorepo composite action"
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

def verify_action(source)
  root = mapping(Psych.parse(source).root, "action root")
  runs = mapping(root.fetch("runs"), "action runs")
  assert(
    scalar(runs.fetch("using"), "action using") == "composite",
    "setup-monorepo must stay a composite action"
  )

  steps = sequence(runs.fetch("steps"), "action steps").map { |node| mapping(node, "action step") }
  steps.each do |step|
    name = step.key?("name") ? scalar(step.fetch("name"), "action step name") : "unnamed step"
    assert(!step.key?("if"), "#{name} must not be conditional")
    assert(!step.key?("continue-on-error"), "#{name} must block when it fails")
    assert_pinned_action(scalar(step.fetch("uses"), "action reference"), name) if step.key?("uses")
  end

  commands = run_commands(steps)
  assert(
    commands.any? { |command| command.include?("dotnet restore Rvt.Mono.slnx --locked-mode") },
    "the setup preamble must restore with locked dependencies"
  )
  assert(
    commands.any? { |command| command.include?("npm ci --ignore-scripts") },
    "the setup preamble must install the Portal client with the committed lockfile"
  )
end

workflow_source = File.read(ARGV.fetch(0), encoding: "utf-8")
action_source = File.read(ARGV.fetch(1), encoding: "utf-8")
verify_workflow(workflow_source)
verify_action(action_source)

on_block = "on:\n  pull_request:\n  push:\n    branches: [main]\n"

workflow_mutations = {
  "manual trigger" => [on_block, "on: workflow_dispatch\n"],
  "removed push trigger" => [on_block, "on: pull_request\n"],
  "push to any branch" => ["    branches: [main]\n", ""],
  "write permission" => ["  contents: read", "  contents: write"],
  "no cancellation" => [
    "  cancel-in-progress: ${{ github.event_name == 'pull_request' }}",
    "  cancel-in-progress: false"
  ],
  "cancellation of pushes" => [
    "  cancel-in-progress: ${{ github.event_name == 'pull_request' }}",
    "  cancel-in-progress: true"
  ],
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
  "bypassed change detection" => [
    'run: scripts/detect-code-changes.sh --base "${BASE_SHA}" --head "${GITHUB_SHA}"',
    'run: echo code=false >>"${GITHUB_OUTPUT}"'
  ],
  "always-skipped dotnet job" => [
    "  dotnet:\n    name: .NET tests\n    needs: changes\n    if: needs.changes.outputs.code != 'false'\n",
    "  dotnet:\n    name: .NET tests\n    needs: changes\n    if: ${{ false }}\n"
  ],
  "conditional guards job" => [
    "  repository-guards:\n    name: Repository guards\n",
    "  repository-guards:\n    name: Repository guards\n    needs: changes\n    if: needs.changes.outputs.code == 'true'\n"
  ],
  "unshared dotnet setup" => [
    "      - name: Set up monorepo toolchain\n        uses: ./.github/actions/setup-monorepo\n\n",
    ""
  ],
  "nonblocking test step" => [
    "      - name: Run .NET tests\n",
    "      - name: Run .NET tests\n        continue-on-error: true\n"
  ],
  "conditional test step" => [
    "      - name: Run .NET tests\n",
    "      - name: Run .NET tests\n        if: ${{ false }}\n"
  ],
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
      "        if: ${{ hashFiles('RELEASE_SOURCE.json') == '' }}\n" \
      "        run: scripts/verify-documentation-layout.sh\n\n",
    ""
  ],
  "unconditional documentation guard" => [
    "      - name: Verify documentation layout\n" \
      "        if: ${{ hashFiles('RELEASE_SOURCE.json') == '' }}\n",
    "      - name: Verify documentation layout\n"
  ],
  "wrong contract-test release gate" => [
    "      - name: Verify repository contract tests\n" \
      "        if: ${{ hashFiles('RELEASE_SOURCE.json') == '' }}\n",
    "      - name: Verify repository contract tests\n" \
      "        if: ${{ false }}\n"
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

action_mutations = {
  "unlocked restore" => [" --locked-mode --disable-parallel", " --disable-parallel"],
  "unpinned setup-dotnet" => [
    "actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1",
    "actions/setup-dotnet@v4"
  ],
  "rolled-back setup-node" => [
    "actions/setup-node@249970729cb0ef3589644e2896645e5dc5ba9c38",
    "actions/setup-node@0000000000000000000000000000000000000000"
  ],
  "unscripted npm install" => ["npm ci --ignore-scripts", "npm install"],
  "conditional restore step" => [
    "    - name: Restore monorepo\n",
    "    - name: Restore monorepo\n      if: ${{ false }}\n"
  ]
}

accepted_mutations = []

workflow_mutations.each do |label, (needle, replacement)|
  mutated = workflow_source.sub(needle, replacement)
  raise "#{label} mutation did not change workflow" if mutated == workflow_source

  begin
    verify_workflow(mutated)
  rescue VerificationFailure, KeyError, Psych::SyntaxError
    next
  end
  accepted_mutations << label
end

action_mutations.each do |label, (needle, replacement)|
  mutated = action_source.sub(needle, replacement)
  raise "#{label} mutation did not change action" if mutated == action_source

  begin
    verify_action(mutated)
  rescue VerificationFailure, KeyError, Psych::SyntaxError
    next
  end
  accepted_mutations << label
end

raise "mutations were accepted: #{accepted_mutations.join(', ')}" unless accepted_mutations.empty?

puts "verify-tests-workflow: PASS"
RUBY
