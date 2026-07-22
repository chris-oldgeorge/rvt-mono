// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { expect, test } from '@playwright/test';

test('anonymous user can see the SPA login shell', async ({ page }) => {
  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: { isAuthenticated: false, user: null },
      status: 200
    });
  });

  await page.goto('/login');

  await expect(page.getByRole('heading', { name: /please sign in/i })).toBeVisible();
  await expect(page.getByLabel(/email address/i)).toBeVisible();
  await expect(page.getByLabel(/password/i)).toBeVisible();
});

test('RVT admin can see admin navigation and companies', async ({ page }) => {
  await mockSignedInShell(page, ['RVTAdmin']);
  await page.route('**/api/companies?**', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        results: [
          {
            id: 'company-id',
            companyName: 'RVT Group',
            userCount: 2,
            sites: '1',
            contracts: '1'
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'companyName',
        sortDir: 'Ascending'
      },
      status: 200
    });
  });
  await page.route('**/api/companies/company-id', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        item: {
          id: 'company-id',
          companyName: 'RVT Group',
          userCount: 2,
          siteCount: 1,
          contractCount: 1,
          sites: 'Athens Plant',
          contracts: 'Monitoring'
        }
      },
      status: 200
    });
  });
  await page.route('**/api/lookups/**', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: { kind: 'companies', query: 'rvt', take: 8, results: ['RVT Group'] },
      status: 200
    });
  });

  await page.goto('/companies');

  await expect(page.getByRole('button', { name: /companies/i })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: /^companies$/i })).toBeVisible();
  await expect(page.getByText('RVT Group')).toBeVisible();
  await page.getByRole('button', { name: /view company/i }).click();
  await expect(page.getByRole('heading', { name: /^RVT Group$/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /manage users/i })).toBeVisible();
});

test('RVT admin can see users and user details', async ({ page }) => {
  await mockSignedInShell(page, ['RVTAdmin']);
  await page.route('**/api/users?**', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        results: [
          {
            id: 'company-user-id',
            companyId: 'company-id',
            companyName: 'RVT Group',
            isDisabled: false,
            name: 'Company User',
            email: 'company.user@rvt.test',
            phoneNumber: '07123456789',
            companyRole: 'Site contact',
            role: 'CompanyUser',
            siteCount: 1,
            emailConfirmed: true,
            canView: true,
            canEdit: true,
            canDisable: true,
            canEnable: false,
            canDelete: true,
            canSendConfirmation: false,
            canSendPasswordReset: true,
            canManageNotificationSettings: true
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'email',
        sortDir: 'Ascending',
        companyId: null,
        companyName: null
      },
      status: 200
    });
  });
  await page.route('**/api/users/company-user-id', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        item: {
          id: 'company-user-id',
          companyId: 'company-id',
          companyName: 'RVT Group',
          isDisabled: false,
          name: 'Company User',
          email: 'company.user@rvt.test',
          phoneNumber: '07123456789',
          companyRole: 'Site contact',
          role: 'CompanyUser',
          siteCount: 1,
          emailConfirmed: true,
          canView: true,
          canEdit: true,
          canDisable: true,
          canEnable: false,
          canDelete: true,
          canSendConfirmation: false,
          canSendPasswordReset: true,
          canManageNotificationSettings: true,
          availableRoles: [],
          companies: []
        }
      },
      status: 200
    });
  });

  await page.goto('/users');

  await expect(page.getByRole('button', { name: /users/i })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: /^users$/i })).toBeVisible();
  await expect(page.getByText('company.user@rvt.test')).toBeVisible();
  await page.getByRole('button', { name: /view user/i }).click();
  await expect(page.getByRole('heading', { name: /company\.user@rvt\.test/i })).toBeVisible();
  await expect(page.getByText('CompanyUser')).toBeVisible();
});

test('installer cannot reach direct admin companies route', async ({ page }) => {
  let companyRequests = 0;
  await mockSignedInShell(page, ['RVTInstaller']);
  await page.route('**/api/companies?**', async (route) => {
    companyRequests += 1;
    await route.fulfill({ status: 403, body: '' });
  });

  await page.goto('/companies');

  await expect(page.getByRole('heading', { name: /access denied/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /companies/i })).toHaveCount(0);
  expect(companyRequests).toBe(0);
});

async function mockSignedInShell(page: import('@playwright/test').Page, roles: string[]) {
  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        isAuthenticated: true,
        user: {
          id: 'user-id',
          email: 'user@rvt.test',
          name: 'RVT User',
          roles
        }
      },
      status: 200
    });
  });
  await page.route('**/api/health', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: { status: 'Healthy', framework: 'Testing', serverTimeUtc: new Date(0).toISOString() },
      status: 200
    });
  });
  await page.route('**/api/auth/profile', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        id: 'user-id',
        email: 'user@rvt.test',
        name: 'RVT User',
        role: roles[0] ?? null,
        companyRole: null,
        companyName: null
      },
      status: 200
    });
  });
}
