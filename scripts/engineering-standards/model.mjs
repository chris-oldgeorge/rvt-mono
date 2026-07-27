import path from 'node:path';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const WILDCARD = /[*?[\]{}]/;

export function normalizeRepositoryPath(repoRoot, candidate) {
  const normalizedRoot = path.posix.resolve(toPosixPath(repoRoot));
  const normalizedCandidate = path.posix.resolve(
    normalizedRoot,
    toPosixPath(candidate)
  );
  const relative = path.posix.relative(normalizedRoot, normalizedCandidate);

  if (
    relative === '' ||
    relative === '..' ||
    relative.startsWith('../') ||
    path.posix.isAbsolute(relative)
  ) {
    throw new Error(`Path is outside repository root: ${candidate}`);
  }

  return relative;
}

export function parseDotnetFormatReport(report, repoRoot = process.cwd()) {
  assertArray(report, '.NET format report');

  return report.flatMap((file) => {
    assertNonEmptyString(file?.FilePath, '.NET format FilePath');
    assertArray(file?.FileChanges, '.NET format FileChanges');

    return file.FileChanges.map((change) => ({
      tool: 'dotnet-format-style',
      ruleId: requiredString(change?.DiagnosticId, '.NET diagnostic ID'),
      path: normalizeRepositoryPath(repoRoot, file.FilePath),
      line: diagnosticLine(change?.LineNumber),
      message: requiredString(
        change?.FormatDescription,
        '.NET diagnostic description'
      )
    }));
  });
}

export function parseEslintReport(report, repoRoot = process.cwd()) {
  assertArray(report, 'ESLint report');

  return report.flatMap((file) => {
    assertNonEmptyString(file?.filePath, 'ESLint filePath');
    assertArray(file?.messages, 'ESLint messages');

    return file.messages
      .filter((message) => message?.severity > 0)
      .map((message) => ({
        tool: 'eslint',
        ruleId: requiredString(message?.ruleId, 'ESLint rule ID'),
        path: normalizeRepositoryPath(repoRoot, file.filePath),
        line: diagnosticLine(message?.line),
        message: requiredString(message?.message, 'ESLint message')
      }));
  });
}

export function diagnosticKey({ tool, ruleId, path: diagnosticPath }) {
  return `${tool}\t${ruleId}\t${diagnosticPath}`;
}

export function countDiagnostics(diagnostics) {
  const counts = new Map();

  for (const item of diagnostics) {
    const key = diagnosticKey(item);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }

  return counts;
}

export function validateBaseline(document) {
  assertVersionOne(document, 'baseline');
  assertArray(document.entries, 'baseline entries');

  const keys = new Set();
  for (const [index, entry] of document.entries.entries()) {
    validateDiagnosticIdentity(entry, `baseline entry ${index}`);
    validateExactRepositoryPath(entry.path, `baseline entry ${index} path`);

    if (!Number.isInteger(entry.count) || entry.count < 0) {
      throw new Error(
        `Baseline entry ${index} count must be a non-negative integer`
      );
    }

    const key = diagnosticKey(entry);
    if (keys.has(key)) {
      throw new Error(`Duplicate baseline entry: ${key}`);
    }
    keys.add(key);
  }
}

export function validateExceptions(document, now = new Date()) {
  assertVersionOne(document, 'exceptions');
  assertArray(document.exceptions, 'exceptions');

  if (!(now instanceof Date) || Number.isNaN(now.valueOf())) {
    throw new Error('Exception validation time must be a valid Date');
  }

  const today = now.toISOString().slice(0, 10);
  const ids = new Set();

  for (const [index, item] of document.exceptions.entries()) {
    const prefix = `exception ${index}`;
    for (const field of [
      'id',
      'ruleId',
      'owner',
      'justification',
      'introducedOn',
      'reviewOn',
      'removalCondition',
      'validation'
    ]) {
      assertNonEmptyString(item?.[field], `${prefix} ${field}`);
    }

    if (ids.has(item.id)) {
      throw new Error(`Duplicate exception ID: ${item.id}`);
    }
    ids.add(item.id);

    validateIsoDate(item.introducedOn, `${prefix} introducedOn`);
    validateIsoDate(item.reviewOn, `${prefix} reviewOn`);
    if (item.reviewOn < today) {
      throw new Error(`Expired exception ${item.id}: ${item.reviewOn}`);
    }

    const hasPath = item.path !== undefined;
    const hasSymbol = item.symbol !== undefined;
    if (hasPath === hasSymbol) {
      throw new Error(
        `${prefix} must define exactly one exact path or symbol scope`
      );
    }

    if (hasPath) {
      validateExactRepositoryPath(item.path, `${prefix} path`);
    } else {
      validateExactSymbol(item.symbol, `${prefix} symbol`);
      validateRuleSpecificValidator(item.validator, item.ruleId, prefix);
    }
  }
}

