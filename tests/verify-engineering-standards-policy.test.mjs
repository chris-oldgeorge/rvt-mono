import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  diagnosticKey,
  validateBaseline,
  validateExceptions
} from '../scripts/engineering-standards/model.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const fixedPolicyDate = new Date('2026-07-27T00:00:00.000Z');
const generatedPathSegments = new Set([
  '.codegraph',
  '.git',
  '.worktrees',
  'TestResults',
  'artifacts',
  'bin',
  'coverage',
  'dist',
  'node_modules',
  'obj',
  'playwright-report',
  'test-results'
]);
const frameworkPackages = new Map([
  ['MSTest.TestFramework', 'MSTest'],
  ['xunit', 'xUnit']
]);
const supportedFrameworks = new Set(['MSTest', 'xUnit']);
const supportedPackageVersionPolicies = new Set([
  'module-central',
  'module-central-locked',
  'project-inline-legacy'
]);

function readJson(relativePath) {
  const absolutePath = path.join(repoRoot, relativePath);
  let contents;
  try {
    contents = readFileSync(absolutePath, 'utf8');
  } catch (error) {
    throw new Error(`Policy file is missing or unreadable: ${relativePath}: ${error.message}`);
  }

  try {
    return JSON.parse(contents);
  } catch (error) {
    throw new Error(`Policy file is malformed JSON: ${relativePath}: ${error.message}`);
  }
}

function compareCodePoints(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}

function assertExactPolicyPath(candidate, label) {
  assert.equal(typeof candidate, 'string', `${label} must be a string`);
  assert.notEqual(candidate, '', `${label} must not be empty`);
  assert.equal(candidate, candidate.replaceAll('\\', '/'), `${label} must use POSIX separators`);
  assert.equal(
    path.posix.normalize(candidate),
    candidate,
    `${label} must be a normalized repository-relative path`
  );
  assert.equal(candidate.startsWith('/'), false, `${label} must be repository-relative`);
  assert.equal(candidate.endsWith('/'), false, `${label} must not end with a slash`);
  assert.equal(
    candidate.split('/').includes('..'),
    false,
    `${label} must not contain traversal`
  );
}

function validateModulePolicy(document) {
  assert.equal(document?.version, 1, 'module policy version must be 1');
  assert.ok(Array.isArray(document.modules), 'module policy modules must be an array');
  assert.ok(document.modules.length > 0, 'module policy must define supported modules');

  const paths = new Set();
  for (const [moduleIndex, module] of document.modules.entries()) {
    const label = `module ${moduleIndex}`;
    assertExactPolicyPath(module.path, `${label} path`);
    assert.ok(
      supportedFrameworks.has(module.testFramework),
      `${label} has unsupported test framework: ${module.testFramework}`
    );
    assert.ok(
      supportedPackageVersionPolicies.has(module.packageVersionPolicy),
      `${label} has unsupported package version policy: ${module.packageVersionPolicy}`
    );
    assert.equal(paths.has(module.path), false, `duplicate module policy path: ${module.path}`);
    paths.add(module.path);

    const overrides = module.testFrameworkOverrides ?? [];
    assert.ok(Array.isArray(overrides), `${label} testFrameworkOverrides must be an array`);
    for (const [overrideIndex, override] of overrides.entries()) {
      const overrideLabel = `${label} override ${overrideIndex}`;
      assertExactPolicyPath(override.path, `${overrideLabel} path`);
      assert.ok(
        isAtOrBelow(override.path, module.path) && override.path !== module.path,
        `${overrideLabel} must be below ${module.path}`
      );
      assert.ok(
        supportedFrameworks.has(override.testFramework),
        `${overrideLabel} has unsupported test framework: ${override.testFramework}`
      );
      assert.equal(
        paths.has(override.path),
        false,
        `duplicate module or override policy path: ${override.path}`
      );
      paths.add(override.path);
    }
  }
}

function isAtOrBelow(candidate, prefix) {
  return candidate === prefix || candidate.startsWith(`${prefix}/`);
}

function longestMatchingPolicy(projectPath, modulePolicy) {
  const matches = [];
  for (const module of modulePolicy.modules) {
    if (!isAtOrBelow(projectPath, module.path)) continue;
    matches.push({
      path: module.path,
      testFramework: module.testFramework,
      packageVersionPolicy: module.packageVersionPolicy
    });
    for (const override of module.testFrameworkOverrides ?? []) {
      if (!isAtOrBelow(projectPath, override.path)) continue;
      matches.push({
        path: override.path,
        testFramework: override.testFramework,
        packageVersionPolicy: module.packageVersionPolicy
      });
    }
  }

  return matches.sort((left, right) =>
    right.path.length - left.path.length ||
    compareCodePoints(left.path, right.path)
  )[0];
}

