// File summary: Exercises the released Help Admin journey and client-side role boundary.
// Major updates:
// - 2026-07-28 Added create, publish, preview, edit, delete, and Company User denial coverage.

import { expect, test } from '@playwright/test';

test('Help Admin supports the approved content lifecycle', async ({ page }) => {
  let roles = ['RVTAdmin'];
  let article: HelpArticleFixture | null = null;
  await mockShell(page, () => roles);
  await page.route('**/api/help/admin**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const articleMatch = url.pathname.match(/^\/api\/help\/admin\/articles\/([^/]+)$/);
    const publicationMatch = url.pathname.match(/^\/api\/help\/admin\/articles\/([^/]+)\/publication$/);

    if (request.method() === 'POST' && url.pathname === '/api/help/admin/articles') {
      const mutation = request.postDataJSON();
      article = articleFromMutation(mutation);
      await route.fulfill({
        contentType: 'application/json',
        json: { item: article },
        status: 201,
      });
      return;
    }

    if (request.method() === 'PUT' && articleMatch && article) {
      const mutation = request.postDataJSON();
      article = articleFromMutation(mutation, article);
      await route.fulfill({
        contentType: 'application/json',
        json: { item: article },
        status: 200,
      });
      return;
    }

    if (request.method() === 'POST' && publicationMatch && article) {
      const mutation = request.postDataJSON();
      article = { ...article, isPublished: mutation.isPublished };
      await route.fulfill({
        contentType: 'application/json',
        json: { item: article },
        status: 200,
      });
      return;
    }

    if (request.method() === 'DELETE' && articleMatch) {
      article = null;
      await route.fulfill({
        contentType: 'application/json',
        json: { id: 'help-e2e-id', message: 'Help article removed.' },
        status: 200,
      });
      return;
    }

    if (request.method() === 'GET' && articleMatch && article) {
      await route.fulfill({
        contentType: 'application/json',
        json: { item: article },
        status: 200,
      });
      return;
    }

    await route.fulfill({
      contentType: 'application/json',
      json: {
        searchText: url.searchParams.get('searchText') ?? '',
        status: url.searchParams.get('status') ?? 'All',
        contentType: url.searchParams.get('contentType') ?? 'All',
        sections: [],
        articles: article ? [article] : [],
      },
      status: 200,
    });
  });
  await page.route('**/api/help/articles/**', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: { item: article },
      status: article ? 200 : 404,
    });
  });
  await page.route('**/api/help', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        searchText: '',
        sections: article?.isPublished
          ? [
              {
                id: 'general-id',
                title: article.sectionTitle,
                slug: article.sectionSlug,
                sortOrder: 1,
                articles: [article],
              },
            ]
          : [],
      },
      status: 200,
    });
  });

  await page.goto('/help');
  await expect(
    page.getByRole('heading', {
      level: 1,
      name: 'Help',
      exact: true,
    }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Help/FAQ' }).click();
  await expect(page).toHaveURL(/\/admin\/help$/);
  await page.getByLabel('Title').fill('New FAQ');
  await page.getByLabel('Body').fill('Published Help body');
  await page.getByRole('button', { name: 'Add Asset' }).click();
  await page.getByPlaceholder('Title').fill('External guide');
  await page.getByPlaceholder('URL').fill('https://docs.rvt.test/guide');
  await page.getByRole('button', { name: 'Create FAQ' }).click();
  await expect(page.getByText('Help article created.')).toBeVisible();

  await page.getByRole('button', { name: 'Publish New FAQ' }).click();
  await expect(page.getByText('Help article published.')).toBeVisible();
  await page.getByRole('button', { name: 'Preview New FAQ' }).click();
  await expect(page).toHaveURL(/\/help\/new-faq$/);
  await expect(page.getByRole('heading', { name: 'New FAQ' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'External guide' })).toHaveAttribute(
    'href',
    'https://docs.rvt.test/guide',
  );

  await page.getByRole('button', { name: 'Help/FAQ' }).click();
  await page.getByRole('button', { name: 'Edit New FAQ' }).click();
  await page.getByLabel('Title').fill('Updated FAQ');
  await page.getByPlaceholder('Title').fill('Updated guide');
  await page.getByRole('button', { name: 'Save FAQ' }).click();
  await expect(page.getByText('Help article updated.')).toBeVisible();
  await page.getByRole('button', { name: 'Delete Updated FAQ' }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByText('No help articles match the current filters.')).toBeVisible();

  roles = ['CompanyUser'];
  await page.goto('/admin/help');
  await expect(page.getByRole('heading', { name: /access denied/i })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Help/FAQ' })).toHaveCount(0);
});

type HelpArticleFixture = {
  id: string;
  title: string;
  slug: string;
  summary: string | null;
  body: string;
  contentType: string;
  sectionTitle: string;
  sectionSlug: string;
  sectionSortOrder: number;
  sortOrder: number;
  isPublished: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  assets: Array<{
    id: string;
    title: string;
    assetType: string;
    url: string;
    internalPath: string | null;
    sortOrder: number;
  }>;
};

function articleFromMutation(mutation: Record<string, unknown>, existing?: HelpArticleFixture): HelpArticleFixture {
  const assets = mutation.assets as Array<Record<string, unknown>>;
  return {
    id: existing?.id ?? 'help-e2e-id',
    title: mutation.title as string,
    slug: mutation.slug as string,
    summary: mutation.summary as string | null,
    body: mutation.body as string,
    contentType: mutation.contentType as string,
    sectionTitle: mutation.sectionTitle as string,
    sectionSlug: mutation.sectionSlug as string,
    sectionSortOrder: mutation.sectionSortOrder as number,
    sortOrder: mutation.sortOrder as number,
    isPublished: mutation.isPublished as boolean,
    createdAtUtc: existing?.createdAtUtc ?? '2026-07-28T08:00:00Z',
    updatedAtUtc: '2026-07-28T09:00:00Z',
    assets: assets.map((asset, index) => ({
      id: (asset.id as string | undefined) ?? `help-e2e-asset-${index}`,
      title: asset.title as string,
      assetType: asset.assetType as string,
      url: asset.url as string,
      internalPath: null,
      sortOrder: asset.sortOrder as number,
    })),
  };
}

async function mockShell(page: import('@playwright/test').Page, roles: () => string[]) {
  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        isAuthenticated: true,
        user: {
          id: 'help-user-id',
          email: 'help.user@rvt.test',
          name: 'Help User',
          roles: roles(),
        },
      },
      status: 200,
    });
  });
  await page.route('**/api/health', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        status: 'Healthy',
        framework: 'Testing',
        serverTimeUtc: new Date(0).toISOString(),
      },
      status: 200,
    });
  });
  await page.route('**/api/auth/profile', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        id: 'help-user-id',
        email: 'help.user@rvt.test',
        name: 'Help User',
        role: roles()[0] ?? null,
        companyRole: null,
        companyName: null,
      },
      status: 200,
    });
  });
  await page.route('**/api/dashboard/summary', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        activeSites: 0,
        totalMonitors: 0,
        monitorsOnline: 0,
        monitorsOffline: 0,
        openAlerts: 0,
        siteStatus: [],
        recentActivity: [],
      },
      status: 200,
    });
  });
}
