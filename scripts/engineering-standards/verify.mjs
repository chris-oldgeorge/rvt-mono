import { spawn, spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import {
  accessSync,
  constants,
  existsSync,
  lstatSync,
  linkSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  renameSync,
  rmSync,
  statSync,
  symlinkSync,
  writeFileSync
} from 'node:fs';
import os from 'node:os';
import path from 'node:path';

import {
  compareRatchet,
  diagnosticKey,
  normalizeRepositoryPath,
  parseDotnetFormatReport,
  parseEslintReport,
  validateBaseline,
  validateExceptions
} from './model.mjs';

const ignoredPrefixes = [
  '.git/', '.worktrees/', '.codegraph/', 'artifacts/',
  'node_modules/', 'bin/', 'obj/', 'dist/', 'coverage/',
  'TestResults/', 'playwright-report/', 'test-results/'
];
const portalPrefix = 'apps/portal/RvtPortal.Client/';
const dotnetTools = {
  whitespace: 'dotnet-format-whitespace',
  style: 'dotnet-format-style',
  analyzers: 'dotnet-format-analyzers'
};
const portalPrettierExtensions = new Set([
  '.css', '.html', '.js', '.jsx', '.json', '.md', '.mdx',
  '.scss', '.ts', '.tsx', '.mts', '.cts', '.mjs', '.cjs',
  '.graphql', '.gql', '.yaml', '.yml'
]);
const portalSourceExtensions = new Set([...portalPrettierExtensions, '.svg']);
const portalBinaryExtensions = new Set([
  '.eot', '.gif', '.ico', '.jpeg', '.jpg', '.pdf', '.png',
  '.ttf', '.webp', '.woff', '.woff2'
]);
const dependencyInputPathspecs = [
  ':(glob,icase)**/*.csproj',
  ':(glob,icase)**/*.fsproj',
  ':(glob,icase)**/*.vbproj',
  ':(glob,icase)**/*.props',
  ':(glob,icase)**/*.targets',
  ':(glob,icase)**/*.sln',
  ':(glob,icase)**/*.slnx',
  ':(glob,icase)**/global.json',
  ':(glob,icase)**/nuget.config',
  ':(glob,icase)**/packages.lock.json',
  `${portalPrefix}.npmrc`,
  `${portalPrefix}npm-shrinkwrap.json`,
  `${portalPrefix}package-lock.json`,
  `${portalPrefix}package.json`,
  `${portalPrefix}pnpm-lock.yaml`,
  `${portalPrefix}pnpm-lock.yml`,
  `${portalPrefix}yarn.lock`
];

class PolicyError extends Error {}
class InvocationError extends Error {}

function usage() {
  return `Usage:
  scripts/verify-engineering-standards.sh --working-tree
  scripts/verify-engineering-standards.sh --base REV --head REV
  scripts/verify-engineering-standards.sh --base auto --head HEAD
  scripts/verify-engineering-standards.sh --all [--initialize-baseline|--update-baseline]

Test-only command overrides are JSON arrays of non-empty string tokens:
  RVT_STANDARDS_DOTNET_COMMAND='["/path/to/fake-dotnet","fixed argument"]'
The ESLint and Prettier overrides use the same representation. Shell text is
never evaluated.`;
}

function parseArguments(argv) {
  const parsed = {
    mode: undefined,
    base: undefined,
    head: undefined,
    initialize: false,
    update: false
  };

  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index];
    if (item === '--working-tree') {
      setMode(parsed, 'working-tree');
    } else if (item === '--all') {
      setMode(parsed, 'all');
    } else if (item === '--base') {
      setMode(parsed, 'range');
      parsed.base = requiredArgument(argv, ++index, '--base');
    } else if (item === '--head') {
      parsed.head = requiredArgument(argv, ++index, '--head');
    } else if (item === '--initialize-baseline') {
      parsed.initialize = true;
    } else if (item === '--update-baseline') {
      parsed.update = true;
    } else if (item === '--help' || item === '-h') {
      process.stdout.write(`${usage()}\n`);
      return { help: true };
    } else {
      throw new InvocationError(`Unknown argument: ${item}\n${usage()}`);
    }
  }

  if (parsed.mode === undefined) {
    throw new InvocationError(`Exactly one verification mode is required\n${usage()}`);
  }
  if (parsed.mode === 'range' && (parsed.base === undefined || parsed.head === undefined)) {
    throw new InvocationError('--base and --head must be provided together');
  }
  if (parsed.mode !== 'range' && parsed.head !== undefined) {
    throw new InvocationError('--head is valid only with --base');
  }
  if (parsed.base === 'auto' && parsed.head !== 'HEAD') {
    throw new InvocationError('--base auto requires --head HEAD');
  }
  if (parsed.initialize && parsed.update) {
    throw new InvocationError('Baseline initialization and update are mutually exclusive');
  }
  if ((parsed.initialize || parsed.update) && parsed.mode !== 'all') {
    throw new InvocationError('Baseline initialization and update require --all');
  }

  return parsed;
}

function setMode(parsed, mode) {
  if (parsed.mode !== undefined) {
    throw new InvocationError('Exactly one verification mode is allowed');
  }
  parsed.mode = mode;
}

function requiredArgument(argv, index, option) {
  const value = argv[index];
  if (value === undefined || value.startsWith('--')) {
    throw new InvocationError(`${option} requires a value`);
  }
  return value;
}

function runProcess(command, args, cwd, label) {
  const result = spawnSync(command[0], [...command.slice(1), ...args], {
    cwd,
    encoding: 'utf8',
    shell: false,
    maxBuffer: 32 * 1024 * 1024
  });

  if (result.error) {
    throw new InvocationError(`${label} failed to start: ${result.error.message}`);
  }
  if (result.status === null) {
    throw new InvocationError(`${label} terminated by ${result.signal ?? 'an unknown signal'}`);
  }

  return {
    status: result.status,
    stdout: result.stdout ?? '',
    stderr: result.stderr ?? ''
  };
}

