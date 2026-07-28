import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
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

test('normalizeRepositoryPath handles Windows drive and UNC roots', () => {
  assert.equal(
    normalizeRepositoryPath(
      String.raw`C:\work\rvt`,
      String.raw`C:\work\rvt\src\Clock.cs`
    ),
    'src/Clock.cs'
  );
  assert.equal(
    normalizeRepositoryPath(
      String.raw`\\build-server\source\rvt`,
      String.raw`\\build-server\source\rvt\src\Clock.cs`
    ),
    'src/Clock.cs'
  );
});

test('normalizeRepositoryPath rejects Windows traversal and cross-root paths', () => {
  assert.throws(
    () => normalizeRepositoryPath(
      String.raw`C:\work\rvt`,
      String.raw`C:\work\outside\Clock.cs`
    ),
    /outside repository root/i
  );
  assert.throws(
    () => normalizeRepositoryPath(
      String.raw`C:\work\rvt`,
      String.raw`D:\work\rvt\src\Clock.cs`
    ),
    /outside repository root/i
  );
  assert.throws(
    () => normalizeRepositoryPath(
      String.raw`\\build-server\source\rvt`,
      String.raw`\\other-server\source\rvt\src\Clock.cs`
    ),
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

test('parseEslintReport assigns a stable rule to fatal parse messages', () => {
  const report = [{
    filePath: '/repo/src/broken.ts',
    messages: [
      {
        ruleId: null,
        fatal: true,
        severity: 2,
        message: 'Parsing error: Expression expected.',
        line: 1,
        column: 4
      },
      {
        ruleId: '',
        fatal: true,
        severity: 2,
        message: 'Parsing error: Declaration expected.',
        line: 2,
        column: 1
      }
    ]
  }];

  assert.deepEqual(parseEslintReport(report, repoRoot), [
    {
      tool: 'eslint',
      ruleId: 'eslint/fatal-parse-error',
      path: 'src/broken.ts',
      line: 1,
      message: 'Parsing error: Expression expected.'
    },
    {
      tool: 'eslint',
      ruleId: 'eslint/fatal-parse-error',
      path: 'src/broken.ts',
      line: 2,
      message: 'Parsing error: Declaration expected.'
    }
  ]);
});

test('report parsers reject invalid one-based diagnostic lines', () => {
  const invalidLines = [undefined, 0, -1, 1.5, '4'];

  for (const line of invalidLines) {
    assert.throws(
      () => parseDotnetFormatReport([{
        FilePath: '/repo/src/Clock.cs',
        FileChanges: [{
          LineNumber: line,
          DiagnosticId: 'IDE0055',
          FormatDescription: 'Fix formatting'
        }]
      }], repoRoot),
      /line.*positive integer/i
    );

    assert.throws(
      () => parseEslintReport([{
        filePath: '/repo/src/calendarDate.ts',
        messages: [{
          ruleId: '@typescript-eslint/no-unused-vars',
          severity: 2,
          message: 'Unused value.',
          line
        }]
      }], repoRoot),
      /line.*positive integer/i
    );
  }
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

test('validateBaseline rejects the repository root as a diagnostic path', () => {
  assert.throws(
    () => validateBaseline({
      version: 1,
      entries: [{
        tool: 'dotnet-format-style',
        ruleId: 'IDE0055',
        path: '.',
        count: 1
      }]
    }),
    /exact repository-relative path/i
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

test('validateExceptions rejects reviewOn before introducedOn', () => {
  assert.throws(
    () => validateExceptions(
      {
        version: 1,
        exceptions: [exception({ reviewOn: '2026-06-30' })]
      },
      new Date('2026-06-29T00:00:00Z')
    ),
    /reviewOn must be after introducedOn/i
  );
});

test('validateExceptions rejects reviewOn equal to introducedOn', () => {
  assert.throws(
    () => validateExceptions(
      {
        version: 1,
        exceptions: [exception({ reviewOn: '2026-07-01' })]
      },
      new Date('2026-06-30T00:00:00Z')
    ),
    /reviewOn must be after introducedOn/i
  );
});

test('validateExceptions accepts reviewOn one day after introducedOn', () => {
  assert.doesNotThrow(() => validateExceptions(
    { version: 1, exceptions: [exception({ reviewOn: '2026-07-02' })] },
    new Date('2026-07-01T00:00:00Z')
  ));
});

test('validateExceptions rejects wildcard paths and unsupported symbol scopes', () => {
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
    /symbol-scoped exceptions are not supported/i
  );
  assert.throws(
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
    ),
    /symbol-scoped exceptions are not supported.*exact path/i
  );
});

test('validateExceptions rejects the repository root as an exact path', () => {
  assert.throws(
    () => validateExceptions(
      { version: 1, exceptions: [exception({ path: '.' })] },
      new Date('2026-07-27T00:00:00Z')
    ),
    /exact repository-relative path/i
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

test('compareRatchet reports a decrease for a baseline-only key', () => {
  const result = compareRatchet({
    diagnostics: [],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/RemovedClock.cs', 2]
    ]),
    newPaths: new Set(),
    changedRanges: new Map(),
    exceptions: []
  });

  assert.deepEqual(result.decreases, [{
    tool: 'dotnet-format-style',
    ruleId: 'IDE0055',
    path: 'src/RemovedClock.cs',
    baseline: 2,
    observed: 0
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

test('compareRatchet returns deterministic changed-surface ordering', () => {
  const diagnostics = [
    diagnostic({ ruleId: 'IDE0055', line: 7, message: 'Zulu' }),
    diagnostic({ ruleId: 'IDE0005', line: 12, message: 'Unused import' }),
    diagnostic({ ruleId: 'IDE0055', line: 7, message: 'Alpha' }),
    diagnostic({ ruleId: 'IDE0055', line: 3, message: 'Early' })
  ];
  const input = {
    baseline: new Map(),
    newPaths: new Set(['src/Clock.cs']),
    changedRanges: new Map(),
    exceptions: []
  };

  const forward = compareRatchet({ ...input, diagnostics });
  const reversed = compareRatchet({
    ...input,
    diagnostics: [...diagnostics].reverse()
  });

  assert.deepEqual(
    forward.changedSurfaceViolations,
    reversed.changedSurfaceViolations
  );
  assert.deepEqual(
    forward.changedSurfaceViolations.map(({ ruleId, line, message }) => ({
      ruleId,
      line,
      message
    })),
    [
      { ruleId: 'IDE0005', line: 12, message: 'Unused import' },
      { ruleId: 'IDE0055', line: 3, message: 'Early' },
      { ruleId: 'IDE0055', line: 7, message: 'Alpha' },
      { ruleId: 'IDE0055', line: 7, message: 'Zulu' }
    ]
  );
});

test('compareRatchet does not mutate caller-owned inputs', () => {
  const inputs = {
    diagnostics: [diagnostic({ line: 9 }), diagnostic({ line: 4 })],
    baseline: new Map([
      ['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 2]
    ]),
    newPaths: new Set(['src/NewClock.cs']),
    changedRanges: new Map([
      ['src/Clock.cs', [{ startLine: 8, endLine: 10 }]]
    ]),
    exceptions: [exception({ ruleId: 'IDE0005' })]
  };
  const before = {
    diagnostics: structuredClone(inputs.diagnostics),
    baseline: [...inputs.baseline],
    newPaths: [...inputs.newPaths],
    changedRanges: [...inputs.changedRanges].map(([key, ranges]) => [
      key,
      structuredClone(ranges)
    ]),
    exceptions: structuredClone(inputs.exceptions)
  };

  compareRatchet(inputs);

  assert.deepEqual(inputs.diagnostics, before.diagnostics);
  assert.deepEqual([...inputs.baseline], before.baseline);
  assert.deepEqual([...inputs.newPaths], before.newPaths);
  assert.deepEqual(
    [...inputs.changedRanges].map(([key, ranges]) => [key, ranges]),
    before.changedRanges
  );
  assert.deepEqual(inputs.exceptions, before.exceptions);
});
