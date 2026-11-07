import { z } from 'zod'

import {
  FREQUENCIES,
  MAX_INTERVAL,
  SINGLE_DAYS,
  WeekDay,
  daysFromList,
  daysToList,
  formatWeekDays,
  isValidDate,
  parseWeekDays,
  type Frequency,
  type Recurrence,
} from './recurrence'
import type { Schedule, ScheduleCommand } from './queries'
import { hhmm, todayIso, withSeconds } from './summary'

// Mirrors Recurrence.Validate and the schedule validator; the server stays the authority.
export const scheduleSchema = z
  .object({
    templateId: z.string().min(1, 'Choose a template.'),
    frequency: z.enum(FREQUENCIES),
    interval: z.string(),
    startDate: z.string(),
    endDate: z.string(),
    days: z.array(z.enum(SINGLE_DAYS)),
    dayOfMonth: z.string(),
    availableFrom: z.string(),
    dueTime: z.string().min(1, 'Set the time the task is due by.'),
    allSites: z.boolean(),
    siteIds: z.array(z.string()),
    regionIds: z.array(z.string()),
    departmentId: z.string(),
    // Only ever set from the server's answer, so a failure about the whole recurrence or the targets has a home.
    recurrence: z.string(),
    targets: z.string(),
  })
  .superRefine((v, ctx) => {
    const add = (path: string, message: string) =>
      ctx.addIssue({ code: 'custom', path: [path], message })
    if (!isValidDate(v.startDate)) add('startDate', 'Choose a start date.')
    if (v.frequency !== 'Once') {
      const interval = Number(v.interval)
      if (
        v.interval.trim() === '' ||
        !Number.isInteger(interval) ||
        interval < 1 ||
        interval > MAX_INTERVAL
      )
        add('interval', `Enter a whole number from 1 to ${MAX_INTERVAL}.`)
      if (v.endDate !== '' && !isValidDate(v.endDate)) add('endDate', 'Enter a valid end date.')
      else if (v.endDate !== '' && isValidDate(v.startDate) && v.endDate < v.startDate)
        add('endDate', 'The end date cannot be before the start date.')
    }
    if (v.availableFrom !== '' && v.dueTime !== '' && hhmm(v.availableFrom) >= hhmm(v.dueTime))
      add('availableFrom', '"Available from" must be earlier than the due time.')
    if (!v.allSites && v.siteIds.length === 0 && v.regionIds.length === 0)
      add('targets', 'Choose all sites, or at least one region or site.')
  })

export type ScheduleValues = z.infer<typeof scheduleSchema>
export type ScheduleField = keyof ScheduleValues

export const FIELDS: readonly ScheduleField[] = [
  'templateId',
  'frequency',
  'interval',
  'startDate',
  'endDate',
  'days',
  'dayOfMonth',
  'availableFrom',
  'dueTime',
  'departmentId',
  'recurrence',
  'targets',
]

/** Server property names that are not the form's field names. */
export const ALIASES: Record<string, ScheduleField> = {
  SiteIds: 'targets',
  RegionIds: 'targets',
  AllSites: 'targets',
  DepartmentId: 'departmentId',
}

export const CODE_FIELDS: Record<string, ScheduleField> = {
  'schedule.no_target': 'targets',
  'schedule.out_of_scope': 'targets',
  'schedule.template_inactive': 'templateId',
}

export function toValues(schedule?: Schedule, today = todayIso()): ScheduleValues {
  return {
    templateId: schedule?.templateId ?? '',
    frequency: schedule?.frequency ?? 'Daily',
    interval: String(schedule?.interval ?? 1),
    startDate: schedule?.startDate ?? today,
    endDate: schedule?.endDate ?? '',
    days: schedule ? daysToList(parseWeekDays(schedule.days)) : [],
    dayOfMonth: schedule?.dayOfMonth == null ? '' : String(schedule.dayOfMonth),
    availableFrom: schedule?.availableFrom ? hhmm(schedule.availableFrom) : '',
    dueTime: schedule ? hhmm(schedule.dueTime) : '',
    allSites: schedule?.allSites ?? false,
    siteIds: schedule?.siteIds ?? [],
    regionIds: schedule?.regionIds ?? [],
    departmentId: schedule?.departmentId ?? '',
    recurrence: '',
    targets: '',
  }
}

/** The recurrence the values describe, or null while they cannot be read yet. */
export function toRecurrence(v: ScheduleValues): Recurrence | null {
  if (!isValidDate(v.startDate)) return null
  const once = v.frequency === 'Once'
  const interval = once ? 1 : Number(v.interval)
  if (v.interval.trim() === '' && !once) return null
  const hasDayOfMonth = v.frequency === 'Monthly' || v.frequency === 'Yearly'
  return {
    frequency: v.frequency,
    interval,
    startDate: v.startDate,
    endDate: !once && isValidDate(v.endDate) ? v.endDate : null,
    days: v.frequency === 'Weekly' ? daysFromList(v.days) : WeekDay.None,
    dayOfMonth: hasDayOfMonth && v.dayOfMonth !== '' ? Number(v.dayOfMonth) : null,
  }
}

/** Fields that do not apply to the chosen frequency are sent as their neutral value, never left over. */
export function toBody(v: ScheduleValues): ScheduleCommand {
  const r = toRecurrence(v)
  return {
    templateId: v.templateId,
    frequency: v.frequency as Frequency,
    interval: r?.interval ?? 1,
    startDate: v.startDate,
    endDate: r?.endDate ?? null,
    days: formatWeekDays(r?.days ?? 0),
    dayOfMonth: r?.dayOfMonth ?? null,
    dueTime: withSeconds(v.dueTime),
    availableFrom: v.availableFrom ? withSeconds(v.availableFrom) : null,
    allSites: v.allSites,
    siteIds: v.allSites ? [] : v.siteIds,
    regionIds: v.allSites ? [] : v.regionIds,
    departmentId: v.departmentId || null,
  }
}