function gitResult(repoRoot, args) {
  return runProcess(['git'], ['-c', 'core.quotepath=true', ...args], repoRoot, 'Git');
}

function gitText(repoRoot, args) {
  const result = gitResult(repoRoot, args);
  if (result.status !== 0) {
    throw new InvocationError(`Git ${args[0]} failed: ${result.stderr.trim() || result.stdout.trim()}`);
  }
  return result.stdout;
}

function gitLines(repoRoot, args) {
  const text = gitText(repoRoot, args);
  if (text.includes('\0')) {
    throw new InvocationError('Git returned a malformed NUL-containing path list');
  }

  return text
    .split('\n')
    .filter((item) => item !== '')
    .map((item) => validateGitPath(repoRoot, item));
}

function validateGitPath(repoRoot, candidate) {
  if (
    candidate.startsWith('"') ||
    candidate.endsWith('"') ||
    candidate.includes('\r') ||
    candidate.includes('\0')
  ) {
    throw new InvocationError(`Git returned a quoted or malformed path: ${candidate}`);
  }

  let normalized;
  try {
    normalized = normalizeRepositoryPath(repoRoot, candidate);
  } catch (error) {
    throw new InvocationError(error.message);
  }
  if (candidate.replaceAll('\\', '/') !== normalized) {
    throw new InvocationError(`Git returned a non-canonical repository path: ${candidate}`);
  }
  return normalized;
}

function resolveRevision(repoRoot, revision) {
  const result = gitResult(repoRoot, ['rev-parse', '--verify', `${revision}^{commit}`]);
  if (result.status !== 0) {
    throw new InvocationError(`Invalid Git revision: ${revision}`);
  }
  const resolved = result.stdout.trim();
  if (!/^[0-9a-f]{40,64}$/i.test(resolved)) {
    throw new InvocationError(`Git returned a malformed revision for ${revision}`);
  }
  return resolved;
}

function resolveRange(repoRoot, base, head) {
  const resolvedHead = resolveRevision(repoRoot, head);
  if (base !== 'auto') {
    return { base: resolveRevision(repoRoot, base), head: resolvedHead };
  }

  const originMain = resolveRevision(repoRoot, 'origin/main');
  if (resolvedHead === originMain) {
    return { base: resolveRevision(repoRoot, 'HEAD^'), head: resolvedHead };
  }

  const result = gitResult(repoRoot, ['merge-base', originMain, resolvedHead]);
  if (result.status !== 0) {
    throw new InvocationError('Git could not find a merge-base with origin/main');
  }
  const mergeBase = result.stdout.trim();
  if (!/^[0-9a-f]{40,64}$/i.test(mergeBase)) {
    throw new InvocationError('Git returned a malformed merge-base');
  }
  return { base: mergeBase, head: resolvedHead };
}

function resolveScope(repoRoot, options) {
  if (options.mode === 'all') {
    const paths = gitLines(repoRoot, ['ls-files']);
    return {
      inventory: true,
      paths,
      newPaths: new Set(),
      changedRanges: new Map(),
      executionRoot: repoRoot,
      cleanup() {}
    };
  }

  if (options.mode === 'working-tree') {
    const tracked = gitLines(repoRoot, [
      'diff', '--name-only', '--diff-filter=ACMR', 'HEAD'
    ]);
    const untracked = gitLines(repoRoot, [
      'ls-files', '--others', '--exclude-standard'
    ]);
    const patch = tracked.length === 0
      ? ''
      : gitText(repoRoot, [
        'diff', '--unified=0', '--no-ext-diff', 'HEAD', '--', ...tracked
      ]);
    const added = gitLines(repoRoot, [
      'diff', '--name-only', '--diff-filter=A', 'HEAD'
    ]);
    return {
      ...finalizeChangedScope(repoRoot, {
      paths: [...new Set([...tracked, ...untracked])],
      patch,
      newPaths: new Set([...added, ...untracked]),
      untracked: new Set(untracked)
      }),
      executionRoot: repoRoot,
      cleanup() {}
    };
  }

  const range = resolveRange(repoRoot, options.base, options.head);
  const tracked = gitLines(repoRoot, [
    'diff', '--name-only', '--diff-filter=ACMR', range.base, range.head
  ]);
  const patch = tracked.length === 0
    ? ''
    : gitText(repoRoot, [
      'diff', '--unified=0', '--no-ext-diff',
      range.base, range.head, '--', ...tracked
    ]);
  const added = gitLines(repoRoot, [
    'diff', '--name-only', '--diff-filter=A', range.base, range.head
  ]);
  const materialized = materializeRevision(repoRoot, range.head);
  try {
    provisionRangePrerequisites(repoRoot, materialized.root, range.head, tracked);
    return {
      ...finalizeChangedScope(materialized.root, {
        paths: tracked,
        patch,
        newPaths: new Set(added),
        untracked: new Set()
      }),
      executionRoot: materialized.root,
      cleanup: materialized.cleanup
    };
  } catch (error) {
    materialized.cleanup();
    throw error;
  }
}

function materializeRevision(repoRoot, revision) {
  const temporaryRoot = mkdtempSync(path.join(os.tmpdir(), 'rvt-standards-head-'));
  const checkoutRoot = path.join(temporaryRoot, 'tree');
  const result = gitResult(repoRoot, [
    'worktree', 'add', '--detach', checkoutRoot, revision
  ]);
  if (result.status !== 0) {
    rmSync(temporaryRoot, { recursive: true, force: true });
    throw new InvocationError(`Git could not materialize requested head: ${result.stderr.trim()}`);
  }
  let cleaned = false;
  return {
    root: checkoutRoot,
    cleanup() {
      if (cleaned) return;
      cleaned = true;
      const removal = gitResult(repoRoot, ['worktree', 'remove', '--force', checkoutRoot]);
      rmSync(temporaryRoot, { recursive: true, force: true });
      if (removal.status !== 0) {
        throw new InvocationError(
          `Git could not remove requested-head worktree: ${removal.stderr.trim()}`
        );
      }
    }
  };
}

