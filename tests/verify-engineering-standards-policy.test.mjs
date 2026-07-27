import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  diagnosticKey,
  validateBaseline,
  validateExceptions
} from '../scripts/engineering-standards/model.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dotnetCommand = process.env.RVT_STANDARDS_POLICY_DOTNET ?? 'dotnet';
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
const projectDiscoveryIgnoredDirectories = new Set([
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

function toPosixPath(candidate) {
  return candidate.replaceAll('\\', '/');
}

function assertExactPolicyPath(candidate, label) {
  assert.equal(typeof candidate, 'string', `${label} must be a string`);
  assert.notEqual(candidate, '', `${label} must not be empty`);
  assert.equal(candidate, toPosixPath(candidate), `${label} must use POSIX separators`);
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

function runDotnet(arguments_, root) {
  const result = spawnSync(dotnetCommand, arguments_, {
    cwd: root,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
    windowsHide: true
  });
  if (result.error !== undefined) {
    throw new Error(
      `Unable to run ${dotnetCommand} ${arguments_.join(' ')}: ${result.error.message}`
    );
  }
  if (result.status !== 0) {
    throw new Error(
      `${dotnetCommand} ${arguments_.join(' ')} exited ${result.status}\n` +
      `${result.stdout}${result.stderr}`
    );
  }
  return result.stdout;
}

function parseMsBuildJson(stdout, projectPath) {
  try {
    return JSON.parse(stdout);
  } catch (error) {
    throw new Error(`MSBuild returned malformed JSON for ${projectPath}: ${error.message}`);
  }
}

function optionalMetadata(value) {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : undefined;
}

function trueProperty(value) {
  return typeof value === 'string' && value.trim().toLowerCase() === 'true';
}

function normalizePackageId(packageId) {
  return packageId.toLowerCase();
}

function evaluateProjectMetadata(root, projectPath) {
  assertExactPolicyPath(projectPath, 'project path');
  const document = parseMsBuildJson(
    runDotnet([
      'msbuild',
      path.join(root, projectPath),
      '-nologo',
      '-getProperty:IsTestProject,ManagePackageVersionsCentrally,RestorePackagesWithLockFile',
      '-getItem:PackageReference,PackageVersion'
    ], root),
    projectPath
  );
  const properties = document.Properties ?? {};
  const packageReferences = (document.Items?.PackageReference ?? []).map((item) => ({
    include: item.Identity,
    version: optionalMetadata(item.VersionOverride) ?? optionalMetadata(item.Version)
  }));
  const packageVersions = (document.Items?.PackageVersion ?? []).map((item) => ({
    include: item.Identity,
    version: optionalMetadata(item.Version)
  }));
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
    packageReferences,
    packageVersions,
    frameworks: [...frameworks].sort(compareCodePoints),
    isTestProject:
      trueProperty(properties.IsTestProject) ||
      normalizedPackageIds.has('microsoft.net.test.sdk'),
    managePackageVersionsCentrally:
      trueProperty(properties.ManagePackageVersionsCentrally),
    restorePackagesWithLockFile:
      trueProperty(properties.RestorePackagesWithLockFile)
  };
}

function solutionProjectPaths(root) {
  const solutionPath = path.join(root, 'Rvt.Mono.slnx');
  const stdout = runDotnet(['sln', solutionPath, 'list'], root);
  const paths = stdout
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.toLowerCase().endsWith('.csproj'))
    .map((candidate) => {
      const platformPath = candidate.replaceAll('/', path.sep).replaceAll('\\', path.sep);
      return toPosixPath(
        path.relative(root, path.isAbsolute(platformPath)
          ? platformPath
          : path.resolve(root, platformPath))
      );
    })
    .sort(compareCodePoints);

  assert.ok(paths.length > 0, 'Rvt.Mono.slnx must reference projects');
  for (const projectPath of paths) {
    assertExactPolicyPath(projectPath, 'solution project path');
  }
  assert.equal(
    new Set(paths).size,
    paths.length,
    'Rvt.Mono.slnx must not reference a project more than once'
  );
  return paths;
}

function discoverGovernedProjectPaths(root, modulePolicy = expectedModulePolicy) {
  const projects = [];

  function visit(directory, relativeDirectory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      if (entry.isSymbolicLink()) continue;
      const relativePath = path.posix.join(relativeDirectory, entry.name);
      const absolutePath = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        if (!projectDiscoveryIgnoredDirectories.has(entry.name)) {
          visit(absolutePath, relativePath);
        }
      } else if (entry.isFile() && entry.name.toLowerCase().endsWith('.csproj')) {
        assertExactPolicyPath(relativePath, 'discovered project path');
        projects.push(relativePath);
      }
    }
  }

  for (const module of modulePolicy.modules) {
    const moduleRoot = path.join(root, module.path);
    if (existsSync(moduleRoot)) visit(moduleRoot, module.path);
  }
  return projects.sort(compareCodePoints);
}

