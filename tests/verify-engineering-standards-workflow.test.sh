#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow_path="${repo_root}/.github/workflows/engineering-standards.yml"

if [[ ! -f "${workflow_path}" ]]; then
  printf 'FAIL: missing automatic engineering standards workflow.\n' >&2
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

def verify_workflow(source)
  root = mapping(Psych.parse(source).root, "workflow root")

  assert(
    root.fetch("on").is_a?(Psych::Nodes::Scalar) &&
      scalar(root.fetch("on"), "workflow event") == "pull_request",
    "workflow must run automatically for ordinary pull requests"
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
      "engineering-standards-${{ github.event.pull_request.number }}",
    "unexpected workflow concurrency group"
  )
  assert(
    scalar(concurrency.fetch("cancel-in-progress"), "cancel-in-progress") == "true",
    "superseded pull-request checks must be cancelled"
  )

  jobs = mapping(root.fetch("jobs"), "jobs")
  assert(
    jobs.keys == ["engineering-standards"],
    "workflow must define one blocking engineering-standards job"
  )
  job = mapping(jobs.fetch("engineering-standards"), "engineering-standards job")
  assert(
    scalar(job.fetch("name"), "job name") == "Engineering standards",
    "job must expose the stable Engineering standards check name"
  )
  assert(
    scalar(job.fetch("runs-on"), "runner") == "ubuntu-latest",
    "pull-request standards must run on a GitHub-hosted runner"
  )
  assert(
    scalar(job.fetch("timeout-minutes"), "timeout") == "30",
    "engineering standards timeout must be 30 minutes"
  )
  %w[continue-on-error container env if permissions services].each do |key|
    assert(!job.key?(key), "engineering-standards job must not define #{key}")
  end

  steps = sequence(job.fetch("steps"), "steps").map { |node| mapping(node, "step") }
  assert(steps.length == 8, "workflow must define exactly the eight required setup and gate steps")
  steps.each do |step|
    name = step.key?("name") ? scalar(step.fetch("name"), "step name") : "unnamed step"
    assert(!step.key?("if"), "#{name} must be unconditional")
    assert(!step.key?("continue-on-error"), "#{name} must block on failure")
  end

  uses_steps = steps.select { |step| step.key?("uses") }
  assert(uses_steps.length == 3, "workflow must use checkout, setup-dotnet, and setup-node")

  expected_actions = {
    "actions/checkout" => "34e114876b0b11c390a56381ad16ebd13914f8d5",
    "actions/setup-dotnet" => "67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
    "actions/setup-node" => "49933ea5288caeca8642d1e84afbd3f7d6820020"
  }
  expected_actions.each do |action_name, commit|
    expected = "#{action_name}@#{commit}"
    assert(
      uses_steps.one? { |step| scalar(step.fetch("uses"), "action reference") == expected },
      "missing SHA-pinned #{action_name}"
    )
  end
  uses_steps.each do |step|
    action = scalar(step.fetch("uses"), "action reference")
    assert(
      action.match?(%r{\Aactions/[A-Za-z0-9_.-]+@[0-9a-f]{40}\z}),
      "action must be pinned to a commit SHA: #{action}"
    )
  end

  named_step = lambda do |name|
    matches = steps.select do |step|
      step.key?("name") && scalar(step.fetch("name"), "step name") == name
    end
    assert(matches.length == 1, "#{name} step must occur exactly once")
    matches.fetch(0)
  end

  checkout = named_step.call("Check out repository")
  checkout_with = mapping(checkout.fetch("with"), "checkout inputs")
  assert(checkout_with.keys == ["fetch-depth"], "checkout must only set fetch-depth")
  assert(
    scalar(checkout_with.fetch("fetch-depth"), "fetch-depth") == "0",
    "changed-range verification requires full history"
  )

  dotnet_with = mapping(named_step.call("Set up .NET 10").fetch("with"), ".NET setup inputs")
  assert(
    scalar(dotnet_with.fetch("dotnet-version"), ".NET version") == "10.0.x",
    "workflow must use .NET 10"
  )

  node_with = mapping(named_step.call("Set up Node.js 24").fetch("with"), "Node setup inputs")
  assert(
    scalar(node_with.fetch("node-version"), "Node version") == "24.x",
    "workflow must use Node.js 24"
  )
  assert(
    scalar(node_with.fetch("cache"), "Node cache") == "npm" &&
      scalar(node_with.fetch("cache-dependency-path"), "npm lock path") ==
        "apps/portal/RvtPortal.Client/package-lock.json",
    "Node setup must cache the Portal npm lock file"
  )

  canonical = {
    "Install Portal client dependencies" => "npm ci",
    "Restore monorepo" =>
      "dotnet restore Rvt.Mono.slnx --locked-mode --disable-parallel",
    "Verify standards model and module policy" =>
      "node --test tests/engineering-standards-model.test.mjs tests/verify-engineering-standards-policy.test.mjs",
    "Verify engineering configuration" =>
      "tests/verify-engineering-configuration.test.sh",
    "Verify changed-range engineering standards" =>
      "scripts/verify-engineering-standards.sh --base auto --head HEAD"
  }

  canonical.each do |name, command|
    step = named_step.call(name)
    assert(
      scalar(step.fetch("run"), "#{name} command") == command,
      "#{name} must use its canonical command"
    )
  end

  install = named_step.call("Install Portal client dependencies")
  assert(
    scalar(install.fetch("working-directory"), "Portal install directory") ==
      "apps/portal/RvtPortal.Client",
    "npm ci must run in the Portal client directory"
  )

  ordered_names = [
    "Check out repository",
    "Set up .NET 10",
    "Set up Node.js 24",
    "Install Portal client dependencies",
    "Restore monorepo",
    "Verify standards model and module policy",
    "Verify engineering configuration",
    "Verify changed-range engineering standards"
  ]
  indexes = ordered_names.map { |name| steps.index(named_step.call(name)) }
  assert(
    indexes == indexes.sort && indexes.uniq.length == indexes.length,
    "engineering standards gates are out of order"
  )

  forbidden = [
    /pull_request_target/,
    /\bself-hosted\b/i,
    /\bsecrets\./i,
    /\b(?:psql|pg_isready|docker)\b/i,
    /database\s+update/i,
    /RVT_(?:DEPLOY|EF|TEST_POSTGRES)_CONNECTION/
  ]
  forbidden.each do |pattern|
    assert(
      !source.match?(pattern),
      "automatic workflow contains forbidden privileged or database behavior: #{pattern.inspect}"
    )
  end
