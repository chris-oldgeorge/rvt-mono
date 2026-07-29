// File summary: The Portal's one locale policy for dates and numbers.
// Sixteen formatter copies existed across the screens with two locale
// policies: most pinned 'en-GB', two screens (Reports, Notifications) used the
// browser locale, so the same timestamp rendered differently per screen. The
// product formats deterministically in en-GB everywhere; empty inputs render
// as an empty string so callers can supply their own placeholder.

const locale = 'en-GB';

// Function summary: Formats an ISO date as a medium en-GB date, or '' when absent.
export function formatDate(value?: string | null) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(new Date(value));
}

// Function summary: Formats an ISO timestamp as a medium en-GB date with short time, or '' when absent.
export function formatDateTime(value?: string | null, timeZone?: string) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(value));
}

// Function summary: Formats a calendar month as 'July 2026'.
export function formatMonthYear(year: number, month: number) {
  return new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
}

// Function summary: Formats a number with bounded fraction digits in en-GB grouping, or '' when absent.
export function formatNumber(value?: number | null, maximumFractionDigits = 2) {
  if (typeof value !== 'number') {
    return '';
  }
  return new Intl.NumberFormat(locale, { maximumFractionDigits }).format(value);
}
