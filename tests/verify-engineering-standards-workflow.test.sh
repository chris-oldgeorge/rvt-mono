#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow_path="${repo_root}/.github/workflows/engineering-standards.yml"
action_path="${repo_root}/.github/actions/setup-monorepo/action.yml"

if [[ ! -f "${workflow_path}" ]]; then
  printf 'FAIL: missing automatic engineering standards workflow.\n' >&2
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

def assert_exact_keys(value, expected, context)
  actual = value.keys.sort
  required = expected.sort
  assert(
    actual == required,
    "#{context} keys must be exactly #{required.join(', ')}; got #{actual.join(', ')}"
  )
end

PINNED_ACTIONS = {
  "actions/checkout" => "d23441a48e516b6c34aea4fa41551a30e30af803",
  "actions/setup-dotnet" => "26b0ec14cb23fa6904739307f278c14f94c95bf1",
  "actions/setup-node" => "249970729cb0ef3589644e2896645e5dc5ba9c38"
}.freeze

SETUP_ACTION_REFERENCE = "./.github/actions/setup-monorepo".freeze

CHANGE_GATE = "needs.changes.outputs.code == 'true'".freeze

def assert_pinned_action(reference, context)
  assert(
    reference.match?(%r{\Aactions/[A-Za-z0-9_.-]+@[0-9a-f]{40}\z}),
    "#{context} must be pinned to a commit SHA: #{reference}"
  )
  action_name, commit = reference.split("@")
  assert(
    PINNED_ACTIONS.fetch(action_name, commit) == commit,
    "#{action_name} must stay pinned to #{PINNED_ACTIONS[action_name]}"
  )
end

