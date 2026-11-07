import { describe, expect, it } from 'vitest'

import { toBody, toValues } from './form'
import { WeekDay } from './recurrence'
import {
  describeTargets,
  formatOccurrence,
  hhmm,
  summarizeSchedule,
  todayIso,
  withSeconds,
} from './summary'

const one = { allSites: false, siteCount: 1, regionCount: 0 }

describe('summarizeSchedule', () => {
  it('reads weekdays naturally, with times and sites', () => {
    expect(
      summarizeSchedule({
        recurrence: {
          frequency: 'Weekly',
          interval: 1,
          startDate: '2026-03-02',
          days: WeekDay.Weekdays,
        },
        availableFrom: '06:00:00',
        dueTime: '11:00:00',
        targets: { allSites: false, siteCount: 3, regionCount: 0 },
      }),
    ).toBe('Every weekday, from 06:00, due by 11:00, at 3 sites')
  })

  it('falls back to the server wording for other recurrences', () => {
    expect(
      summarizeSchedule({
        recurrence: {
          frequency: 'Weekly',
          interval: 2,
          startDate: '2026-03-02',
          days: WeekDay.Monday | WeekDay.Thursday,
          endDate: '2026-12-31',
        },
        dueTime: '09:00',
        targets: one,
      }),
    ).toBe('Every 2 weeks on Mon, Thu, until 31 Dec 2026, due by 09:00, at 1 site')
    expect(
      summarizeSchedule({
        recurrence: {
          frequency: 'Weekly',
          interval: 2,
          startDate: '2026-03-02',
          days: WeekDay.Weekdays,
        },
        targets: one,
      }),
    ).toBe('Every 2 weeks on Mon, Tue, Wed, Thu, Fri, at 1 site')
  })

  it('names the targets', () => {
    expect(describeTargets({ allSites: true, siteCount: 0, regionCount: 0 })).toBe('at all sites')
    expect(describeTargets({ allSites: false, siteCount: 2, regionCount: 1 })).toBe(
      'at 1 region and 2 sites',
    )
    expect(describeTargets({ allSites: false, siteCount: 0, regionCount: 0 })).toBe(
      'no sites chosen',
    )
  })

  it('keeps "until" for a weekday schedule', () => {
    expect(
      summarizeSchedule({
        recurrence: {
          frequency: 'Weekly',
          interval: 1,
          startDate: '2026-03-02',
          days: WeekDay.Weekdays,
          endDate: '2026-06-30',
        },
        targets: one,
      }),
    ).toBe('Every weekday, until 30 Jun 2026, at 1 site')
  })
})

describe('formatting helpers', () => {
  it('handles times and dates', () => {
    expect(hhmm('09:30:00')).toBe('09:30')
    expect(withSeconds('09:30')).toBe('09:30:00')
    expect(withSeconds('09:30:15')).toBe('09:30:15')
    expect(formatOccurrence('2028-02-29')).toBe('Tue 29 Feb 2028')
    expect(todayIso(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05')
  })
})

describe('toBody', () => {
  const base = toValues(undefined, '2030-01-07')
  const values = {
    ...base,
    templateId: 't1',
    dueTime: '10:00',
    siteIds: ['a'],
    regionIds: ['r'],
  }

  it('drops fields the frequency does not use', () => {
    const body = toBody({
      ...values,
      frequency: 'Daily',
      days: ['Monday'],
      dayOfMonth: '5',
      endDate: '2030-02-01',
      interval: '3',
    })
    expect(body).toMatchObject({
      frequency: 'Daily',
      interval: 3,
      days: 'None',
      dayOfMonth: null,
      endDate: '2030-02-01',
    })
  })

  it('keeps days for weekly and dayOfMonth for monthly and yearly', () => {
    expect(toBody({ ...values, frequency: 'Weekly', days: ['Tuesday', 'Sunday'] }).days).toBe(
      'Tuesday, Sunday',
    )
    expect(toBody({ ...values, frequency: 'Monthly', dayOfMonth: '15' }).dayOfMonth).toBe(15)
    expect(toBody({ ...values, frequency: 'Yearly', dayOfMonth: '-1' }).dayOfMonth).toBe(-1)
    expect(toBody({ ...values, frequency: 'Monthly', dayOfMonth: '' }).dayOfMonth).toBeNull()
  })

  it('ignores listed sites and regions when all sites is on', () => {
    expect(toBody({ ...values, allSites: true })).toMatchObject({
      allSites: true,
      siteIds: [],
      regionIds: [],
    })
  })

  it('forces interval 1 and no end date for a one-off', () => {
    expect(
      toBody({ ...values, frequency: 'Once', interval: '9', endDate: '2031-01-01' }),
    ).toMatchObject({ interval: 1, endDate: null })
  })
})
