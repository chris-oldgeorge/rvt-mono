import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
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
  ['mstest.testframework', 'MSTest'],
  ['xunit', 'xUnit']
]);
const supportedFrameworks = new Set(['MSTest', 'xUnit']);
const supportedPackageVersionPolicies = new Set([
  'module-central',
  'module-central-locked',
  'project-inline-legacy'
]);
const expectedModulePolicy = {
  version: 1,
  modules: [
    {
      path: 'apps/monitors',
      testFramework: 'MSTest',
      packageVersionPolicy: 'module-central',
      testFrameworkOverrides: [
        {
          path: 'apps/monitors/reportingmonitor',
          testFramework: 'xUnit'
        }
      ]
    },
    {
      path: 'apps/portal',
      testFramework: 'xUnit',
      packageVersionPolicy: 'project-inline-legacy'
    },
    {
      path: 'libs/rvt-monitor-common',
      testFramework: 'MSTest',
      packageVersionPolicy: 'module-central-locked'
    },
    {
      path: 'services/reporting',
      testFramework: 'xUnit',
      packageVersionPolicy: 'project-inline-legacy'
    }
  ]
};
const repositoryView = {
  readText(relativePath) {
    const absolutePath = path.join(repoRoot, relativePath);
    try {
      return readFileSync(absolutePath, 'utf8');
    } catch (error) {
      if (error.code === 'ENOENT') return undefined;
      throw error;
    }
  },
  exists(relativePath) {
    return existsSync(path.join(repoRoot, relativePath));
  }
};

function withRepositoryMutations({ text = new Map(), missing = new Set() }) {
  return {
    readText(relativePath) {
      if (missing.has(relativePath)) return undefined;
      return text.has(relativePath)
        ? text.get(relativePath)
        : repositoryView.readText(relativePath);
    },
    exists(relativePath) {
      return !missing.has(relativePath) && repositoryView.exists(relativePath);
    }
  };
}

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

  assert.deepEqual(
    document,
    expectedModulePolicy,
    'module policy must match the exact governed module and override shape'
  );
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

function parsePackageVersions(directoryPackagesXml) {
  const versions = [];
  for (const match of directoryPackagesXml.matchAll(/<PackageVersion\b([^>]*)>/g)) {
    const attributes = parseAttributes(match[1]);
    const include = attributes.get('Include') ?? attributes.get('Update');
    if (include === undefined) continue;
    versions.push({
      include,
      version: attributes.get('Version')
    });
  }
  return versions;
}

function normalizePackageId(packageId) {
  return packageId.toLowerCase();
}

function hasTrueProperty(projectXml, propertyName) {
  const propertyPattern = new RegExp(
    `<${propertyName}\\b[^>]*>([^<]*)</${propertyName}>`,
    'gi'
  );
  let effectiveValue;
  for (const match of projectXml.matchAll(propertyPattern)) {
    effectiveValue = match[1].trim().toLowerCase();
  }
  return effectiveValue === 'true';
}

function projectMetadata(projectPath, projectXml) {
  const packageReferences = parsePackageReferences(projectXml);
  const normalizedPackageIds = new Set(
    packageReferences.map((reference) => normalizePackageId(reference.include))
  );
  const frameworks = new Set(
    packageReferences
      .map((reference) => frameworkPackages.get(normalizePackageId(reference.include)))
      .filter((framework) => framework !== undefined)
  );
  return {
    path: projectPath,
    projectXml,
    packageReferences,
    frameworks: [...frameworks].sort(compareCodePoints),
    isTestProject:
      hasTrueProperty(projectXml, 'IsTestProject') ||
      normalizedPackageIds.has('microsoft.net.test.sdk')
  };
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
    return projectMetadata(projectPath, projectXml);
  });
}