def verify_trigger_and_root(root, context)
  assert_exact_keys(
    root,
    %w[concurrency jobs name on permissions],
    "#{context} root"
  )

  on = mapping(root.fetch("on"), "#{context} triggers")
  assert(
    on.keys.sort == %w[pull_request push],
    "#{context} must run for pull requests and pushes to main"
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
end

def verify_changes_job(jobs)
  job = mapping(jobs.fetch("changes"), "changes job")
  assert_exact_keys(
    job,
    %w[name outputs runs-on steps timeout-minutes],
    "changes job"
  )
  assert(
    scalar(job.fetch("runs-on"), "changes runner") == "ubuntu-latest",
    "change detection must run on a GitHub-hosted runner"
  )
  outputs = mapping(job.fetch("outputs"), "changes outputs")
  assert(outputs.keys == ["code"], "changes job must expose exactly the code output")
  assert(
    scalar(outputs.fetch("code"), "code output") == "${{ steps.detect.outputs.code }}",
    "code output must come from the detect step"
  )

  steps = sequence(job.fetch("steps"), "changes steps").map { |node| mapping(node, "changes step") }
  assert(steps.length == 2, "changes job must have exactly checkout and detection steps")

  checkout = steps.fetch(0)
  assert_exact_keys(checkout, %w[name uses with], "changes checkout step")
  assert_pinned_action(scalar(checkout.fetch("uses"), "changes checkout"), "changes checkout")
  checkout_with = mapping(checkout.fetch("with"), "changes checkout inputs")
  assert(checkout_with.keys == ["fetch-depth"], "changes checkout must only set fetch-depth")
  assert(
    scalar(checkout_with.fetch("fetch-depth"), "changes fetch-depth") == "0",
    "change detection requires full history"
  )

  detect = steps.fetch(1)
  assert_exact_keys(detect, %w[env id name run], "detect step")
  assert(scalar(detect.fetch("id"), "detect id") == "detect", "detect step must keep the detect id")
  assert(
    scalar(detect.fetch("run"), "detect command") ==
      'scripts/detect-code-changes.sh --base "${BASE_SHA}" --head "${GITHUB_SHA}"',
    "detect step must run the canonical change-detection script"
  )
  env = mapping(detect.fetch("env"), "detect environment")
  assert(env.keys == ["BASE_SHA"], "detect step must receive exactly BASE_SHA")
  assert(
    scalar(env.fetch("BASE_SHA"), "BASE_SHA") ==
      "${{ github.event.pull_request.base.sha || github.event.before }}",
    "BASE_SHA must cover both pull-request and push events"
  )
end

def verify_workflow(source)
  root = mapping(Psych.parse(source).root, "workflow root")
  verify_trigger_and_root(root, "engineering standards workflow")

  concurrency = mapping(root.fetch("concurrency"), "concurrency")
  assert(
    scalar(concurrency.fetch("group"), "concurrency group") ==
      "engineering-standards-${{ github.event.pull_request.number || github.ref }}",
    "unexpected workflow concurrency group"
  )
  assert(
    scalar(concurrency.fetch("cancel-in-progress"), "cancel-in-progress") == "true",
    "superseded runs must be cancelled"
  )

  jobs = mapping(root.fetch("jobs"), "jobs")
  assert(
    jobs.keys.sort == %w[changes engineering-standards],
    "workflow must define exactly the changes filter and the blocking engineering-standards job"
  )

  verify_changes_job(jobs)

  job = mapping(jobs.fetch("engineering-standards"), "engineering-standards job")
  assert_exact_keys(
    job,
    %w[if name needs runs-on steps timeout-minutes],
    "engineering-standards job"
  )
  assert(
    scalar(job.fetch("needs"), "needs") == "changes",
    "the standards job must wait for change detection"
  )
  assert(
    scalar(job.fetch("if"), "job condition") == CHANGE_GATE,
    "the standards job may only skip on documentation-only changes"
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
  assert(steps.length == 7, "workflow must define exactly the seven required setup and gate steps")
  steps.each do |step|
    name = step.key?("name") ? scalar(step.fetch("name"), "step name") : "unnamed step"
    if step.key?("uses")
      reference = scalar(step.fetch("uses"), "action reference")
      if reference == SETUP_ACTION_REFERENCE
        assert_exact_keys(step, %w[name uses], name)
      else
        assert_exact_keys(step, %w[name uses with], name)
        assert_pinned_action(reference, name)
      end
    elsif step.key?("run")
      assert_exact_keys(step, %w[name run], name)
    else
      assert(false, "#{name} must be an action or run step")
    end
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

  toolchain = named_step.call("Set up monorepo toolchain")
  assert(
    scalar(toolchain.fetch("uses"), "toolchain reference") == SETUP_ACTION_REFERENCE,
    "the setup preamble must come from the shared composite action"
  )

  canonical = {
    "Verify standards model and module policy" =>
      "node --test tests/engineering-standards-model.test.mjs tests/verify-engineering-standards-policy.test.mjs",
    "Verify engineering configuration" =>
      "tests/verify-engineering-configuration.test.sh",
    "Verify shell conditional safety" =>
      "tests/verify-shell-conditionals.test.sh\nscripts/verify-shell-conditionals.sh .\n",
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

  ordered_names = [
    "Check out repository",
    "Set up monorepo toolchain",
    "Verify standards model and module policy",
    "Verify engineering configuration",
    "Verify shell conditional safety",
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

def verify_action(source)
  root = mapping(Psych.parse(source).root, "action root")
  assert_exact_keys(root, %w[description name runs], "composite action root")

  runs = mapping(root.fetch("runs"), "action runs")
  assert_exact_keys(runs, %w[steps using], "action runs")
  assert(
    scalar(runs.fetch("using"), "action using") == "composite",
    "setup-monorepo must stay a composite action"
  )

  steps = sequence(runs.fetch("steps"), "action steps").map { |node| mapping(node, "action step") }
  assert(steps.length == 4, "the setup preamble must keep exactly its four steps")
  steps.each do |step|
    name = step.key?("name") ? scalar(step.fetch("name"), "action step name") : "unnamed step"
    assert(!step.key?("if"), "#{name} must not be conditional")
    assert(!step.key?("continue-on-error"), "#{name} must block when it fails")
    assert_pinned_action(scalar(step.fetch("uses"), "action reference"), name) if step.key?("uses")
  end

  named_step = lambda do |name|
    matches = steps.select do |step|
      step.key?("name") && scalar(step.fetch("name"), "action step name") == name
    end
    assert(matches.length == 1, "#{name} action step must occur exactly once")
    matches.fetch(0)
  end

  dotnet_with = mapping(named_step.call("Set up .NET 10").fetch("with"), ".NET setup inputs")
  assert(
    scalar(dotnet_with.fetch("dotnet-version"), ".NET version") == "10.0.x",
    "setup preamble must use .NET 10"
  )

  node_with = mapping(named_step.call("Set up Node.js 24").fetch("with"), "Node setup inputs")
  assert(
    scalar(node_with.fetch("node-version"), "Node version") == "24.x",
    "setup preamble must use Node.js 24"
  )
  assert(
    scalar(node_with.fetch("cache"), "Node cache") == "npm" &&
      scalar(node_with.fetch("cache-dependency-path"), "npm lock path") ==
        "apps/portal/RvtPortal.Client/package-lock.json",
    "Node setup must cache the Portal npm lock file"
  )

  install = named_step.call("Install Portal client dependencies")
  assert(
    scalar(install.fetch("run"), "npm install command") == "npm ci --ignore-scripts",
    "the Portal client must install with the committed lockfile"
  )
  assert(
    scalar(install.fetch("working-directory"), "Portal install directory") ==
      "apps/portal/RvtPortal.Client",
    "npm ci must run in the Portal client directory"
  )

  restore = named_step.call("Restore monorepo")
  assert(
    scalar(restore.fetch("run"), "restore command") ==
      "dotnet restore Rvt.Mono.slnx --locked-mode --disable-parallel",
    "the setup preamble must restore with locked dependencies"
  )

  ordered_names = [
    "Set up .NET 10",
    "Set up Node.js 24",
    "Install Portal client dependencies",
    "Restore monorepo"
  ]
  indexes = ordered_names.map { |name| steps.index(named_step.call(name)) }
  assert(
    indexes == indexes.sort && indexes.uniq.length == indexes.length,
    "setup preamble steps are out of order"
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
  "root defaults shell" => [
    "permissions:\n  contents: read\n\n",
    "permissions:\n  contents: read\n\ndefaults:\n  run:\n    shell: bash\n\n"
  ],
  "root environment" => [
    "permissions:\n  contents: read\n\n",
    "permissions:\n  contents: read\n\nenv:\n  GITHUB_ACTIONS: false\n\n"
  ],
  "write permission" => ["  contents: read", "  contents: write"],
  "no cancellation" => ["  cancel-in-progress: true", "  cancel-in-progress: false"],
  "shallow checkout" => ["          fetch-depth: 0", "          fetch-depth: 1"],
  "unpinned checkout" => [
    "actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803",
    "actions/checkout@v6"
  ],
  "self-hosted runner" => ["    runs-on: ubuntu-latest", "    runs-on: self-hosted"],
  "bypassed change detection" => [
    'run: scripts/detect-code-changes.sh --base "${BASE_SHA}" --head "${GITHUB_SHA}"',
    'run: printf %s\\\\n code=false >>"${GITHUB_OUTPUT}"'
  ],
  "always-skipped standards job" => [
    "    if: needs.changes.outputs.code == 'true'\n",
    "    if: ${{ false }}\n"
  ],
  "detached standards job" => ["    needs: changes\n", ""],
  "job permission override" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    permissions:\n      contents: write\n"
  ],
  "job defaults shell" => [
    "    timeout-minutes: 30\n",
    "    timeout-minutes: 30\n    defaults:\n      run:\n        shell: bash\n"
  ],
  "unpinned toolchain composite" => [
    "        uses: ./.github/actions/setup-monorepo\n",
    "        uses: someone/setup-monorepo@v1\n"
  ],
  "removed toolchain setup" => [
    "      - name: Set up monorepo toolchain\n        uses: ./.github/actions/setup-monorepo\n\n",
    ""
  ],
  "nonblocking toolchain setup" => [
    "      - name: Set up monorepo toolchain\n",
    "      - name: Set up monorepo toolchain\n        continue-on-error: true\n"
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
  "removed shell conditional gate" => [
    "      - name: Verify shell conditional safety\n" \
      "        run: |\n" \
      "          tests/verify-shell-conditionals.test.sh\n" \
      "          scripts/verify-shell-conditionals.sh .\n\n",
    ""
  ],
  "nonblocking shell conditional gate" => [
    "      - name: Verify shell conditional safety\n",
    "      - name: Verify shell conditional safety\n        continue-on-error: true\n"
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
workflow_mutations["reordered gates"] = [
  "#{model_block}\n#{configuration_block}",
  "#{configuration_block}\n#{model_block}"
]

workflow_contract_block =
  "      - name: Verify automatic workflow contract\n" \
  "        run: tests/verify-engineering-standards-workflow.test.sh\n"
changed_range_block =
  "      - name: Verify changed-range engineering standards\n" \
  "        run: scripts/verify-engineering-standards.sh --base auto --head HEAD\n"
workflow_mutations["workflow contract after changed-range gate"] = [
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
  workflow_mutations["#{override} step override"] = [
    "      - name: Verify changed-range engineering standards\n",
    "      - name: Verify changed-range engineering standards\n" \
      "        env:\n" \
      "          #{override}: unsafe\n"
  ]
end

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
  ],
  "nonblocking npm install" => [
    "    - name: Install Portal client dependencies\n",
    "    - name: Install Portal client dependencies\n      continue-on-error: true\n"
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

puts "verify-engineering-standards-workflow: PASS"
RUBY
