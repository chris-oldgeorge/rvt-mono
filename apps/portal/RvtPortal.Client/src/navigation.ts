// File summary: Provides internal SPA navigation helpers for preserving safe return routes.
// Major updates:
// - 2026-06-26 pending Added returnTo helpers so form Back buttons can return to their opening list/detail context.

const fallbackOrigin = 'https://rvt.local';
const returnToParameter = 'returnTo';

// Function summary: Captures the live internal route path and query string for return navigation.
export function currentRoutePath(locationPath: string) {
  const browserPath = typeof globalThis.location === 'object'
    ? `${globalThis.location.pathname}${globalThis.location.search}`
    : '';
  const url = new URL(browserPath || locationPath, fallbackOrigin);
  return `${url.pathname}${url.search}`;
}

// Function summary: Adds a safe internal return path to a target route.
export function withReturnTo(path: string, returnTo: string) {
  const safeReturnTo = normalizeReturnTo(returnTo);
  if (!safeReturnTo) {
    return path;
  }

  const url = new URL(path, fallbackOrigin);
  url.searchParams.set(returnToParameter, safeReturnTo);
  return `${url.pathname}${url.search}`;
}

// Function summary: Reads a safe internal return path from the current route or falls back.
export function returnToOr(locationPath: string, fallback: string) {
  const url = new URL(locationPath, fallbackOrigin);
  return normalizeReturnTo(url.searchParams.get(returnToParameter)) ?? fallback;
}

// Function summary: Normalizes return paths to internal SPA-only routes.
function normalizeReturnTo(value?: string | null) {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
    return null;
  }

  let url: URL;
  try {
    url = new URL(value, fallbackOrigin);
  } catch {
    return null;
  }

  if (url.origin !== fallbackOrigin || !url.pathname.startsWith('/')) {
    return null;
  }

  if (url.pathname.split('/').some((segment) => segment === '.' || segment === '..')) {
    return null;
  }

  return `${url.pathname}${url.search}`;
}
