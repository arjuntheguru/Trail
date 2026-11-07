import {
  WeekDay,
  dayNumber,
  describeRecurrence,
  formatDate,
  fromDayNumber,
  type Recurrence,
} from './recurrence'

/** "09:00:00" or "09:00" -> "09:00". */
export const hhmm = (time: string) => time.slice(0, 5)

/** The API's TimeOnly wants seconds; time inputs give HH:mm. */
export const withSeconds = (time: string) => (time.length === 5 ? `${time}:00` : time)

export interface Targets {
  allSites: boolean
  siteCount: number
  regionCount: number
}

export function describeTargets({ allSites, siteCount, regionCount }: Targets): string {
  if (allSites) return 'at all sites'
  const plural = (n: number, noun: string) => `${n} ${noun}${n === 1 ? '' : 's'}`
  const bits = [
    regionCount > 0 ? plural(regionCount, 'region') : '',
    siteCount > 0 ? plural(siteCount, 'site') : '',
  ].filter(Boolean)
  return bits.length === 0 ? 'no sites chosen' : `at ${bits.join(' and ')}`
}

/** "Every weekday" reads better than "Every week on Mon, Tue, Wed, Thu, Fri". */
function recurrenceText(r: Recurrence): string {
  if (r.frequency === 'Weekly' && r.interval <= 1 && r.days === WeekDay.Weekdays) {
    return r.endDate ? `Every weekday, until ${formatDate(r.endDate)}` : 'Every weekday'
  }
  return describeRecurrence(r)
}

/** "Every weekday, from 06:00, due by 11:00, at 3 sites". Times are site-local. */
export function summarizeSchedule(input: {
  recurrence: Recurrence
  dueTime?: string | null
  availableFrom?: string | null
  targets: Targets
}): string {
  const parts = [recurrenceText(input.recurrence)]
  if (input.availableFrom) parts.push(`from ${hhmm(input.availableFrom)}`)
  if (input.dueTime) parts.push(`due by ${hhmm(input.dueTime)}`)
  parts.push(describeTargets(input.targets))
  return parts.join(', ')
}

const WEEKDAY_SHORT = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

/** "Mon 9 Mar 2026" */
export function formatOccurrence(iso: string): string {
  const n = dayNumber(iso)
  const name = WEEKDAY_SHORT[(new Date(n * 86_400_000).getUTCDay() + 6) % 7]
  return `${name} ${formatDate(fromDayNumber(n))}`
}

/** Today as a yyyy-mm-dd date in the viewer's calendar. */
export function todayIso(now = new Date()): string {
  const p = (v: number) => String(v).padStart(2, '0')
  return `${now.getFullYear()}-${p(now.getMonth() + 1)}-${p(now.getDate())}`
}
