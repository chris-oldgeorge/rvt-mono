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

def assert_exact_keys(value, expected, context)
  actual = value.keys.sort
  required = expected.sort
  assert(
    actual == required,
    "#{context} keys must be exactly #{required.join(', ')}; got #{actual.join(', ')}"
  )
end

def verify_workflow(source)
  root = mapping(Psych.parse(source).root, "workflow root")
  assert_exact_keys(
    root,
    %w[concurrency jobs name on permissions],
    "workflow root"
  )

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
  assert_exact_keys(
    job,
    %w[name runs-on steps timeout-minutes],
    "engineering-standards job"
  )
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
  steps = sequence(job.fetch("steps"), "steps").map { |node| mapping(node, "step") }
  assert(steps.length == 9, "workflow must define exactly the nine required setup and gate steps")
  steps.each do |step|
    name = step.key?("name") ? scalar(step.fetch("name"), "step name") : "unnamed step"
    if step.key?("uses")
      assert_exact_keys(step, %w[name uses with], name)
    elsif step.key?("run")
      expected =
        if name == "Install Portal client dependencies"
          %w[name run working-directory]
        else
          %w[name run]
        end
      assert_exact_keys(step, expected, name)
    else
      assert(false, "#{name} must be an action or run step")
    end
  end

  uses_steps = steps.select { |step| step.key?("uses") }
  assert(uses_steps.length == 3, "workflow must use checkout, setup-dotnet, and setup-node")

  expected_actions = {
    "actions/checkout" => "d23441a48e516b6c34aea4fa41551a30e30af803",
    "actions/setup-dotnet" => "26b0ec14cb23fa6904739307f278c14f94c95bf1",
    "actions/setup-node" => "249970729cb0ef3589644e2896645e5dc5ba9c38"
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
    "Verify automatic workflow contract" =>
      "tests/verify-engineering-standards-workflow.test.sh",
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
    "Verify automatic workflow contract",
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
  "root defaults shell" => [
    "on: pull_request\n\n",
    "on: pull_request\n\ndefaults:\n  run:\n    shell: bash\n\n"
  ],
  "root environment" => [
    "permissions:\n  contents: read\n\n",
    "permissions:\n  contents: read\n\nenv:\n  GITHUB_ACTIONS: false\n\n"
  ],
  "write permission" => ["  contents: read", "  contents: write"],
  "shallow checkout" => ["          fetch-depth: 0", "          fetch-depth: 1"],
  "unpinned checkout" => [
    "actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803",
    "actions/checkout@v6"
  ],
  "self-hosted runner" => ["    runs-on: ubuntu-latest", "    runs-on: self-hosted"],
  "job permission override" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    permissions:\n      contents: write\n"
  ],
  "job defaults shell" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    defaults:\n      run:\n        shell: bash\n"
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
  "changed-range shell override" => [
    "      - name: Verify changed-range engineering standards\n",
    "      - name: Verify changed-range engineering standards\n" \
      "        shell: env GITHUB_ACTIONS=false RVT_STANDARDS_DOTNET_COMMAND=unsafe bash {0}\n"
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
  "removed workflow contract gate" => [
    "      - name: Verify automatic workflow contract\n" \
      "        run: tests/verify-engineering-standards-workflow.test.sh\n\n",
    ""
  ],
  "nonblocking workflow contract gate" => [
    "      - name: Verify automatic workflow contract\n",
    "      - name: Verify automatic workflow contract\n        continue-on-error: true\n"
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

workflow_contract_block =
  "      - name: Verify automatic workflow contract\n" \
  "        run: tests/verify-engineering-standards-workflow.test.sh\n"
changed_range_block =
  "      - name: Verify changed-range engineering standards\n" \
  "        run: scripts/verify-engineering-standards.sh --base auto --head HEAD\n"
mutations["workflow contract after changed-range gate"] = [
  "#{workflow_contract_block}\n#{changed_range_block}",
  "#{changed_range_block}\n#{workflow_contract_block}"
]

[
  "RVT_STANDARDS_DOTNET_COMMAND",
  "RVT_STANDARDS_PRETTIER_COMMAND",
  "RVT_STANDARDS_ESLINT_COMMAND",
  "RVT_STANDARDS_BASELINE_PATH",
  "RVT_STANDARDS_EXCEPTIONS_PATH"
].each do |override|
  mutations["#{override} step override"] = [
    "      - name: Verify changed-range engineering standards\n",
    "      - name: Verify changed-range engineering standards\n" \
      "        env:\n" \
      "          #{override}: unsafe\n"
  ]
end

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

puts "verify-engineering-standards-workflow: PASS"
RUBY