function centralPackagePolicyErrors(modulePolicy, projects, repository) {
  const errors = [];
  for (const module of modulePolicy.modules) {
    if (module.packageVersionPolicy === 'project-inline-legacy') continue;

    const centralPath = `${module.path}/Directory.Packages.props`;
    const centralXml = repository.readText(centralPath);
    if (centralXml === undefined) {
      errors.push(
        `${module.path}: ${module.packageVersionPolicy} requires ${centralPath}`
      );
      continue;
    }

    if (!hasTrueProperty(centralXml, 'ManagePackageVersionsCentrally')) {
      errors.push(
        `${module.path}: ${module.packageVersionPolicy} requires ` +
        'ManagePackageVersionsCentrally=true'
      );
    }

    const packageVersions = parsePackageVersions(centralXml);
    if (packageVersions.length === 0) {
      errors.push(
        `${module.path}: ${module.packageVersionPolicy} requires central PackageVersion declarations`
      );
    }
    for (const packageVersion of packageVersions) {
      if (packageVersion.version === undefined || packageVersion.version === '') {
        errors.push(
          `${centralPath}: PackageVersion ${packageVersion.include} requires a Version`
        );
      }
    }
    const centrallyVersionedIds = new Set(
      packageVersions.map((packageVersion) => normalizePackageId(packageVersion.include))
    );
    const moduleProjects = projects.filter((project) =>
      isAtOrBelow(project.path, module.path)
    );
    for (const project of moduleProjects) {
      for (const reference of project.packageReferences) {
        if (
          reference.version === undefined &&
          !centrallyVersionedIds.has(normalizePackageId(reference.include))
        ) {
          errors.push(
            `${project.path}: ${module.packageVersionPolicy} has no central ` +
            `PackageVersion for ${reference.include}`
          );
        }
      }
    }

    const buildPropsPath = `${module.path}/Directory.Build.props`;
    const buildPropsXml = repository.readText(buildPropsPath) ?? '';
    const restoresWithLockFile = hasTrueProperty(
      buildPropsXml,
      'RestorePackagesWithLockFile'
    );
    if (module.packageVersionPolicy === 'module-central-locked') {
      if (!restoresWithLockFile) {
        errors.push(
          `${module.path}: module-central-locked requires ` +
          'RestorePackagesWithLockFile=true'
        );
      }
      for (const project of moduleProjects) {
        const lockPath = path.posix.join(
          path.posix.dirname(project.path),
          'packages.lock.json'
        );
        if (!repository.exists(lockPath)) {
          errors.push(
            `${project.path}: module-central-locked requires packages.lock.json`
          );
        }
      }
    }
  }
  return errors;
}

function projectPolicyErrors(modulePolicy, projects, repository = repositoryView) {
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
  errors.push(...centralPackagePolicyErrors(modulePolicy, projects, repository));
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

test('test-project identity survives removal of its required framework reference', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projectPath =
    'apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj';
  const projectXml = readFileSync(path.join(repoRoot, projectPath), 'utf8');
  const withoutFrameworkXml = projectXml.replace(
    /^\s*<PackageReference Include="MSTest\.TestFramework" \/>\r?\n/m,
    ''
  );
  assert.notEqual(
    withoutFrameworkXml,
    projectXml,
    'mutation must remove the real MSTest framework reference'
  );

  const withoutFramework = projectMetadata(projectPath, withoutFrameworkXml);
  assert.equal(
    withoutFramework.isTestProject,
    true,
    'IsTestProject or Microsoft.NET.Test.Sdk must identify the project independently'
  );
  assert.match(
    projectPolicyErrors(modulePolicy, [withoutFramework]).join('\n'),
    /AirQMonitorTests\.csproj: expected MSTest .*found no supported test framework/
  );
});

test('NuGet framework package IDs are matched case-insensitively', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projectPath =
    'apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj';
  const projectXml = readFileSync(path.join(repoRoot, projectPath), 'utf8');

  const caseVariantXml = projectXml.replace(
    'Include="MSTest.TestFramework"',
    'Include="mStEsT.tEsTfRaMeWoRk"'
  );
  assert.notEqual(caseVariantXml, projectXml, 'mutation must change the package ID casing');
  const caseVariant = projectMetadata(projectPath, caseVariantXml);
  assert.deepEqual(caseVariant.frameworks, ['MSTest']);
  assert.deepEqual(projectPolicyErrors(modulePolicy, [caseVariant]), []);

  const mixedFrameworkXml = projectXml.replace(
    '<PackageReference Include="MSTest.TestFramework" />',
    '<PackageReference Include="MSTest.TestFramework" />\n' +
      '    <PackageReference Include="XUNIT" />'
  );
  assert.notEqual(
    mixedFrameworkXml,
    projectXml,
    'mutation must add the case-variant forbidden framework'
  );
  const mixedFrameworks = projectMetadata(projectPath, mixedFrameworkXml);
  assert.match(
    projectPolicyErrors(modulePolicy, [mixedFrameworks]).join('\n'),
    /AirQMonitorTests\.csproj: expected MSTest .*found MSTest, xUnit/
  );
});