function solutionProjects(root = repoRoot, modulePolicy = expectedModulePolicy) {
  const listedProjects = solutionProjectPaths(root);
  const discoveredProjects = discoverGovernedProjectPaths(root, modulePolicy);
  assert.deepEqual(
    listedProjects,
    discoveredProjects,
    'Rvt.Mono.slnx must contain every governed project exactly once'
  );
  return discoveredProjects.map((projectPath) =>
    evaluateProjectMetadata(root, projectPath)
  );
}

function projectPolicyErrors(modulePolicy, projects, root = repoRoot) {
  const errors = [];
  for (const module of modulePolicy.modules) {
    if (module.packageVersionPolicy === 'project-inline-legacy') continue;
    if (!projects.some((project) => isAtOrBelow(project.path, module.path))) continue;
    const centralPath = `${module.path}/Directory.Packages.props`;
    if (!existsSync(path.join(root, centralPath))) {
      errors.push(
        `${module.path}: ${module.packageVersionPolicy} requires ${centralPath}`
      );
    }
  }

  for (const project of projects) {
    const policy = longestMatchingPolicy(project.path, modulePolicy);
    if (policy === undefined) {
      errors.push(`${project.path}: no module policy matches this governed project`);
      continue;
    }

    if (project.isTestProject && (
      project.frameworks.length !== 1 ||
      project.frameworks[0] !== policy.testFramework
    )) {
      errors.push(
        `${project.path}: expected ${policy.testFramework} from ${policy.path}; ` +
        `found ${project.frameworks.join(', ') || 'no supported test framework'}`
      );
    }

    const inlineReferences = project.packageReferences
      .filter((reference) => reference.version !== undefined);
    const centralReferences = project.packageReferences
      .filter((reference) => reference.version === undefined);

    if (policy.packageVersionPolicy === 'project-inline-legacy') {
      if (project.managePackageVersionsCentrally) {
        errors.push(
          `${project.path}: project-inline-legacy forbids central package management`
        );
      }
      if (centralReferences.length > 0) {
        errors.push(
          `${project.path}: project-inline-legacy requires inline versions for ` +
          centralReferences.map((reference) => reference.include).join(', ')
        );
      }
      continue;
    }

    if (!project.managePackageVersionsCentrally) {
      errors.push(
        `${project.path}: ${policy.packageVersionPolicy} requires ` +
        'ManagePackageVersionsCentrally=true'
      );
    }
    if (inlineReferences.length > 0) {
      errors.push(
        `${project.path}: ${policy.packageVersionPolicy} forbids inline versions for ` +
        inlineReferences.map((reference) => reference.include).join(', ')
      );
    }
    if (project.packageVersions.length === 0) {
      errors.push(
        `${project.path}: ${policy.packageVersionPolicy} requires effective ` +
        'PackageVersion declarations'
      );
    }

    const centrallyVersionedIds = new Set();
    for (const packageVersion of project.packageVersions) {
      if (packageVersion.version === undefined) {
        errors.push(
          `${project.path}: PackageVersion ${packageVersion.include} requires a Version`
        );
      } else {
        centrallyVersionedIds.add(normalizePackageId(packageVersion.include));
      }
    }
    for (const reference of centralReferences) {
      if (!centrallyVersionedIds.has(normalizePackageId(reference.include))) {
        errors.push(
          `${project.path}: ${policy.packageVersionPolicy} has no effective ` +
          `PackageVersion for ${reference.include}`
        );
      }
    }

    if (policy.packageVersionPolicy === 'module-central-locked') {
      if (!project.restorePackagesWithLockFile) {
        errors.push(
          `${project.path}: module-central-locked requires ` +
          'RestorePackagesWithLockFile=true'
        );
      }
      const lockPath = path.posix.join(
        path.posix.dirname(project.path),
        'packages.lock.json'
      );
      if (!existsSync(path.join(root, lockPath))) {
        errors.push(
          `${project.path}: module-central-locked requires packages.lock.json`
        );
      }
    }
  }
  return errors;
}

