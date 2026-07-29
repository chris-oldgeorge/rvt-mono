// Starts Vite for Visual Studio when the repository is opened through a macOS
// shared folder. Native npm packages must live on the Windows filesystem;
// reusing the macOS node_modules tree fails for packages such as Rollup.

import { createHash } from 'node:crypto';
import console from 'node:console';
import { copyFileSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import process from 'node:process';
import { clearInterval, setInterval } from 'node:timers';
import { fileURLToPath } from 'node:url';
import { spawn, spawnSync } from 'node:child_process';

const clientRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const packageJsonPath = join(clientRoot, 'package.json');
const packageLockPath = join(clientRoot, 'package-lock.json');
const viteConfigPath = join(clientRoot, 'vite.config.ts');
const cacheRoot = process.env.LOCALAPPDATA;
const commandProcessor = process.env.ComSpec ?? 'cmd.exe';

if (process.platform !== 'win32') {
  throw new Error('The Visual Studio SPA launcher is intended for Windows.');
}

if (!cacheRoot) {
  throw new Error('LOCALAPPDATA is not defined; the SPA dependency cache cannot be created.');
}

const lockfileHash = createHash('sha256').update(readFileSync(packageLockPath)).digest('hex').slice(0, 16);
const dependencyCacheBase = join(cacheRoot, 'RvtPortal', 'spa-dependencies');
const dependencyRoot = join(dependencyCacheBase, lockfileHash);
const readyMarkerPath = join(dependencyRoot, '.rvt-ready');

function installDependencies() {
  if (existsSync(readyMarkerPath)) {
    return;
  }

  mkdirSync(dependencyCacheBase, { recursive: true });

  if (existsSync(dependencyRoot)) {
    if (existsSync(readyMarkerPath)) {
      return;
    }

    throw new Error(
      `The Windows SPA dependency cache is incomplete. Delete ${dependencyRoot} and start the project again.`,
    );
  }

  const temporaryRoot = `${dependencyRoot}-${process.pid}-${Date.now()}`;
  mkdirSync(temporaryRoot, { recursive: true });

  try {
    copyFileSync(packageJsonPath, join(temporaryRoot, 'package.json'));
    copyFileSync(packageLockPath, join(temporaryRoot, 'package-lock.json'));

    console.log(`Restoring Windows SPA dependencies in ${dependencyRoot}...`);
    const npmResult = spawnSync(commandProcessor, ['/d', '/s', '/c', 'npm.cmd ci --ignore-scripts'], {
      cwd: temporaryRoot,
      stdio: 'inherit',
      windowsHide: true,
    });

    if (npmResult.error) {
      throw npmResult.error;
    }

    if (npmResult.status !== 0) {
      throw new Error(`npm ci failed with exit code ${npmResult.status ?? 'unknown'}.`);
    }

    writeFileSync(join(temporaryRoot, '.rvt-ready'), `${lockfileHash}\n`);

    try {
      renameSync(temporaryRoot, dependencyRoot);
    } catch (error) {
      // A second Visual Studio process may have completed the same immutable
      // lockfile cache while this process was restoring it.
      if (!existsSync(readyMarkerPath)) {
        throw error;
      }
    }
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true });
  }
}

installDependencies();

const viteConfigHash = createHash('sha256').update(readFileSync(viteConfigPath)).digest('hex').slice(0, 16);
const cachedViteConfigPath = join(dependencyRoot, `vite.config.${viteConfigHash}.ts`);

if (!existsSync(cachedViteConfigPath)) {
  copyFileSync(viteConfigPath, cachedViteConfigPath);
}

const sourceHash = createHash('sha256').update(clientRoot.toLowerCase()).digest('hex').slice(0, 16);
const workspaceRoot = join(dependencyRoot, 'workspaces', sourceHash);
const robocopyArguments = [
  clientRoot,
  workspaceRoot,
  '/MIR',
  '/XD',
  'node_modules',
  'dist',
  'coverage',
  '.git',
  '/NFL',
  '/NDL',
  '/NJH',
  '/NJS',
  '/NP',
];

function assertSuccessfulMirror(result) {
  if (result.error) {
    throw result.error;
  }

  // Robocopy uses 0-7 for successful copies and differences; 8+ is failure.
  if (result.status === null || result.status >= 8) {
    throw new Error(`robocopy failed with exit code ${result.status ?? 'unknown'}.`);
  }
}

mkdirSync(dirname(workspaceRoot), { recursive: true });
assertSuccessfulMirror(
  spawnSync('robocopy.exe', robocopyArguments, {
    stdio: 'inherit',
    windowsHide: true,
  }),
);

let mirrorInProgress = false;
let mirrorErrorReported = false;
const mirrorInterval = setInterval(() => {
  if (mirrorInProgress) {
    return;
  }

  mirrorInProgress = true;
  const mirror = spawn('robocopy.exe', robocopyArguments, {
    stdio: 'ignore',
    windowsHide: true,
  });

  mirror.once('error', (error) => {
    mirrorInProgress = false;
    if (!mirrorErrorReported) {
      mirrorErrorReported = true;
      console.error('The Windows SPA source mirror failed.', error);
    }
  });
  mirror.once('exit', (code) => {
    mirrorInProgress = false;
    if ((code === null || code >= 8) && !mirrorErrorReported) {
      mirrorErrorReported = true;
      console.error(`The Windows SPA source mirror exited with code ${code}.`);
    }
  });
}, 1000);

const viteEntryPoint = resolve(dependencyRoot, 'node_modules/vite/bin/vite.js');
const vite = spawn(
  process.execPath,
  [
    viteEntryPoint,
    workspaceRoot,
    '--config',
    cachedViteConfigPath,
    '--host',
    '127.0.0.1',
    '--port',
    '5173',
    '--strictPort',
  ],
  {
    cwd: workspaceRoot,
    stdio: 'inherit',
    windowsHide: true,
  },
);

const exitCode = await new Promise((resolveExitCode, reject) => {
  vite.once('error', reject);
  vite.once('exit', (code) => resolveExitCode(code ?? 1));
});

clearInterval(mirrorInterval);
process.exitCode = exitCode;
