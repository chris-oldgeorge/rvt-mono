// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.RVT_PORTAL_E2E_BASE_URL ?? 'http://127.0.0.1:5173';

export default defineConfig({
  testDir: './tests/e2e',
  testIgnore: ['**/._*'],
  testMatch: ['**/*.spec.ts'],
  timeout: 30_000,
  expect: {
    timeout: 5_000
  },
  use: {
    baseURL,
    trace: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: process.env.RVT_PORTAL_E2E_SKIP_WEBSERVER
    ? undefined
    : {
        command: 'npm run dev:vs',
        url: 'http://127.0.0.1:5173',
        reuseExistingServer: true,
        timeout: 120_000
      }
});
