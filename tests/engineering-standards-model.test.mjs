import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

import {
  compareRatchet,
  countDiagnostics,
  diagnosticKey,
  normalizeRepositoryPath,
  parseDotnetFormatReport,
  parseEslintReport,
  validateBaseline,
  validateExceptions
} from '../scripts/engineering-standards/model.mjs';

const repoRoot = '/repo';
const fixtureRoot = new URL('./fixtures/engineering-standards/', import.meta.url);

const diagnostic = (overrides = {}) => ({
  tool: 'dotnet-format-style',
  ruleId: 'IDE0055',
  path: 'src/Clock.cs',
  line: 7,
  message: 'Fix formatting',
  ...overrides
});

const exception = (overrides = {}) => ({
  id: 'EX-001',
  ruleId: 'IDE0055',
  owner: 'platform-team',
  path: 'src/Clock.cs',
  justification: 'Compatibility requires the current shape.',
  introducedOn: '2026-07-01',
  reviewOn: '2026-08-01',
  removalCondition: 'Remove after the compatibility window closes.',
  validation: 'The exact rule and path were verified against the format report.',
  ...overrides
});

async function readFixture(name) {
  return JSON.parse(await readFile(new URL(name, fixtureRoot), 'utf8'));
}

test('normalizeRepositoryPath returns a repository-relative POSIX path', () => {
  assert.equal(
    normalizeRepositoryPath(repoRoot, '/repo/src/nested/Clock.cs'),
    'src/nested/Clock.cs'
  );
  assert.equal(
    normalizeRepositoryPath(repoRoot, String.raw`\repo\src\calendarDate.ts`),
    'src/calendarDate.ts'
  );
});

test('normalizeRepositoryPath rejects a path outside the repository root', () => {
  assert.throws(
    () => normalizeRepositoryPath(repoRoot, '/repo-other/src/Clock.cs'),
    /outside repository root/i
  );
});

test('parsers normalize realistic dotnet-format and ESLint reports', async () => {
  const dotnetReport = await readFixture('dotnet-format-report.json');
  const eslintReport = await readFixture('eslint-report.json');

  assert.deepEqual(parseDotnetFormatReport(dotnetReport, repoRoot), [
    diagnostic()
  ]);
  assert.deepEqual(parseEslintReport(eslintReport, repoRoot), [
    {
      tool: 'eslint',
      ruleId: '@typescript-eslint/no-unused-vars',
      path: 'src/calendarDate.ts',
      line: 4,
      message: "'unused' is assigned a value but never used."
    }
  ]);
});

test('diagnosticKey and countDiagnostics use exact tab-separated identity', () => {
  assert.equal(
    diagnosticKey(diagnostic()),
    'dotnet-format-style\tIDE0055\tsrc/Clock.cs'
  );

  assert.deepEqual(
    [...countDiagnostics([
      diagnostic(),
      diagnostic({ line: 11 }),
      diagnostic({ ruleId: 'IDE0005' })
    ])],
    [
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 2],
      ['dotnet-format-style\tIDE0005\tsrc/Clock.cs', 1]
    ]
  );
});

test('validateBaseline accepts unique non-negative integer entries', () => {
  assert.doesNotThrow(() => validateBaseline({
    version: 1,
    generatedAt: '2026-07-27',
    entries: [
      {
        tool: 'dotnet-format-style',
        ruleId: 'IDE0055',
        path: 'src/Clock.cs',
        count: 0
      }
    ]
  }));
});

test('validateBaseline rejects duplicate, negative, and fractional counts', () => {
  const validEntry = {
    tool: 'dotnet-format-style',
    ruleId: 'IDE0055',
    path: 'src/Clock.cs',
    count: 1
  };

  assert.throws(
    () => validateBaseline({ version: 1, entries: [validEntry, { ...validEntry }] }),
    /duplicate baseline entry/i
  );
  assert.throws(
    () => validateBaseline({
      version: 1,
      entries: [{ ...validEntry, count: -1 }]
    }),
    /non-negative integer/i
  );
  assert.throws(
    () => validateBaseline({
      version: 1,
      entries: [{ ...validEntry, count: 1.5 }]
    }),
    /non-negative integer/i
  );
});

test('validateExceptions rejects expired exceptions', () => {
  assert.throws(
    () => validateExceptions(
      { version: 1, exceptions: [exception({ reviewOn: '2026-07-26' })] },
      new Date('2026-07-27T00:00:00Z')
    ),
    /expired exception EX-001/i
  );
});