function provisionRangePrerequisites(repoRoot, executionRoot, requestedHead, changedPaths) {
  const applicable = changedPaths
    .filter((item) => !isIgnoredPath(item))
    .filter(isSourcePath);
  const needsDotnet =
    process.env.RVT_STANDARDS_DOTNET_COMMAND === undefined &&
    applicable.some((item) => item.endsWith('.cs'));
  const portalPaths = applicable.filter(isPortalPrettierPath);
  const needsPrettier =
    process.env.RVT_STANDARDS_PRETTIER_COMMAND === undefined &&
    portalPaths.length > 0;
  const needsEslint =
    process.env.RVT_STANDARDS_ESLINT_COMMAND === undefined &&
    portalPaths.some((item) =>
      ['.ts', '.tsx', '.mts', '.cts'].includes(path.posix.extname(item).toLowerCase())
    );
  if (!needsDotnet && !needsPrettier && !needsEslint) return;

  const callerHead = resolveRevision(repoRoot, 'HEAD');
  if (needsDotnet) {
    assertCallerDependencyInputsClean(repoRoot, isDotnetDependencyInput, '.NET');
    assertCompatibleFingerprint(
      repoRoot, callerHead, requestedHead, isDotnetDependencyInput, '.NET'
    );
  }
  if (needsPrettier || needsEslint) {
    assertCallerDependencyInputsClean(repoRoot, isPortalDependencyInput, 'Portal');
    assertCompatibleFingerprint(
      repoRoot, callerHead, requestedHead, isPortalDependencyInput, 'Portal'
    );
  }

  if (needsDotnet) {
    const projects = gitLines(executionRoot, ['ls-files']).filter(
      (item) => /\.(?:cs|fs|vb)proj$/i.test(item)
    );
    if (projects.length === 0) {
      throw new InvocationError(
        'Committed-range prerequisite is missing: no tracked project file for dotnet --no-restore'
      );
    }
    for (const project of projects) {
      provisionAssetDirectory(
        repoRoot,
        executionRoot,
        path.posix.join(path.posix.dirname(project), 'obj'),
        'dotnet --no-restore'
      );
    }
  }

  if (needsPrettier || needsEslint) {
    const nodeModules = `${portalPrefix}node_modules`;
    provisionAssetDirectory(repoRoot, executionRoot, nodeModules, 'Portal node_modules');
    for (const executable of [
      ...(needsPrettier ? ['prettier'] : []),
      ...(needsEslint ? ['eslint'] : [])
    ]) {
      const executablePath = path.join(executionRoot, nodeModules, '.bin', executable);
      if (!existsSync(executablePath)) {
        throw new InvocationError(
          `Committed-range prerequisite is missing: Portal node_modules/.bin/${executable}`
        );
      }
    }
  }
}

function isDotnetDependencyInput(candidate) {
  const name = path.posix.basename(candidate).toLowerCase();
  return /\.(?:cs|fs|vb)proj$/i.test(candidate) ||
    /\.(?:props|targets|sln|slnx)$/i.test(candidate) ||
    name === 'global.json' || name === 'nuget.config' ||
    name === 'packages.lock.json';
}

function isPortalDependencyInput(candidate) {
  if (path.posix.dirname(candidate) !== portalPrefix.slice(0, -1)) return false;
  const name = path.posix.basename(candidate).toLowerCase();
  return [
    '.npmrc', 'npm-shrinkwrap.json', 'package-lock.json', 'package.json',
    'pnpm-lock.yaml', 'pnpm-lock.yml', 'yarn.lock'
  ].includes(name);
}

function assertCallerDependencyInputsClean(repoRoot, predicate, label) {
  const staged = gitLines(repoRoot, [
    'diff', '--cached', '--name-only', '--no-renames', 'HEAD'
  ]);
  const working = gitLines(repoRoot, [
    'diff', '--name-only', '--no-renames', 'HEAD'
  ]);
  const untracked = gitLines(repoRoot, [
    'ls-files', '--others', '--', ...dependencyInputPathspecs
  ]);
  const mismatch = [...new Set([...staged, ...working, ...untracked])]
    .filter((candidate) => !isIgnoredPath(candidate) && predicate(candidate))
    .sort(compareCodePoints)[0];
  if (mismatch !== undefined) {
    throw new InvocationError(
      `Committed-range caller dependency input does not match caller HEAD for ${label}: ${mismatch}`
    );
  }
}

function dependencyFingerprint(repoRoot, revision, predicate) {
  const output = gitText(repoRoot, [
    'ls-tree', '-r', '--format=%(objectname)%x09%(path)', revision
  ]);
  const result = new Map();
  for (const line of output.split('\n')) {
    if (line === '') continue;
    const separator = line.indexOf('\t');
    if (separator <= 0) {
      throw new InvocationError('Git returned a malformed dependency fingerprint');
    }
    const objectId = line.slice(0, separator);
    const candidate = validateGitPath(repoRoot, line.slice(separator + 1));
    if (predicate(candidate)) result.set(candidate, objectId);
  }
  return result;
}

function assertCompatibleFingerprint(repoRoot, callerHead, requestedHead, predicate, label) {
  const caller = dependencyFingerprint(repoRoot, callerHead, predicate);
  const requested = dependencyFingerprint(repoRoot, requestedHead, predicate);
  const paths = [...new Set([...caller.keys(), ...requested.keys()])]
    .sort(compareCodePoints);
  const mismatch = paths.find(
    (candidate) => caller.get(candidate) !== requested.get(candidate)
  );
  if (mismatch !== undefined) {
    throw new InvocationError(
      `Committed-range prerequisite inputs are incompatible for ${label}: ${mismatch}`
    );
  }
}

