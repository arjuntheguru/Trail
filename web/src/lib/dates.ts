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

/** Date and time of an instant (ISO string, UTC when it has no offset) in the given zone (default: the viewer's). */
export function formatDateTime(iso: string, timeZone?: string): string {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    hour: 'numeric',
    minute: '2-digit',
    timeZone,
  }).format(parseInstant(iso))
}

/** The wall-clock parts of an instant in a zone (default: the viewer's). */
function wallClock(date: Date, timeZone?: string) {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).formatToParts(date)
  const get = (type: string) => Number(parts.find((p) => p.type === type)?.value)
  return { y: get('year'), mo: get('month'), d: get('day'), h: get('hour'), mi: get('minute') }
}

/** The value a datetime-local input takes for an instant, as the wall clock of the given zone. */
export function toLocalInput(iso: string, timeZone?: string): string {
  const { y, mo, d, h, mi } = wallClock(parseInstant(iso), timeZone)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${String(y).padStart(4, '0')}-${p(mo)}-${p(d)}T${p(h)}:${p(mi)}`
}

/**
 * Converts a datetime-local value, read as the wall clock of the given zone (default: the viewer's), to a UTC
 * ISO instant for the API. Times skipped by a DST change resolve to the instant just after the gap.
 */
export function fromLocalInput(value: string, timeZone?: string): string {
  if (!timeZone) return new Date(value).toISOString()
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value)
  if (!m) return new Date(value).toISOString()
  const [y, mo, d, h, mi] = m.slice(1).map(Number) as [number, number, number, number, number]
  const wanted = Date.UTC(y, mo - 1, d, h, mi)
  // The zone's offset (shown minus real) a day either side brackets any clock change; the right one makes the
  // wall clock match. In a gap neither does, and the offset from before it lands just after the gap.
  const offsetAt = (t: number) => {
    const w = wallClock(new Date(t), timeZone)
    return Date.UTC(w.y, w.mo - 1, w.d, w.h, w.mi) - Math.floor(t / 60_000) * 60_000
  }
  const before = wanted - offsetAt(wanted - 86_400_000)
  const after = wanted - offsetAt(wanted + 86_400_000)
  const matches = (t: number) => offsetAt(t) === wanted - t
  const instant = [before, after].filter(matches).sort((x, y) => x - y)[0] ?? before
  return new Date(instant).toISOString()
}

const pad2 = (n: number) => String(n).padStart(2, '0')

/** The viewer's calendar date as yyyy-mm-dd. */
export function localDay(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

/** Whether the text is a real calendar date written as yyyy-mm-dd. */
export function isDay(value: string): boolean {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!m) return false
  const [y, mo, d] = [Number(m[1]), Number(m[2]), Number(m[3])]
  const date = new Date(Date.UTC(y, mo - 1, d))
  return date.getUTCFullYear() === y && date.getUTCMonth() === mo - 1 && date.getUTCDate() === d
}

function dayNumber(day: string): number {
  const [y, m, d] = day.split('-').map(Number)
  return Math.round(Date.UTC(y!, m! - 1, d!) / 86_400_000)
}

/** A yyyy-mm-dd date shifted by whole days. */
export function addDays(day: string, days: number): string {
  const date = new Date((dayNumber(day) + days) * 86_400_000)
  return `${date.getUTCFullYear()}-${pad2(date.getUTCMonth() + 1)}-${pad2(date.getUTCDate())}`
}

/** Calendar days from `from` to `to` inclusive (1 when they are the same day). */
export function daysInclusive(from: string, to: string): number {
  return dayNumber(to) - dayNumber(from) + 1
}

/** Whether a datetime-local value, read in the given zone (default: the viewer's), is not in the future. */
export function isPastLocalInput(value: string, timeZone?: string): boolean {
  return new Date(fromLocalInput(value, timeZone)).getTime() <= Date.now()
}
