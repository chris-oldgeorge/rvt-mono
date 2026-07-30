// File summary: Guards the security headers the SPA container serves alongside the built bundle.
// Major updates:
// - 2026-07-30 pending Covered the CSP, nosniff, referrer and frame-ancestors headers in nginx.conf.

import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const nginxConf = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), '../nginx.conf'), 'utf8');

describe('SPA security headers', () => {
  it('serves a content security policy that keeps scripts and styles first-party', () => {
    const policy = /add_header Content-Security-Policy "([^"]+)"/.exec(nginxConf)?.[1] ?? '';

    expect(policy).toContain("default-src 'self'");
    expect(policy).toContain("script-src 'self'");
    expect(policy).toContain("style-src 'self'");
    expect(policy).toContain("object-src 'none'");
    expect(policy).toContain("frame-ancestors 'none'");
    expect(policy).not.toContain('unsafe-inline');
    expect(policy).not.toContain('unsafe-eval');
  });

  it('allows the map tiles and object-storage images the app actually renders', () => {
    const policy = /add_header Content-Security-Policy "([^"]+)"/.exec(nginxConf)?.[1] ?? '';
    const imgSrc = /img-src ([^;]+)/.exec(policy)?.[1] ?? '';

    expect(imgSrc).toContain("'self'");
    expect(imgSrc).toContain('data:');
    expect(imgSrc).toContain('https:');
  });

  it('serves the supporting hardening headers', () => {
    expect(nginxConf).toContain('add_header X-Content-Type-Options "nosniff" always;');
    expect(nginxConf).toContain('add_header Referrer-Policy "strict-origin-when-cross-origin" always;');
    expect(nginxConf).toContain('add_header X-Frame-Options "DENY" always;');
  });
});