function parseAttributes(source) {
  const attributes = new Map();
  for (const match of source.matchAll(/([A-Za-z][A-Za-z0-9_.:-]*)\s*=\s*(["'])(.*?)\2/g)) {
    attributes.set(match[1], match[3]);
  }
  return attributes;
}

function parsePackageReferences(projectXml) {
  const references = [];
  for (const match of projectXml.matchAll(/<PackageReference\b([^>]*)>/g)) {
    const attributes = parseAttributes(match[1]);
    const include = attributes.get('Include') ?? attributes.get('Update');
    if (include === undefined) continue;
    references.push({
      include,
      version: attributes.get('Version') ?? attributes.get('VersionOverride')
    });
  }
  return references;
}

function solutionProjects() {
  const solution = readFileSync(path.join(repoRoot, 'Rvt.Mono.slnx'), 'utf8');
  const projectPaths = [...solution.matchAll(/<Project\s+Path=(["'])(.*?)\1\s*\/>/g)]
    .map((match) => match[2])
    .filter((projectPath) => projectPath.endsWith('.csproj'));
  assert.ok(projectPaths.length > 0, 'Rvt.Mono.slnx must reference projects');
  assert.equal(
    new Set(projectPaths).size,
    projectPaths.length,
    'Rvt.Mono.slnx must not reference a project more than once'
  );

  return projectPaths.map((projectPath) => {
    assertExactPolicyPath(projectPath, 'solution project path');
    const projectXml = readFileSync(path.join(repoRoot, projectPath), 'utf8');
    const packageReferences = parsePackageReferences(projectXml);
    const frameworks = new Set(
      packageReferences
        .map((reference) => frameworkPackages.get(reference.include))
        .filter((framework) => framework !== undefined)
    );
    return {
      path: projectPath,
      packageReferences,
      frameworks: [...frameworks].sort(compareCodePoints),
      isTestProject: frameworks.size > 0
    };
  });
}

function projectPolicyErrors(modulePolicy, projects) {
  const errors = [];
  for (const project of projects) {
    const policy = longestMatchingPolicy(project.path, modulePolicy);
    if (policy === undefined) {
      errors.push(`${project.path}: no module policy matches this solution project`);
      continue;
    }

    if (project.isTestProject) {
      if (
        project.frameworks.length !== 1 ||
        project.frameworks[0] !== policy.testFramework
      ) {
        errors.push(
          `${project.path}: expected ${policy.testFramework} from ${policy.path}; ` +
          `found ${project.frameworks.join(', ') || 'no supported test framework'}`
        );
      }
    }

    const inlineReferences = project.packageReferences
      .filter((reference) => reference.version !== undefined);
    const centralReferences = project.packageReferences
      .filter((reference) => reference.version === undefined);
    if (
      policy.packageVersionPolicy === 'project-inline-legacy' &&
      centralReferences.length > 0
    ) {
      errors.push(
        `${project.path}: project-inline-legacy requires inline versions for ` +
        centralReferences.map((reference) => reference.include).join(', ')
      );
    }
    if (
      policy.packageVersionPolicy !== 'project-inline-legacy' &&
      inlineReferences.length > 0
    ) {
      errors.push(
        `${project.path}: ${policy.packageVersionPolicy} forbids inline versions for ` +
        inlineReferences.map((reference) => reference.include).join(', ')
      );
    }
  }
  return errors;
}

test('legacy baseline is valid, deterministic, unique, and excludes generated paths', () => {
  const baseline = readJson('eng/standards/baseline.json');
  assert.deepEqual(
    Object.keys(baseline),
    ['version', 'generatedAt', 'entries'],
    'baseline must use the version/generatedAt/entries interface'
  );
  assert.equal(baseline.generatedAt, '2026-07-27');
  validateBaseline(baseline);

  const keys = baseline.entries.map(diagnosticKey);
  assert.deepEqual(
    keys,
    [...keys].sort(compareCodePoints),
    'baseline entries must use deterministic code-point ordering'
  );
  assert.equal(new Set(keys).size, keys.length, 'baseline diagnostic identities must be unique');
  for (const entry of baseline.entries) {
    assert.ok(Number.isInteger(entry.count) && entry.count >= 0);
    const generatedSegment = entry.path
      .split('/')
      .find((segment) => generatedPathSegments.has(segment));
    assert.equal(
      generatedSegment,
      undefined,
      `baseline contains generated/cache path segment ${generatedSegment}: ${entry.path}`
    );
  }
});

test('exceptions are valid at the fixed policy date and start empty', () => {
  const exceptions = readJson('eng/standards/exceptions.json');
  assert.deepEqual(exceptions, {
    version: 1,
    exceptions: []
  });
  validateExceptions(exceptions, fixedPolicyDate);
});

test('real solution projects match module framework and package-version policies', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  validateModulePolicy(modulePolicy);
  const projects = solutionProjects();
  const testProjects = projects.filter((project) => project.isTestProject);
  assert.ok(testProjects.length > 0, 'solution metadata must identify test projects');
  assert.deepEqual(projectPolicyErrors(modulePolicy, projects), []);
});

test('framework policy rejects an added xUnit package and a displaced override', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  validateModulePolicy(modulePolicy);
  const projects = solutionProjects();

  const mstestProject = projects.find(
    (project) =>
      project.path === 'apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj'
  );
  assert.ok(mstestProject, 'AirQMonitorTests must be present in the real solution metadata');
  const withAddedXunit = {
    ...mstestProject,
    packageReferences: [
      ...mstestProject.packageReferences,
      { include: 'xunit', version: undefined }
    ],
    frameworks: [...mstestProject.frameworks, 'xUnit'].sort(compareCodePoints)
  };
  assert.match(
    projectPolicyErrors(modulePolicy, [withAddedXunit]).join('\n'),
    /AirQMonitorTests\.csproj: expected MSTest .*found MSTest, xUnit/
  );

  const displacedOverride = structuredClone(modulePolicy);
  displacedOverride.modules[0].testFrameworkOverrides[0].path =
    'apps/monitors/reportingmonitor-moved';
  const reportingProject = projects.find(
    (project) =>
      project.path ===
      'apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj'
  );
  assert.ok(reportingProject, 'ReportingMonitorTests must be present in real solution metadata');
  assert.match(
    projectPolicyErrors(displacedOverride, [reportingProject]).join('\n'),
    /ReportingMonitorTests\.csproj: expected MSTest .*found xUnit/
  );
});
