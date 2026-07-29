// File summary: Provides date conversion and presentation helpers shared by the data-view UI and its tests.

export function fromDateToApi(value: string) {
  if (!value) {
    return null;
  }

  return new Date(value).toISOString();
}

export function formatDateTime(value?: string | null, timeZone?: string) {
  if (!value) {
    return '';
  }

  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(
    new Date(value),
  );
}