test('validateExceptions rejects wildcard paths and unvalidated symbol scopes', () => {
  assert.throws(
    () => validateExceptions(
      { version: 1, exceptions: [exception({ path: 'src/**/*.cs' })] },
      new Date('2026-07-27T00:00:00Z')
    ),
    /exact repository-relative path/i
  );
  assert.throws(
    () => validateExceptions(
      {
        version: 1,
        exceptions: [
          exception({
            path: undefined,
            symbol: 'Clock.Tick',
            validator: undefined
          })
        ]
      },
      new Date('2026-07-27T00:00:00Z')
    ),
    /rule-specific validator/i
  );
  assert.doesNotThrow(
    () => validateExceptions(
      {
        version: 1,
        exceptions: [
          exception({
            path: undefined,
            symbol: 'Clock.Tick',
            validator: { ruleId: 'IDE0055', name: 'dotnet-symbol-validator' }
          })
        ]
      },
      new Date('2026-07-27T00:00:00Z')
    )
  );
});

test('compareRatchet reports a changed-scope baseline increase', () => {
  const result = compareRatchet({
    diagnostics: [
      diagnostic(),
      diagnostic({ line: 11 })
    ],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 1]
    ]),
    newPaths: new Set(),
    changedRanges: new Map([
      ['src/Clock.cs', [{ startLine: 7, endLine: 7 }]]
    ]),
    exceptions: []
  });

  assert.deepEqual(result.changedSurfaceViolations, [diagnostic()]);
  assert.deepEqual(result.increases, [{
    tool: 'dotnet-format-style',
    ruleId: 'IDE0055',
    path: 'src/Clock.cs',
    baseline: 1,
    observed: 2
  }]);
  assert.deepEqual(result.decreases, []);
  assert.deepEqual(result.unchanged, []);
});

test('compareRatchet reports a baseline decrease and unchanged count', () => {
  const result = compareRatchet({
    diagnostics: [
      diagnostic(),
      diagnostic({ ruleId: 'IDE0005', line: 20 })
    ],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 2],
      ['dotnet-format-style\tIDE0005\tsrc/Clock.cs', 1]
    ]),
    newPaths: new Set(),
    changedRanges: new Map(),
    exceptions: []
  });

  assert.deepEqual(result.decreases, [{
    tool: 'dotnet-format-style',
    ruleId: 'IDE0055',
    path: 'src/Clock.cs',
    baseline: 2,
    observed: 1
  }]);
  assert.deepEqual(result.unchanged, [{
    tool: 'dotnet-format-style',
    ruleId: 'IDE0005',
    path: 'src/Clock.cs',
    baseline: 1,
    observed: 1
  }]);
});

test('compareRatchet rejects a stable-count diagnostic on a changed line', () => {
  const changed = diagnostic({ line: 9 });
  const result = compareRatchet({
    diagnostics: [changed],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 1]
    ]),
    newPaths: new Set(),
    changedRanges: new Map([
      ['src/Clock.cs', [{ startLine: 9, endLine: 9 }]]
    ]),
    exceptions: []
  });

  assert.deepEqual(result.changedSurfaceViolations, [changed]);
  assert.equal(result.increases.length, 0);
  assert.equal(result.unchanged[0].observed, 1);
});

test('compareRatchet allows a pre-existing diagnostic outside changed ranges', () => {
  const existing = diagnostic({ line: 4 });
  const result = compareRatchet({
    diagnostics: [existing],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 1]
    ]),
    newPaths: new Set(),
    changedRanges: new Map([
      ['src/Clock.cs', [{ startLine: 9, endLine: 12 }]]
    ]),
    exceptions: []
  });

  assert.deepEqual(result.changedSurfaceViolations, []);
  assert.equal(result.unchanged[0].observed, 1);
});

test('compareRatchet rejects every diagnostic in a new file', () => {
  const newFileDiagnostic = diagnostic({ path: 'src/NewClock.cs', line: 1 });
  const result = compareRatchet({
    diagnostics: [newFileDiagnostic],
    baseline: new Map(),
    newPaths: new Set(['src/NewClock.cs']),
    changedRanges: new Map(),
    exceptions: []
  });

  assert.deepEqual(result.changedSurfaceViolations, [newFileDiagnostic]);
  assert.equal(result.increases[0].baseline, 0);
  assert.equal(result.increases[0].observed, 1);
});

test('compareRatchet matches generic exceptions by exact rule and path only', () => {
  const exact = diagnostic();
  const otherRule = diagnostic({ ruleId: 'IDE0005' });
  const broaderPath = diagnostic({ path: 'src/nested/Clock.cs' });
  const result = compareRatchet({
    diagnostics: [exact, otherRule, broaderPath],
    baseline: new Map(),
    newPaths: new Set([
      'src/Clock.cs',
      'src/nested/Clock.cs'
    ]),
    changedRanges: new Map(),
    exceptions: [exception()]
  });

  assert.deepEqual(result.changedSurfaceViolations, [otherRule, broaderPath]);
  assert.deepEqual(
    result.increases.map(({ ruleId, path }) => ({ ruleId, path })),
    [
      { ruleId: 'IDE0005', path: 'src/Clock.cs' },
      { ruleId: 'IDE0055', path: 'src/nested/Clock.cs' }
    ]
  );
});
