// File summary: Shared helper that sanitizes anchor hrefs so unsafe schemes (javascript:, data:)
// cannot be rendered from server- or admin-authored URLs.

// Function summary: Returns a safe href (relative path or http/https URL) or null for unsafe schemes.
export function safeHref(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed) {
    return null;
  }

  // Reject protocol-relative ("//host") URLs; require an explicit scheme or a root-relative path.
  if (trimmed.startsWith('//')) {
    return null;
  }

  // Same-origin root-relative paths are safe.
  if (trimmed.startsWith('/')) {
    return trimmed;
  }

  let url: URL;
  try {
    url = new URL(trimmed, globalThis.location.origin);
  } catch {
    return null;
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') {
    return null;
  }

  return url.toString();
}