function provisionAssetDirectory(repoRoot, executionRoot, relativePath, label) {
  const source = path.join(repoRoot, relativePath);
  const destination = path.join(executionRoot, relativePath);
  if (!existsSync(source) || !lstatSync(source).isDirectory()) {
    throw new InvocationError(`Committed-range prerequisite is missing: ${label} (${relativePath})`);
  }
  if (!existsSync(destination)) symlinkSync(source, destination, 'dir');
}

function finalizeChangedScope(repoRoot, input) {
  const parsedRanges = parseUnifiedDiffRanges(input.patch, repoRoot);
  const sourcePaths = input.paths.filter((item) => isSourcePath(item) && !isIgnoredPath(item));

  for (const item of sourcePaths) {
    validateSourceFile(repoRoot, item);
    if (
      !input.untracked.has(item) &&
      !input.newPaths.has(item) &&
      (parsedRanges.get(item)?.length ?? 0) === 0
    ) {
      throw new InvocationError(`Changed source path has no new-side hunk: ${item}`);
    }
    if (
      input.newPaths.has(item) &&
      !input.untracked.has(item) &&
      (parsedRanges.get(item)?.length ?? 0) === 0
    ) {
      throw new InvocationError(`Changed source path has no new-side hunk: ${item}`);
    }
  }

  return {
    inventory: false,
    paths: input.paths,
    newPaths: new Set(
      [...input.newPaths].filter((item) => sourcePaths.includes(item))
    ),
    changedRanges: new Map(
      [...parsedRanges].filter(([item]) => sourcePaths.includes(item))
    )
  };
}

function parseUnifiedDiffRanges(patch, repoRoot) {
  const ranges = new Map();
  let currentPath;
  let sawDiff = false;

  for (const line of patch.split('\n')) {
    if (line.startsWith('Binary files ') || line === 'GIT binary patch') {
      if (currentPath !== undefined && isSourcePath(currentPath) && !isIgnoredPath(currentPath)) {
        throw new InvocationError('Binary source patches are not supported');
      }
      continue;
    }
    if (line.startsWith('diff --git ')) {
      sawDiff = true;
      if (line.includes('"')) {
        throw new InvocationError(`Malformed or quoted patch header: ${line}`);
      }
      const splitAt = line.lastIndexOf(' b/');
      if (!line.startsWith('diff --git a/') || splitAt < 'diff --git a/'.length) {
        throw new InvocationError(`Malformed patch header: ${line}`);
      }
      validatePatchPath(repoRoot, line.slice('diff --git a/'.length, splitAt));
      currentPath = validatePatchPath(repoRoot, line.slice(splitAt + 3));
      continue;
    }
    if (line.startsWith('--- ') || line.startsWith('+++ ')) {
      const marker = line.slice(4);
      if (marker === '/dev/null') {
        if (line.startsWith('+++ ')) currentPath = undefined;
        continue;
      }
      const expectedPrefix = line.startsWith('--- ') ? 'a/' : 'b/';
      if (!marker.startsWith(expectedPrefix) || marker.includes('\t') || marker.includes('"')) {
        throw new InvocationError(`Malformed patch path: ${line}`);
      }
      const patchPath = validatePatchPath(repoRoot, marker.slice(2));
      if (line.startsWith('+++ ')) currentPath = patchPath;
      continue;
    }
    if (line.startsWith('@@')) {
      const match = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@(?: .*)?$/.exec(line);
      if (!match || currentPath === undefined) {
        throw new InvocationError(`Malformed unified diff hunk: ${line}`);
      }
      const startLine = Number(match[1]);
      const count = match[2] === undefined ? 1 : Number(match[2]);
      if (!Number.isSafeInteger(startLine) || !Number.isSafeInteger(count)) {
        throw new InvocationError(`Malformed unified diff range: ${line}`);
      }
      if (count > 0) {
        const items = ranges.get(currentPath) ?? [];
        items.push({ startLine, endLine: startLine + count - 1 });
        ranges.set(currentPath, items);
      }
    }
  }

  if (patch !== '' && !sawDiff) {
    throw new InvocationError('Malformed unified diff: missing file header');
  }
  return ranges;
}

function validatePatchPath(repoRoot, candidate) {
  return validateGitPath(repoRoot, candidate);
}

function isIgnoredPath(candidate) {
  return ignoredPrefixes.some(
    (prefix) => candidate.startsWith(prefix) || candidate.includes(`/${prefix}`)
  );
}

function isSourcePath(candidate) {
  if (candidate.endsWith('.cs')) return true;
  const extension = path.posix.extname(candidate).toLowerCase();
  return (
    candidate.startsWith(portalPrefix) &&
    !portalBinaryExtensions.has(extension) &&
    portalSourceExtensions.has(extension)
  );
}

function isPortalPrettierPath(candidate) {
  return (
    candidate.startsWith(portalPrefix) &&
    portalPrettierExtensions.has(path.posix.extname(candidate).toLowerCase())
  );
}

function validateSourceFile(repoRoot, candidate) {
  validateSourceBoundary(repoRoot, candidate);
  const absolute = path.join(repoRoot, candidate);
  const content = readFileSync(absolute);
  if (content.includes(0)) {
    throw new InvocationError(`Binary source input is not supported: ${candidate}`);
  }
}

function validateSourceBoundary(repoRoot, candidate) {
  const absolute = path.join(repoRoot, candidate);
  validateContainedPath(repoRoot, absolute, 'Changed source path');
  let stat;
  try {
    stat = lstatSync(absolute);
  } catch (error) {
    throw new InvocationError(`Changed source path is unreadable: ${candidate}: ${error.message}`);
  }
  if (stat.isSymbolicLink() || !stat.isFile()) {
    throw new InvocationError(`Changed source path must be a regular in-repository file: ${candidate}`);
  }
}

