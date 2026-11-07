/** Formats an instant (ISO string, UTC when it has no offset) as a time of day in the given IANA zone. */
export function formatTime(iso: string, timeZone?: string): string {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(iso)
  const date = new Date(hasZone ? iso : `${iso}Z`)
  return new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone,
  }).format(date)
}

/** Formats a calendar date (yyyy-mm-dd) without shifting it by the viewer's time zone. */
export function formatDay(day: string): string {
  const [y, m, d] = day.split('-').map(Number)
  if (!y || !m || !d) return day
  return new Intl.DateTimeFormat(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(y, m - 1, d)))
}