export function compareRatchet({
  diagnostics,
  baseline,
  newPaths,
  changedRanges,
  exceptions
}) {
  const activeDiagnostics = diagnostics.filter(
    (item) => !matchesGenericException(item, exceptions)
  );

  const changedSurfaceViolations = activeDiagnostics.filter(
    (item) =>
      newPaths.has(item.path) ||
      isLineChanged(item.line, changedRanges.get(item.path) ?? [])
  );

  const observedCounts = countDiagnostics(activeDiagnostics);
  const keys = [...new Set([...observedCounts.keys(), ...baseline.keys()])].sort();
  const result = {
    changedSurfaceViolations,
    increases: [],
    decreases: [],
    unchanged: []
  };

  for (const key of keys) {
    const identity = parseDiagnosticKey(key);
    const delta = {
      ...identity,
      baseline: baseline.get(key) ?? 0,
      observed: observedCounts.get(key) ?? 0
    };

    if (delta.observed > delta.baseline) {
      result.increases.push(delta);
    } else if (delta.observed < delta.baseline) {
      result.decreases.push(delta);
    } else {
      result.unchanged.push(delta);
    }
  }

  return result;
}

function toPosixPath(value) {
  assertNonEmptyString(value, 'path');
  return value.replaceAll('\\', '/');
}

function requiredString(value, label) {
  assertNonEmptyString(value, label);
  return value;
}

function assertNonEmptyString(value, label) {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`${label} must be a non-empty string`);
  }
}

function assertArray(value, label) {
  if (!Array.isArray(value)) {
    throw new Error(`${label} must be an array`);
  }
}

function assertVersionOne(document, label) {
  if (
    document === null ||
    typeof document !== 'object' ||
    Array.isArray(document) ||
    document.version !== 1
  ) {
    throw new Error(`${label} version must be 1`);
  }
}

function validateDiagnosticIdentity(item, label) {
  for (const field of ['tool', 'ruleId', 'path']) {
    assertNonEmptyString(item?.[field], `${label} ${field}`);
    if (item[field].includes('\t')) {
      throw new Error(`${label} ${field} must not contain tabs`);
    }
  }
}

function validateExactRepositoryPath(value, label) {
  assertNonEmptyString(value, label);
  if (
    value.includes('\\') ||
    WILDCARD.test(value) ||
    path.posix.isAbsolute(value) ||
    path.posix.normalize(value) !== value ||
    value === '..' ||
    value.startsWith('../')
  ) {
    throw new Error(`${label} must be an exact repository-relative path`);
  }
}

function validateIsoDate(value, label) {
  if (!ISO_DATE.test(value)) {
    throw new Error(`${label} must be an ISO date`);
  }

  const parsed = new Date(`${value}T00:00:00Z`);
  if (
    Number.isNaN(parsed.valueOf()) ||
    parsed.toISOString().slice(0, 10) !== value
  ) {
    throw new Error(`${label} must be a valid ISO date`);
  }
}

function validateExactSymbol(value, label) {
  assertNonEmptyString(value, label);
  if (WILDCARD.test(value)) {
    throw new Error(`${label} must be exact`);
  }
}

function validateRuleSpecificValidator(validator, ruleId, label) {
  if (
    validator === null ||
    typeof validator !== 'object' ||
    Array.isArray(validator) ||
    validator.ruleId !== ruleId ||
    typeof validator.name !== 'string' ||
    validator.name.trim() === ''
  ) {
    throw new Error(`${label} symbol scope requires a rule-specific validator`);
  }
}

function diagnosticLine(value) {
  return Number.isInteger(value) && value >= 0 ? value : 0;
}

function matchesGenericException(item, exceptions) {
  return exceptions.some(
    (candidate) =>
      candidate.path !== undefined &&
      candidate.ruleId === item.ruleId &&
      candidate.path === item.path
  );
}

function isLineChanged(line, ranges) {
  return ranges.some(
    ({ startLine, endLine }) => line >= startLine && line <= endLine
  );
}

function parseDiagnosticKey(key) {
  const [tool, ruleId, diagnosticPath, ...extra] = key.split('\t');
  if (
    extra.length > 0 ||
    tool === undefined ||
    ruleId === undefined ||
    diagnosticPath === undefined
  ) {
    throw new Error(`Invalid diagnostic key: ${key}`);
  }

  return { tool, ruleId, path: diagnosticPath };
}