function validateContainedPath(repoRoot, absolute, label, allowMissingLeaf = false) {
  const lexicalRoot = path.resolve(repoRoot);
  const relative = path.relative(lexicalRoot, absolute);
  if (
    relative === '..' ||
    relative.startsWith(`..${path.sep}`) ||
    path.isAbsolute(relative)
  ) {
    throw new InvocationError(`${label} is outside repository root: ${absolute}`);
  }

  const realRoot = realpathSync(lexicalRoot);
  const segments = relative === '' ? [] : relative.split(path.sep);
  let current = lexicalRoot;
  for (let index = 0; index < segments.length; index += 1) {
    current = path.join(current, segments[index]);
    let stat;
    try {
      stat = lstatSync(current);
    } catch (error) {
      if (allowMissingLeaf && index === segments.length - 1 && error.code === 'ENOENT') {
        return;
      }
      throw new InvocationError(`${label} is missing or unreadable: ${current}: ${error.message}`);
    }
    if (stat.isSymbolicLink()) {
      throw new InvocationError(
        `${label} must be a regular in-repository file; path contains a symlink: ${current}`
      );
    }
    const realCurrent = realpathSync(current);
    const realRelative = path.relative(realRoot, realCurrent);
    if (
      realRelative === '..' ||
      realRelative.startsWith(`..${path.sep}`) ||
      path.isAbsolute(realRelative)
    ) {
      throw new InvocationError(`${label} resolves outside repository root: ${current}`);
    }
  }
}

function readJsonDocument(filePath, label) {
  let text;
  try {
    text = readFileSync(filePath, 'utf8');
  } catch (error) {
    throw new InvocationError(`${label} is missing or unreadable: ${filePath}: ${error.message}`);
  }
  try {
    return JSON.parse(text);
  } catch (error) {
    throw new InvocationError(`${label} is malformed JSON: ${filePath}: ${error.message}`);
  }
}

function baselineMap(document) {
  return new Map(document.entries.map((entry) => [diagnosticKey(entry), entry.count]));
}

function loadPolicy(repoRoot, options) {
  const baselinePath = resolvePolicyPath(
    repoRoot,
    process.env.RVT_STANDARDS_BASELINE_PATH ?? 'eng/standards/baseline.json',
    options.initialize
  );
  const exceptionsPath = resolvePolicyPath(
    repoRoot,
    process.env.RVT_STANDARDS_EXCEPTIONS_PATH ?? 'eng/standards/exceptions.json'
  );
  const exceptionDocument = readJsonDocument(exceptionsPath, 'Exceptions document');
  try {
    validateExceptions(exceptionDocument);
  } catch (error) {
    throw new InvocationError(error.message);
  }

  if (options.initialize) {
    if (existsSync(baselinePath)) {
      throw new PolicyError(`Baseline already exists; initialization is refused: ${baselinePath}`);
    }
    return {
      baselinePath,
      baseline: new Map(),
      exceptions: exceptionDocument.exceptions
    };
  }

  const baselineDocument = readJsonDocument(baselinePath, 'Baseline document');
  try {
    validateBaseline(baselineDocument);
  } catch (error) {
    throw new InvocationError(error.message);
  }
  return {
    baselinePath,
    baseline: baselineMap(baselineDocument),
    exceptions: exceptionDocument.exceptions
  };
}

function resolvePolicyPath(repoRoot, candidate, allowMissingLeaf = false) {
  if (typeof candidate !== 'string' || candidate.trim() === '') {
    throw new InvocationError('Policy path override must be a non-empty path');
  }
  const absolute = path.resolve(repoRoot, candidate);
  const relative = path.relative(repoRoot, absolute);
  if (
    relative === '..' ||
    relative.startsWith(`..${path.sep}`) ||
    path.isAbsolute(relative)
  ) {
    throw new InvocationError(`Policy path is outside repository root: ${candidate}`);
  }
  validateContainedPath(repoRoot, absolute, 'Policy path', allowMissingLeaf);
  return absolute;
}

function commandOverride(name, fallback) {
  const raw = process.env[name];
  if (raw === undefined) return fallback;

  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new InvocationError(`${name} must be a JSON array of command tokens`);
  }
  if (
    !Array.isArray(parsed) ||
    parsed.length === 0 ||
    parsed.some(
      (item) => typeof item !== 'string' || item.length === 0 || item.includes('\0')
    )
  ) {
    throw new InvocationError(`${name} must be a JSON array of non-empty string command tokens`);
  }
  return parsed;
}

function collectDiagnostics(repoRoot, scope) {
  for (const item of scope.paths.filter(isSourcePath)) {
    validateSourceBoundary(repoRoot, item);
  }
  const applicable = scope.paths
    .filter((item) => !isIgnoredPath(item))
    .filter(isSourcePath);
  for (const item of applicable) validateSourceFile(repoRoot, item);

  const dotnetPaths = applicable.filter((item) => item.endsWith('.cs')).sort();
  const portalPaths = applicable
    .filter(isPortalPrettierPath)
    .sort();
  const diagnostics = [];
  const immediateViolations = [];

  if (dotnetPaths.length > 0) {
    const command = commandOverride('RVT_STANDARDS_DOTNET_COMMAND', ['dotnet']);
    for (const phase of ['whitespace', 'style', 'analyzers']) {
      diagnostics.push(...runDotnetPhase({
        repoRoot,
        command,
        phase,
        paths: dotnetPaths,
        inventory: scope.inventory,
        immediateViolations
      }));
    }
  }

  if (portalPaths.length > 0) {
    diagnostics.push(...runPortalTools({
      repoRoot,
      paths: portalPaths,
      inventory: scope.inventory,
      immediateViolations
    }));
  }

  return { diagnostics, immediateViolations };
}

