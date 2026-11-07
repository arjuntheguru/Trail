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

function parseInstant(iso: string): Date {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(iso)
  return new Date(hasZone ? iso : `${iso}Z`)
}

/** Date and time of an instant (ISO string, UTC when it has no offset) in the viewer's time zone. */
export function formatDateTime(iso: string): string {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    hour: 'numeric',
    minute: '2-digit',
  }).format(parseInstant(iso))
}

/** The value a datetime-local input takes for an instant, in the viewer's time zone. */
export function toLocalInput(iso: string): string {
  const d = parseInstant(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  const date = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  return `${date}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** Converts a datetime-local value (viewer's time zone) to a UTC ISO instant for the API. */
export function fromLocalInput(value: string): string {
  return new Date(value).toISOString()
}