test('module-central requires effective central package management', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projects = solutionProjects();
  const centralPath = 'apps/monitors/Directory.Packages.props';
  const centralXml = repositoryView.readText(centralPath);
  assert.ok(centralXml, `${centralPath} must exist in the real repository`);

  const withoutCentralManagement = centralXml.replace(
    /^\s*<ManagePackageVersionsCentrally>true<\/ManagePackageVersionsCentrally>\r?\n/m,
    ''
  );
  assert.notEqual(
    withoutCentralManagement,
    centralXml,
    'mutation must remove ManagePackageVersionsCentrally'
  );
  const missingManagementView = withRepositoryMutations({
    text: new Map([[centralPath, withoutCentralManagement]])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, missingManagementView).join('\n'),
    /apps\/monitors: module-central requires ManagePackageVersionsCentrally=true/
  );

  const centralManagementDisabled = centralXml.replace(
    '</PropertyGroup>',
    '    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>\n' +
      '  </PropertyGroup>'
  );
  assert.notEqual(
    centralManagementDisabled,
    centralXml,
    'mutation must override central package management to false'
  );
  const disabledManagementView = withRepositoryMutations({
    text: new Map([[centralPath, centralManagementDisabled]])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, disabledManagementView).join('\n'),
    /apps\/monitors: module-central requires ManagePackageVersionsCentrally=true/
  );
});

test('module-central requires a central declaration for every package reference', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projects = solutionProjects();
  const centralPath = 'apps/monitors/Directory.Packages.props';
  const centralXml = repositoryView.readText(centralPath);
  assert.ok(centralXml, `${centralPath} must exist in the real repository`);
  const withoutTestSdkVersion = centralXml.replace(
    /^\s*<PackageVersion Include="Microsoft\.NET\.Test\.Sdk"[^>]*\/>\r?\n/gm,
    ''
  );
  assert.notEqual(
    withoutTestSdkVersion,
    centralXml,
    'mutation must remove every central Microsoft.NET.Test.Sdk declaration'
  );
  const missingDeclarationView = withRepositoryMutations({
    text: new Map([[centralPath, withoutTestSdkVersion]])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, missingDeclarationView).join('\n'),
    /AirQMonitorTests\.csproj: module-central has no central PackageVersion for Microsoft\.NET\.Test\.Sdk/
  );
});

test('module-central-locked requires effective lock-file restore', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projects = solutionProjects();
  const buildPropsPath = 'libs/rvt-monitor-common/Directory.Build.props';
  const buildPropsXml = repositoryView.readText(buildPropsPath);
  assert.ok(buildPropsXml, `${buildPropsPath} must exist in the real repository`);

  const withoutLockRestore = buildPropsXml.replace(
    /^\s*<RestorePackagesWithLockFile>true<\/RestorePackagesWithLockFile>\r?\n/m,
    ''
  );
  assert.notEqual(
    withoutLockRestore,
    buildPropsXml,
    'mutation must remove RestorePackagesWithLockFile'
  );
  const missingLockRestoreView = withRepositoryMutations({
    text: new Map([[buildPropsPath, withoutLockRestore]])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, missingLockRestoreView).join('\n'),
    /libs\/rvt-monitor-common: module-central-locked requires RestorePackagesWithLockFile=true/
  );

  const lockRestoreDisabled = buildPropsXml.replace(
    '</PropertyGroup>',
    '    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>\n' +
      '  </PropertyGroup>'
  );
  assert.notEqual(
    lockRestoreDisabled,
    buildPropsXml,
    'mutation must override lock-file restore to false'
  );
  const disabledLockRestoreView = withRepositoryMutations({
    text: new Map([[buildPropsPath, lockRestoreDisabled]])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, disabledLockRestoreView).join('\n'),
    /libs\/rvt-monitor-common: module-central-locked requires RestorePackagesWithLockFile=true/
  );
});

test('module-central-locked requires a lock file for every project', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projects = solutionProjects();
  const projectPath =
    'libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/' +
    'Rvt.Communication.Abstractions.csproj';
  const lockPath =
    'libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/packages.lock.json';
  assert.ok(repositoryView.exists(lockPath), `${lockPath} must exist in the real repository`);
  const missingLockFileView = withRepositoryMutations({
    missing: new Set([lockPath])
  });
  assert.match(
    projectPolicyErrors(modulePolicy, projects, missingLockFileView).join('\n'),
    new RegExp(
      `${projectPath.replaceAll('.', '\\.').replaceAll('/', '\\/')}: ` +
      'module-central-locked requires packages\\.lock\\.json'
    )
  );
});

test('module policy rejects an unauthorized extra module boundary', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const extraModule = structuredClone(modulePolicy);
  extraModule.modules.push({
    path: 'tools/temporary',
    testFramework: 'xUnit',
    packageVersionPolicy: 'project-inline-legacy'
  });

  assert.throws(
    () => validateModulePolicy(extraModule),
    /module policy must match the exact governed module and override shape/
  );
});

test('module policy rejects an unauthorized extra framework override', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const extraOverride = structuredClone(modulePolicy);
  extraOverride.modules[1].testFrameworkOverrides = [
    {
      path: 'apps/portal/temporary-tests',
      testFramework: 'MSTest'
    }
  ];

  assert.throws(
    () => validateModulePolicy(extraOverride),
    /module policy must match the exact governed module and override shape/
  );
});