function runDotnetPhase({
  repoRoot,
  command,
  phase,
  paths,
  inventory,
  immediateViolations
}) {
  const reportDirectory = mkdtempSync(path.join(os.tmpdir(), 'rvt-standards-dotnet-'));
  const reportPath = path.join(reportDirectory, `${phase}.json`);
  const args = [
    'format', 'Rvt.Mono.slnx', phase,
    '--verify-no-changes', '--no-restore'
  ];
  if (phase === 'style') args.push('--severity', 'info');
  if (phase === 'analyzers') args.push('--severity', 'warn');
  args.push('--include', ...paths, '--report', reportPath);

  try {
    const result = runProcess(command, args, repoRoot, `dotnet format ${phase}`);
    if (result.status !== 0 && !existsSync(reportPath)) {
      throw new InvocationError(
        `dotnet format ${phase} exited ${result.status} without a readable report`
      );
    }
    if (!existsSync(reportPath)) return [];

    let document;
    try {
      document = JSON.parse(readFileSync(reportPath, 'utf8'));
    } catch (error) {
      throw new InvocationError(`dotnet format ${phase} report is unreadable or malformed: ${error.message}`);
    }
    let parsed;
    try {
      parsed = parseDotnetFormatReport(document, repoRoot);
    } catch (error) {
      throw new InvocationError(`dotnet format ${phase} report is invalid: ${error.message}`);
    }
    if (result.status !== 0 && parsed.length === 0) {
      throw new InvocationError(
        `dotnet format ${phase} exited ${result.status} without diagnostics`
      );
    }
    const unexpected = parsed.find((item) => !paths.includes(item.path));
    if (unexpected !== undefined) {
      throw new InvocationError(
        `dotnet format ${phase} reported an unexpected path: ${unexpected.path}`
      );
    }
    if (result.status !== 0 && phase === 'whitespace' && !inventory) {
      immediateViolations.push(
        `CSH-002 changed C# whitespace violation (${paths.join(', ')})`
      );
      return [];
    }
    return parsed.map((item) => ({ ...item, tool: dotnetTools[phase] }));
  } finally {
    rmSync(reportDirectory, { recursive: true, force: true });
  }
}

function runPortalTools({ repoRoot, paths, inventory, immediateViolations }) {
  const clientRoot = path.join(repoRoot, portalPrefix);
  const clientPaths = paths.map((item) => item.slice(portalPrefix.length));
  const typescriptPaths = clientPaths.filter(
    (item) => ['.ts', '.tsx', '.mts', '.cts'].includes(path.posix.extname(item).toLowerCase())
  );
  const diagnostics = [];
  const prettier = commandOverride(
    'RVT_STANDARDS_PRETTIER_COMMAND',
    ['node_modules/.bin/prettier']
  );
  const eslint = commandOverride(
    'RVT_STANDARDS_ESLINT_COMMAND',
    ['node_modules/.bin/eslint']
  );

  if (process.env.RVT_STANDARDS_PRETTIER_COMMAND === undefined) {
    requirePortalExecutable(clientRoot, 'node_modules/.bin/prettier');
  }
  if (process.env.RVT_STANDARDS_ESLINT_COMMAND === undefined && typescriptPaths.length > 0) {
    requirePortalExecutable(clientRoot, 'node_modules/.bin/eslint');
  }

  const prettierResult = runProcess(
    prettier,
    ['--list-different', ...clientPaths],
    clientRoot,
    'Prettier'
  );
  if (prettierResult.status !== 0 && prettierResult.status !== 1) {
    throw new InvocationError(`Prettier exited with internal/configuration status ${prettierResult.status}`);
  }
  if (prettierResult.status !== 0) {
    const reported = prettierResult.stdout
      .split('\n')
      .filter((item) => item !== '');
    if (reported.length === 0) {
      throw new InvocationError(
        `Prettier exited ${prettierResult.status} without a readable file report`
      );
    }
    for (const candidate of reported) {
      const repositoryPath = validateToolPath(
        repoRoot,
        path.resolve(clientRoot, candidate),
        'Prettier'
      );
      if (!paths.includes(repositoryPath)) {
        throw new InvocationError(`Prettier reported an unexpected path: ${candidate}`);
      }
      if (inventory) {
        diagnostics.push({
          tool: 'prettier',
          ruleId: 'prettier/format',
          path: repositoryPath,
          line: 1,
          message: 'File differs from Prettier formatting'
        });
      }
    }
    if (!inventory) {
      immediateViolations.push(
        `WEB-001 Prettier violation in changed Portal files: ${paths.join(', ')}`
      );
    }
  }

  if (typescriptPaths.length === 0) return diagnostics;
  const eslintResult = runProcess(
    eslint,
    ['--format', 'json', '--no-warn-ignored', ...typescriptPaths],
    clientRoot,
    'ESLint'
  );
  if (eslintResult.status !== 0 && eslintResult.status !== 1) {
    throw new InvocationError(`ESLint exited with internal/configuration status ${eslintResult.status}`);
  }
  if (eslintResult.status !== 0 && eslintResult.stdout.trim() === '') {
    throw new InvocationError(
      `ESLint exited ${eslintResult.status} without a readable JSON report`
    );
  }

  let report;
  try {
    report = JSON.parse(eslintResult.stdout);
  } catch (error) {
    throw new InvocationError(`ESLint report is malformed: ${error.message}`);
  }
  let parsed;
  try {
    parsed = parseEslintReport(report, repoRoot);
  } catch (error) {
    throw new InvocationError(`ESLint report is invalid: ${error.message}`);
  }
  if (eslintResult.status !== 0 && parsed.length === 0) {
    throw new InvocationError(
      `ESLint exited ${eslintResult.status} without diagnostics`
    );
  }
  const selectedPaths = new Set(
    typescriptPaths.map((item) => `${portalPrefix}${item}`)
  );
  const unexpected = parsed.find((item) => !selectedPaths.has(item.path));
  if (unexpected !== undefined) {
    throw new InvocationError(
      `ESLint reported an unexpected path: ${unexpected.path}`
    );
  }
  diagnostics.push(...parsed);
  return diagnostics;
}

