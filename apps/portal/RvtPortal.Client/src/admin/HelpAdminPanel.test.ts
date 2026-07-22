// File summary: Unit tests for Help/FAQ admin helpers.
// Major updates:
// - 2026-06-29 pending Added regression coverage for linear Help CMS slug generation.

import { describe, expect, it } from 'vitest';
import { slugify } from './HelpAdminSlug';

describe('slugify', () => {
  it('normalizes display text into a URL-safe slug', () => {
    expect(slugify('  Dust Reading Definitions  ')).toBe('dust-reading-definitions');
    expect(slugify('Noise & Vibration: Weekly Summary')).toBe('noise-vibration-weekly-summary');
  });

  it('trims generated separators without regex backtracking risk', () => {
    expect(slugify('---Dust---')).toBe('dust');
    expect(slugify('###'.repeat(1000) + 'Noise' + '###'.repeat(1000))).toBe('noise');
  });
});
