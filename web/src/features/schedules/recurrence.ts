/**
 * A pure client copy of `Trail.Domain.Tasks.Recurrence` (and `RecurrenceSummary`). Dates are calendar dates
 * (`yyyy-mm-dd`) in the site's local calendar, so time zones and DST never matter here. Keep in step with the
 * C# rules: the server stays the authority, this only previews and validates early.
 */

export const FREQUENCIES = ['Once', 'Daily', 'Weekly', 'Monthly', 'Yearly'] as const
export type Frequency = (typeof FREQUENCIES)[number]

/** Same bit values as the C# `WeekDays` flags. */
export const WeekDay = {
  None: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 4,
  Thursday: 8,
  Friday: 16,
  Saturday: 32,
  Sunday: 64,
  Weekdays: 31,
  Weekend: 96,
  All: 127,
} as const

export const SINGLE_DAYS = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
] as const
export type SingleDay = (typeof SINGLE_DAYS)[number]

export const LAST_DAY_OF_MONTH = -1
export const MAX_INTERVAL = 52

export interface Recurrence {
  frequency: Frequency
  interval: number
  /** yyyy-mm-dd */
  startDate: string
  endDate?: string | null
  /** WeekDay bit flags. Weekly only; 0 means the start date's weekday. */
  days?: number
  /** Monthly/yearly: 1-31 or -1 for the last day; null/undefined means the start date's day. */
  dayOfMonth?: number | null
}

// --- calendar helpers -------------------------------------------------------------------------------------

const MS_PER_DAY = 86_400_000

/** Days since 1970-01-01 for a yyyy-mm-dd date (the C# `DayNumber`, offset by a constant). */
export function dayNumber(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number) as [number, number, number]
  return Math.round(Date.UTC(y, m - 1, d) / MS_PER_DAY)
}

export function fromDayNumber(n: number): string {
  const date = new Date(n * MS_PER_DAY)
  const p = (v: number, w = 2) => String(v).padStart(w, '0')
  return `${p(date.getUTCFullYear(), 4)}-${p(date.getUTCMonth() + 1)}-${p(date.getUTCDate())}`
}

export function isValidDate(iso: string | null | undefined): iso is string {
  if (!iso || !/^\d{4}-\d{2}-\d{2}$/.test(iso)) return false
  return fromDayNumber(dayNumber(iso)) === iso
}

export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

const parts = (iso: string) => iso.split('-').map(Number) as [number, number, number]

/** 0 = Monday ... 6 = Sunday. */
function mondayIndex(n: number): number {
  return (new Date(n * MS_PER_DAY).getUTCDay() + 6) % 7
}

const flagOf = (n: number) => 1 << mondayIndex(n)

// --- week day flags ---------------------------------------------------------------------------------------

export function daysToList(days: number): SingleDay[] {
  return SINGLE_DAYS.filter((_, i) => (days & (1 << i)) !== 0)
}

export function daysFromList(list: readonly SingleDay[]): number {
  return list.reduce((mask, day) => mask | (1 << SINGLE_DAYS.indexOf(day)), 0)
}

/** What the API sends for `WeekDays`: a .NET flags string ("Monday, Thursday", "Weekdays, Saturday"), or a number. */
export function parseWeekDays(value: string | number | null | undefined): number {
  if (typeof value === 'number') return value
  if (!value) return 0
  if (/^\s*\d+\s*$/.test(value)) return Number(value)
  return value
    .split(',')
    .map((s) => s.trim())
    .reduce((mask, name) => mask | (WeekDay[name as keyof typeof WeekDay] ?? 0), 0)
}

/** The string form the API accepts: single day names, comma separated, or "None". */
export function formatWeekDays(days: number): string {
  const list = daysToList(days)
  return list.length === 0 ? 'None' : list.join(', ')
}

// --- rules ------------------------------------------------------------------------------------------------

/** Error messages exactly as `Recurrence.Validate` words them. */
export function validateRecurrence(r: Recurrence): string[] {
  const errors: string[] = []
  const days = r.days ?? 0
  if (!Number.isInteger(r.interval) || r.interval < 1 || r.interval > MAX_INTERVAL)
    errors.push(`Interval must be between 1 and ${MAX_INTERVAL}.`)
  if (r.endDate && r.endDate < r.startDate) errors.push('End date cannot be before the start date.')
  if (days !== 0 && r.frequency !== 'Weekly')
    errors.push('Days of the week only apply to weekly schedules.')
  if ((days & ~WeekDay.All) !== 0) errors.push('Unknown day of the week.')
  const d = r.dayOfMonth
  if (d !== null && d !== undefined) {
    if (r.frequency !== 'Monthly' && r.frequency !== 'Yearly')
      errors.push('Day of month only applies to monthly and yearly schedules.')
    if (!(d === LAST_DAY_OF_MONTH || (Number.isInteger(d) && d >= 1 && d <= 31)))
      errors.push('Day of month must be 1-31 or -1 for the last day.')
  }
  return errors
}