function withTemporaryRepository(files, action) {
  const root = mkdtempSync(path.join(tmpdir(), 'rvt-standards-policy-'));
  try {
    for (const [relativePath, contents] of Object.entries(files)) {
      const absolutePath = path.join(root, relativePath);
      mkdirSync(path.dirname(absolutePath), { recursive: true });
      writeFileSync(absolutePath, contents, 'utf8');
    }
    return action(root);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

let cachedRealProjects;

function realProjects() {
  cachedRealProjects ??= solutionProjects();
  return cachedRealProjects;
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

test('real governed projects match solution, framework, and package policies', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  validateModulePolicy(modulePolicy);
  const projects = realProjects();
  assert.ok(
    projects.some((project) => project.isTestProject),
    'evaluated MSBuild metadata must identify test projects'
  );
  assert.deepEqual(projectPolicyErrors(modulePolicy, projects), []);
});

test('framework policy rejects an added xUnit package and a displaced override', () => {
  const modulePolicy = readJson('eng/standards/module-policy.json');
  const projects = realProjects();
  const mstestProject = projects.find(
    (project) =>
      project.path === 'apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj'
  );
  assert.ok(mstestProject, 'AirQMonitorTests must be present in governed metadata');
  const withAddedXunit = {
    ...mstestProject,
    packageReferences: [
      ...mstestProject.packageReferences,
      { include: 'XUNIT', version: undefined }
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
  assert.ok(reportingProject, 'ReportingMonitorTests must be present in governed metadata');
  assert.match(
    projectPolicyErrors(displacedOverride, [reportingProject]).join('\n'),
    /ReportingMonitorTests\.csproj: expected MSTest .*found xUnit/
  );
});

test('evaluated test identity survives a missing framework and matches IDs case-insensitively', () => {
  withTemporaryRepository({
    'apps/monitors/Directory.Packages.props': `
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageVersion Include="mStEsT.tEsTfRaMeWoRk" Version="4.0.2" />
    <PackageVersion Include="XUNIT" Version="2.9.3" />
  </ItemGroup>
</Project>
`,
    'apps/monitors/missing/Missing.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>
</Project>
`,
    'apps/monitors/case/Case.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="mStEsT.tEsTfRaMeWoRk" />
  </ItemGroup>
</Project>
`,
    'apps/monitors/mixed/Mixed.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="mStEsT.tEsTfRaMeWoRk" />
    <PackageReference Include="XUNIT" />
  </ItemGroup>
</Project>
`
  }, (root) => {
    const missing = evaluateProjectMetadata(root, 'apps/monitors/missing/Missing.csproj');
    assert.equal(missing.isTestProject, true);
    assert.match(
      projectPolicyErrors(expectedModulePolicy, [missing], root).join('\n'),
      /Missing\.csproj: expected MSTest .*found no supported test framework/
    );

    const caseVariant = evaluateProjectMetadata(root, 'apps/monitors/case/Case.csproj');
    assert.deepEqual(caseVariant.frameworks, ['MSTest']);
    assert.deepEqual(projectPolicyErrors(expectedModulePolicy, [caseVariant], root), []);

    const mixed = evaluateProjectMetadata(root, 'apps/monitors/mixed/Mixed.csproj');
    assert.match(
      projectPolicyErrors(expectedModulePolicy, [mixed], root).join('\n'),
      /Mixed\.csproj: expected MSTest .*found MSTest, xUnit/
    );
  });
});

test('MSBuild evaluation ignores XML comments and false conditions', () => {
  withTemporaryRepository({
    'apps/monitors/Directory.Packages.props': `
<Project>
  <PropertyGroup>
    <!-- <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally> -->
    <ManagePackageVersionsCentrally Condition="'$(EnableCentral)' == 'true'">
      true
    </ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- <PackageVersion Include="Commented.Package" Version="1.0.0" /> -->
    <PackageVersion
      Include="Conditional.Package"
      Version="1.0.0"
      Condition="'$(EnableConditionalPackage)' == 'true'"
    />
  </ItemGroup>
</Project>
`,
    'apps/monitors/probe/Probe.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <!-- <PackageReference Include="MSTest.TestFramework" Version="4.0.2" /> -->
    <PackageReference Include="Conditional.Package" />
  </ItemGroup>
</Project>
`,
    'apps/monitors/production/Production.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject Condition="'$(ClassifyAsTest)' == 'true'">true</IsTestProject>
  </PropertyGroup>
</Project>
`
  }, (root) => {
    const probe = evaluateProjectMetadata(root, 'apps/monitors/probe/Probe.csproj');
    assert.deepEqual(probe.frameworks, []);
    assert.equal(probe.managePackageVersionsCentrally, false);
    assert.deepEqual(probe.packageVersions, []);
    const errors = projectPolicyErrors(expectedModulePolicy, [probe], root).join('\n');
    assert.match(errors, /requires ManagePackageVersionsCentrally=true/);
    assert.match(errors, /has no effective PackageVersion for Conditional\.Package/);

    const production = evaluateProjectMetadata(
      root,
      'apps/monitors/production/Production.csproj'
    );
    assert.equal(production.isTestProject, false);
  });
});

test('MSBuild evaluation honors project lock overrides and commented restore properties', () => {
  withTemporaryRepository({
    'libs/rvt-monitor-common/Directory.Build.props': `
<Project>
  <PropertyGroup>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
`,
    'libs/rvt-monitor-common/Directory.Packages.props': `
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Example.Package" Version="1.0.0" />
  </ItemGroup>
</Project>
`,
    'libs/rvt-monitor-common/override/Override.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
`,
    'libs/rvt-monitor-common/override/packages.lock.json': '{}',
    'libs/rvt-monitor-common/commented/Directory.Build.props': `
<Project>
  <PropertyGroup>
    <!-- <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile> -->
  </PropertyGroup>
</Project>
`,
    'libs/rvt-monitor-common/commented/Commented.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`,
    'libs/rvt-monitor-common/commented/packages.lock.json': '{}'
  }, (root) => {
    for (const projectPath of [
      'libs/rvt-monitor-common/override/Override.csproj',
      'libs/rvt-monitor-common/commented/Commented.csproj'
    ]) {
      const project = evaluateProjectMetadata(root, projectPath);
      assert.equal(project.restorePackagesWithLockFile, false);
      assert.match(
        projectPolicyErrors(expectedModulePolicy, [project], root).join('\n'),
        /module-central-locked requires RestorePackagesWithLockFile=true/
      );
    }
  });
});

test('MSBuild evaluation recognizes nested inline PackageReference versions', () => {
  withTemporaryRepository({
    'apps/monitors/Directory.Packages.props': `
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Example.Package" Version="1.2.3" />
  </ItemGroup>
</Project>
`,
    'apps/monitors/probe/Probe.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Example.Package">
      <Version>1.2.3</Version>
    </PackageReference>
  </ItemGroup>
</Project>
`
  }, (root) => {
    const project = evaluateProjectMetadata(root, 'apps/monitors/probe/Probe.csproj');
    assert.equal(project.packageReferences[0].version, '1.2.3');
    assert.match(
      projectPolicyErrors(expectedModulePolicy, [project], root).join('\n'),
      /module-central forbids inline versions for Example\.Package/
    );
  });
});

test('module-central-locked requires a colocated lock file for every project', () => {
  withTemporaryRepository({
    'libs/rvt-monitor-common/Directory.Build.props': `
<Project>
  <PropertyGroup>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
`,
    'libs/rvt-monitor-common/Directory.Packages.props': `
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Example.Package" Version="1.0.0" />
  </ItemGroup>
</Project>
`,
    'libs/rvt-monitor-common/probe/Probe.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`
  }, (root) => {
    const project = evaluateProjectMetadata(
      root,
      'libs/rvt-monitor-common/probe/Probe.csproj'
    );
    assert.match(
      projectPolicyErrors(expectedModulePolicy, [project], root).join('\n'),
      /Probe\.csproj: module-central-locked requires packages\.lock\.json/
    );
  });
});

test('governed project discovery rejects a project omitted from the solution', () => {
  withTemporaryRepository({
    'Rvt.Mono.slnx': `
<Solution>
  <Project Path="apps/monitors/listed/Listed.csproj" />
</Solution>
`,
    'apps/monitors/listed/Listed.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`,
    'apps/monitors/omitted/Omitted.csproj': `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`
  }, (root) => {
    assert.throws(
      () => solutionProjects(root),
      /Rvt\.Mono\.slnx must contain every governed project exactly once/
    );
  });
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