end

workflow_path = ARGV.fetch(0)
source = File.read(workflow_path, encoding: "utf-8")
verify_workflow(source)

mutations = {
  "manual trigger" => ["on: pull_request", "on: workflow_dispatch"],
  "write permission" => ["  contents: read", "  contents: write"],
  "shallow checkout" => ["          fetch-depth: 0", "          fetch-depth: 1"],
  "unpinned checkout" => [
    "actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5",
    "actions/checkout@v4"
  ],
  "self-hosted runner" => ["    runs-on: ubuntu-latest", "    runs-on: self-hosted"],
  "job permission override" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    permissions:\n      contents: write\n"
  ],
  "unlocked restore" => [" --locked-mode --disable-parallel", " --disable-parallel"],
  "nonblocking setup" => [
    "      - name: Set up Node.js 24\n",
    "      - name: Set up Node.js 24\n        continue-on-error: true\n"
  ],
  "nonblocking changed-range gate" => [
    "      - name: Verify changed-range engineering standards\n",
    "      - name: Verify changed-range engineering standards\n        continue-on-error: true\n"
  ],
  "conditional model gate" => [
    "      - name: Verify standards model and module policy\n",
    "      - name: Verify standards model and module policy\n        if: ${{ false }}\n"
  ],
  "removed configuration gate" => [
    "      - name: Verify engineering configuration\n" \
      "        run: tests/verify-engineering-configuration.test.sh\n\n",
    ""
  ],
  "changed-range bypass" => [
    "scripts/verify-engineering-standards.sh --base auto --head HEAD",
    "scripts/verify-engineering-standards.sh --all"
  ],
  "secret exposure" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    env:\n      TOKEN: ${{ secrets.SONAR_TOKEN }}\n"
  ]
}

model_block =
  "      - name: Verify standards model and module policy\n" \
  "        run: node --test tests/engineering-standards-model.test.mjs tests/verify-engineering-standards-policy.test.mjs\n"
configuration_block =
  "      - name: Verify engineering configuration\n" \
  "        run: tests/verify-engineering-configuration.test.sh\n"
mutations["reordered gates"] = [
  "#{model_block}\n#{configuration_block}",
  "#{configuration_block}\n#{model_block}"
]

mutations.each do |label, (needle, replacement)|
  mutated = source.sub(needle, replacement)
  raise "#{label} mutation did not change workflow" if mutated == source

  begin
    verify_workflow(mutated)
  rescue VerificationFailure, KeyError, Psych::SyntaxError
    next
  end
  raise "#{label} mutation was accepted"
end

puts "verify-engineering-standards-workflow: PASS"
RUBY