function dueDayIn(r: Recurrence, year: number, month: number): number {
  const days = daysInMonth(year, month)
  const wanted = r.dayOfMonth ?? parts(r.startDate)[2]
  return wanted === LAST_DAY_OF_MONTH || wanted > days ? days : wanted
}

/** Whole weeks between the Monday-starting weeks containing each date. */
function weeksBetween(start: number, date: number): number {
  const mondayOf = (n: number) => n - mondayIndex(n)
  return (mondayOf(date) - mondayOf(start)) / 7
}

export function occursOn(r: Recurrence, iso: string): boolean {
  if (iso < r.startDate || (r.endDate && iso > r.endDate)) return false
  const date = dayNumber(iso)
  const start = dayNumber(r.startDate)
  const [y, m, d] = parts(iso)
  const [sy, sm] = parts(r.startDate)
  switch (r.frequency) {
    case 'Once':
      return iso === r.startDate
    case 'Daily':
      return (date - start) % r.interval === 0
    case 'Weekly': {
      const effective = r.days ? r.days : flagOf(start)
      return weeksBetween(start, date) % r.interval === 0 && (effective & flagOf(date)) !== 0
    }
    case 'Monthly':
      return ((y - sy) * 12 + m - sm) % r.interval === 0 && d === dueDayIn(r, y, m)
    case 'Yearly':
      return (y - sy) % r.interval === 0 && m === sm && d === dueDayIn(r, y, m)
    default:
      return false
  }
}

/** Occurrences from `from` to `to` inclusive. */
export function occurrences(r: Recurrence, from: string, to: string): string[] {
  const result: string[] = []
  const last = dayNumber(to)
  for (let n = dayNumber(from > r.startDate ? from : r.startDate); n <= last; n++) {
    const iso = fromDayNumber(n)
    if (occursOn(r, iso)) result.push(iso)
  }
  return result
}

/** Longest gap between two occurrences is 52 years (yearly, interval 52). */
const SEARCH_LIMIT_DAYS = 366 * 53

/**
 * The next `count` occurrences on or after `from`. Stops at the end date, and after a bounded search so an
 * odd rule can never hang the page.
 */
export function nextOccurrences(r: Recurrence, from: string, count = 10): string[] {
  const result: string[] = []
  const first = dayNumber(from > r.startDate ? from : r.startDate)
  const endLimit = r.endDate ? dayNumber(r.endDate) : Infinity
  for (
    let n = first;
    result.length < count && n <= endLimit && n < first + SEARCH_LIMIT_DAYS;
    n++
  ) {
    const iso = fromDayNumber(n)
    if (occursOn(r, iso)) result.push(iso)
  }
  return result
}

// --- wording (RecurrenceSummary) ---

const DAY_ABBREVIATIONS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
const MONTH_ABBREVIATIONS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
]

/** "9 Mar 2026" */
export function formatDate(iso: string): string {
  const [y, m, d] = parts(iso)
  return `${d} ${MONTH_ABBREVIATIONS[m - 1]} ${y}`
}

const every = (interval: number, unit: string) =>
  interval <= 1 ? `Every ${unit}` : `Every ${interval} ${unit}s`

/** A short sentence, worded exactly as the server's `RecurrenceSummary.Describe`. */
export function describeRecurrence(r: Recurrence): string {
  const [, sm, sd] = parts(r.startDate)
  const dayText = (d: number) => (d === LAST_DAY_OF_MONTH ? 'last' : String(d))
  const monthDay = () => {
    const wanted = r.dayOfMonth ?? sd
    return wanted === LAST_DAY_OF_MONTH ? 'the last day' : `day ${wanted}`
  }
  let text: string
  switch (r.frequency) {
    case 'Once':
      text = `Once on ${formatDate(r.startDate)}`
      break
    case 'Daily':
      text = every(r.interval, 'day')
      break
    case 'Weekly': {
      const days = r.days ? r.days : flagOf(dayNumber(r.startDate))
      const names = DAY_ABBREVIATIONS.filter((_, i) => (days & (1 << i)) !== 0).join(', ')
      text = `${every(r.interval, 'week')} on ${names}`
      break
    }
    case 'Monthly':
      text = `${every(r.interval, 'month')} on ${monthDay()}`
      break
    case 'Yearly':
      text =
        `${every(r.interval, 'year')} on ${sd} ${MONTH_ABBREVIATIONS[sm - 1]}` +
        (r.dayOfMonth != null && r.dayOfMonth !== sd ? ` (day ${dayText(r.dayOfMonth)})` : '')
      break
    default:
      text = String(r.frequency)
  }
  return r.endDate ? `${text}, until ${formatDate(r.endDate)}` : text
}