function requirePortalExecutable(clientRoot, relativePath) {
  try {
    accessSync(path.join(clientRoot, relativePath), constants.X_OK);
  } catch {
    throw new InvocationError(
      `Missing Portal executable ${relativePath}; run npm ci in apps/portal/RvtPortal.Client`
    );
  }
}

function validateToolPath(repoRoot, candidate, label) {
  try {
    return normalizeRepositoryPath(repoRoot, candidate);
  } catch (error) {
    throw new InvocationError(`${label} report path is invalid: ${error.message}`);
  }
}

function printRatchet(result, immediateViolations) {
  for (const violation of immediateViolations) {
    process.stderr.write(`Policy violation: ${violation}\n`);
  }
  for (const item of result.changedSurfaceViolations) {
    process.stderr.write(
      `Policy violation: changed surface ${item.tool} ${item.ruleId} ${item.path}:${item.line}: ${item.message}\n`
    );
  }
  for (const item of result.increases) {
    process.stderr.write(
      `Policy violation: increase ${item.tool} ${item.ruleId} ${item.path} baseline=${item.baseline} observed=${item.observed}\n`
    );
  }
  for (const item of result.decreases) {
    process.stdout.write(
      `Baseline decrease: ${item.tool} ${item.ruleId} ${item.path} baseline=${item.baseline} observed=${item.observed}\n`
    );
  }
}

function baselineDocumentFromResult(result) {
  const entries = [...result.increases, ...result.decreases, ...result.unchanged]
    .filter((item) => item.observed > 0)
    .map(({ tool, ruleId, path: itemPath, observed }) => ({
      tool,
      ruleId,
      path: itemPath,
      count: observed
    }))
    .sort((left, right) => compareCodePoints(
      diagnosticKey(left),
      diagnosticKey(right)
    ));
  return {
    version: 1,
    generatedAt: new Date().toISOString().slice(0, 10),
    entries
  };
}

function compareCodePoints(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}

function writeBaselineAtomically(filePath, document, mustNotExist, beforeCommit = () => {}) {
  const parent = path.dirname(filePath);
  const temporary = path.join(
    parent,
    `.${path.basename(filePath)}.${process.pid}.${Date.now()}.tmp`
  );
  try {
    writeFileSync(temporary, `${JSON.stringify(document, null, 2)}\n`, {
      encoding: 'utf8',
      flag: 'wx'
    });
    if (mustNotExist) {
      beforeCommit();
      linkSync(temporary, filePath);
      rmSync(temporary);
    } else {
      beforeCommit();
      renameSync(temporary, filePath);
    }
  } catch (error) {
    rmSync(temporary, { force: true });
    if (mustNotExist && error?.code === 'EEXIST') {
      throw new PolicyError(
        `Baseline already exists; initialization is refused: ${filePath}`
      );
    }
    throw new InvocationError(`Could not atomically write baseline: ${error.message}`);
  }
}

function updateBaselineMonotonically(repoRoot, filePath, candidate) {
  validateContainedPath(repoRoot, filePath, 'Policy path');
  const canonicalTarget = realpathSync(filePath);
  const lockPath = `${canonicalTarget}.update.lock`;
  const token = randomUUID();
  const sentinel = createLockSentinel(token);
  const owner = {
    version: 2,
    pid: process.pid,
    sentinelPid: sentinel.pid,
    token,
    createdAt: new Date().toISOString()
  };
  let ownsLock = false;
  try {
    for (let attempt = 0; attempt < 400; attempt += 1) {
      try {
        mkdirSync(lockPath);
        writeFileSync(
          path.join(lockPath, 'owner.json'),
          `${JSON.stringify(owner)}\n`,
          { encoding: 'utf8', flag: 'wx' }
        );
        ownsLock = true;
        break;
      } catch (error) {
        if (error.code !== 'EEXIST') {
          throw new InvocationError(`Could not acquire baseline-update lock: ${error.message}`);
        }
        reclaimStaleLock(lockPath);
        Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 25);
      }
    }
    if (!ownsLock) {
      throw new InvocationError('Timed out waiting for baseline-update lock');
    }

    const liveDocument = readJsonDocument(filePath, 'Baseline document');
    try {
      validateBaseline(liveDocument);
    } catch (error) {
      throw new InvocationError(error.message);
    }
    const live = baselineMap(liveDocument);
    const widened = candidate.entries.find(
      (entry) => entry.count > (live.get(diagnosticKey(entry)) ?? 0)
    );
    if (widened !== undefined) {
      throw new PolicyError(
        `Concurrent baseline update would increase ${diagnosticKey(widened)}`
      );
    }
    writeBaselineAtomically(
      filePath,
      candidate,
      false,
      () => assertOwnedLock(lockPath, owner)
    );
  } finally {
    if (ownsLock) releaseOwnedLock(lockPath, owner.token);
    stopLockSentinel(sentinel);
  }
}

function lockSnapshot(lockPath) {
  let directoryStat;
  try {
    directoryStat = statSync(lockPath);
  } catch (error) {
    if (error.code === 'ENOENT') return undefined;
    throw new InvocationError(`Could not inspect baseline-update lock: ${error.message}`);
  }
  try {
    const value = JSON.parse(readFileSync(path.join(lockPath, 'owner.json'), 'utf8'));
    if (
      value?.version === 2 &&
      Number.isSafeInteger(value.pid) &&
      value.pid > 0 &&
      Number.isSafeInteger(value.sentinelPid) &&
      value.sentinelPid > 0 &&
      typeof value.token === 'string' &&
      value.token !== '' &&
      Number.isFinite(Date.parse(value.createdAt))
    ) {
      return { kind: 'owner', ...value };
    }
  } catch {
    // A creator can crash before its metadata is complete.
  }
  return { kind: 'partial', mtimeMs: directoryStat.mtimeMs };
}

function processIsAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === 'EPERM';
  }
}

function sentinelMarker(token) {
  return `rvt-standards-lock-sentinel=${token}`;
}

function createLockSentinel(token) {
  const child = spawn(
    process.execPath,
    [
      '-e',
      'process.on("disconnect",()=>process.exit(0));setInterval(()=>{},60000)',
      sentinelMarker(token)
    ],
    {
      stdio: ['ignore', 'ignore', 'ignore', 'ipc']
    }
  );
  if (!Number.isSafeInteger(child.pid) || child.pid <= 0) {
    child.kill();
    throw new InvocationError('Could not create baseline-update lock sentinel');
  }
  return child;
}

function stopLockSentinel(child) {
  if (child.connected) child.disconnect();
  if (child.exitCode === null && child.signalCode === null) child.kill();
}

function observeLockSentinel(pid, token) {
  const aliveBefore = processIsAlive(pid);
  if (!aliveBefore) return 'dead';
  const result = spawnSync('ps', ['-ww', '-o', 'command=', '-p', String(pid)], {
    encoding: 'utf8',
    shell: false,
    env: { ...process.env, LC_ALL: 'C' }
  });
  if (result.error !== undefined || result.status !== 0) {
    if (!processIsAlive(pid)) return 'dead';
    throw new InvocationError(
      'Could not observe baseline-update lock sentinel; refusing stale-lock reclamation'
    );
  }
  if (!processIsAlive(pid)) return 'dead';
  return result.stdout.includes(sentinelMarker(token)) ? 'owner' : 'collision';
}

function assertOwnedLock(lockPath, owner) {
  const snapshot = lockSnapshot(lockPath);
  if (
    snapshot?.kind !== 'owner' ||
    snapshot.token !== owner.token ||
    snapshot.sentinelPid !== owner.sentinelPid ||
    observeLockSentinel(owner.sentinelPid, owner.token) !== 'owner'
  ) {
    throw new InvocationError(
      'Baseline-update lock ownership changed at the mutation boundary'
    );
  }
}

function reclaimStaleLock(lockPath) {
  const snapshot = lockSnapshot(lockPath);
  if (snapshot === undefined) return;
  if (snapshot.kind === 'owner') {
    if (observeLockSentinel(snapshot.sentinelPid, snapshot.token) === 'owner') return;
  }
  if (
    snapshot.kind === 'partial' &&
    Date.now() - snapshot.mtimeMs < 1000
  ) {
    return;
  }
  moveAndRemoveMatchingLock(lockPath, snapshot, 'stale');
}

function releaseOwnedLock(lockPath, token) {
  const snapshot = lockSnapshot(lockPath);
  if (snapshot?.kind !== 'owner' || snapshot.token !== token) return;
  moveAndRemoveMatchingLock(lockPath, snapshot, 'release');
}

function moveAndRemoveMatchingLock(lockPath, expected, purpose) {
  const movedPath = `${lockPath}.${purpose}.${process.pid}.${randomUUID()}`;
  try {
    renameSync(lockPath, movedPath);
  } catch (error) {
    if (error.code === 'ENOENT') return false;
    throw new InvocationError(`Could not claim baseline-update lock: ${error.message}`);
  }
  const actual = lockSnapshot(movedPath);
  const matches =
    expected.kind === 'owner'
      ? actual?.kind === 'owner' && actual.token === expected.token
      : actual?.kind === 'partial' && actual.mtimeMs === expected.mtimeMs;
  if (matches) {
    rmSync(movedPath, { recursive: true, force: true });
    return true;
  }
  if (!existsSync(lockPath)) {
    try {
      renameSync(movedPath, lockPath);
    } catch (error) {
      throw new InvocationError(
        `Baseline-update lock ownership changed during ${purpose}: ${error.message}`
      );
    }
  }
  throw new InvocationError(
    `Baseline-update lock ownership changed during ${purpose}; refusing deletion`
  );
}

function repositoryRoot() {
  const result = runProcess(['git'], ['rev-parse', '--show-toplevel'], process.cwd(), 'Git');
  if (result.status !== 0) {
    throw new InvocationError('The verifier must run inside a Git repository');
  }
  const root = result.stdout.trim();
  if (root === '') throw new InvocationError('Git returned an empty repository root');
  return path.resolve(root);
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  if (options.help) return 0;
  const repoRoot = repositoryRoot();
  const policy = loadPolicy(repoRoot, options);
  const scope = resolveScope(repoRoot, options);
  let execution;
  try {
    execution = collectDiagnostics(scope.executionRoot, scope);
  } finally {
    scope.cleanup();
  }
  const result = compareRatchet({
    diagnostics: execution.diagnostics,
    baseline: policy.baseline,
    newPaths: scope.newPaths,
    changedRanges: scope.changedRanges,
    exceptions: policy.exceptions
  });
  printRatchet(
    options.initialize ? { ...result, increases: [] } : result,
    execution.immediateViolations
  );

  const violated =
    execution.immediateViolations.length > 0 ||
    result.changedSurfaceViolations.length > 0 ||
    (!options.initialize && result.increases.length > 0);
  if (violated) return 1;

  if (options.initialize || options.update) {
    const candidate = baselineDocumentFromResult(result);
    if (options.update) {
      updateBaselineMonotonically(repoRoot, policy.baselinePath, candidate);
    } else {
      writeBaselineAtomically(policy.baselinePath, candidate, true);
    }
  }
  return 0;
}

try {
  process.exitCode = main();
} catch (error) {
  if (error instanceof PolicyError) {
    process.stderr.write(`Policy violation: ${error.message}\n`);
    process.exitCode = 1;
  } else {
    process.stderr.write(`Invocation/tool failure: ${error.message}\n`);
    process.exitCode = 2;
  }
}
