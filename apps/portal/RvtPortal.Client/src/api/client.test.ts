// File summary: Defines the browser API client and typed request helpers for the React SPA.
// Major updates:
// - 2026-06-26 pending Covered AbortSignal propagation and schema-backed DTO facade guardrails.
// - 2026-06-26 pending Added OpenAPI generated-schema boundary guardrails.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ApiError, downloadFile, getHealth, queryCompanies } from './client';

const apiDirectory = dirname(fileURLToPath(import.meta.url));

describe('API client infrastructure', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('includes API correlation ids on problem responses', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(
      { title: 'Invalid request', detail: 'Sort field is not supported.', correlationId: 'problem-id' },
      400,
      { 'X-Correlation-Id': 'header-id' }
    )));

    await expect(getHealth()).rejects.toMatchObject({
      name: 'ApiError',
      status: 400,
      message: 'Sort field is not supported.',
      correlationId: 'header-id'
    } satisfies Partial<ApiError>);
  });

  it('downloads files without navigating away from the SPA', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('RVT diagnostics', {
      headers: {
        'Content-Disposition': 'attachment; filename="rvt-diagnostics.txt"',
        'Content-Type': 'text/plain',
        'X-Correlation-Id': 'download-id'
      },
      status: 200
    })));

    const file = await downloadFile('/api/health/diagnostics/download');

    expect(file.fileName).toBe('rvt-diagnostics.txt');
    expect(file.contentType).toBe('text/plain');
    expect(file.correlationId).toBe('download-id');
    expect(await file.blob.text()).toBe('RVT diagnostics');
  });

  it('blocks client requests to absolute or non-api URLs', async () => {
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);

    await expect(downloadFile('https://attacker.example/api/health/diagnostics/download')).rejects.toThrow(/unsafe API request URL/i);
    await expect(downloadFile('/content/report.csv')).rejects.toThrow(/unsafe API request URL/i);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('passes abort signals through generated API helper calls', async () => {
    const controller = new AbortController();
    let observedSignal: AbortSignal | undefined;
    vi.stubGlobal('fetch', vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      observedSignal = init?.signal ?? undefined;
      return jsonResponse({
        results: [],
        total: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'companyName',
        sortDir: 'Ascending'
      });
    }));

    await queryCompanies(new URLSearchParams(), { signal: controller.signal });

    expect(observedSignal).toBe(controller.signal);
  });

  it('uses the generated OpenAPI schema facade for request and response contracts', () => {
    const clientSource = readFileSync(resolve(apiDirectory, 'client.ts'), 'utf8');

    expect(existsSync(resolve(apiDirectory, 'openApiClient.ts'))).toBe(true);
    expect(clientSource).toContain("from './openApiClient'");
    expect(clientSource).not.toContain("from '../dtos'");
  });

  it('keeps SPA DTO exports backed by the generated OpenAPI schema', () => {
    const dtoSource = readFileSync(resolve(apiDirectory, '../dtos.ts'), 'utf8');

    expect(dtoSource).toContain("import type { components } from './api/schema'");
    expect(dtoSource).toContain("export type CompanyListItem = ApiSchema<'CompanyListItem'>");
    expect(dtoSource).toContain("ApiSchema<'ReportRuleMutationRequest'>");
    expect(dtoSource).not.toContain('export type CompanyListItem = {');
    expect(dtoSource).not.toContain('export type ReportRuleMutationRequest = {');
  });
});

// Function summary: Handles the json response workflow for this module.
function jsonResponse(body: unknown, status: number, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    headers: {
      'Content-Type': 'application/problem+json',
      ...headers
    },
    status
  });
}
