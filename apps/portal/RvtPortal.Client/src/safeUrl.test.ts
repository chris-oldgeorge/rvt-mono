// File summary: Unit tests for the safeHref anchor sanitizer.

import { describe, expect, it } from 'vitest';
import { safeHref } from './safeUrl';

describe('safeHref', () => {
  it('allows http and https URLs', () => {
    expect(safeHref('https://example.com/file.pdf')).toBe('https://example.com/file.pdf');
    expect(safeHref('http://example.com/')).toBe('http://example.com/');
  });

  it('allows same-origin relative paths', () => {
    expect(safeHref('/api/monitors/123/picture')).toBe('/api/monitors/123/picture');
  });

  it('rejects javascript and data scheme URLs', () => {
    expect(safeHref('javascript:alert(document.cookie)')).toBeNull();
    expect(safeHref("data:text/html,<script>alert(1)</script>")).toBeNull();
    expect(safeHref('vbscript:msgbox(1)')).toBeNull();
  });

  it('rejects protocol-relative URLs', () => {
    expect(safeHref('//evil.example/x')).toBeNull();
  });

  it('rejects empty and nullish values', () => {
    expect(safeHref('')).toBeNull();
    expect(safeHref('   ')).toBeNull();
    expect(safeHref(null)).toBeNull();
    expect(safeHref(undefined)).toBeNull();
  });
});
