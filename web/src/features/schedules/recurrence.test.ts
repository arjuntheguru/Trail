import { describe, expect, it } from 'vitest'

import {
  LAST_DAY_OF_MONTH,
  WeekDay,
  dayNumber,
  daysFromList,
  daysToList,
  describeRecurrence,
  formatWeekDays,
  fromDayNumber,
  isValidDate,
  nextOccurrences,
  occurrences,
  occursOn,
  parseWeekDays,
  validateRecurrence,
  type Recurrence,
} from './recurrence'

const rec = (r: Partial<Recurrence> & Pick<Recurrence, 'frequency' | 'startDate'>): Recurrence => ({
  interval: 1,
  ...r,
})

// The cases below are ported from tests/Trail.UnitTests/Tasks/RecurrenceTests.cs and RecurrenceSummaryTests.cs.
describe('occurrences (ported from RecurrenceTests.cs)', () => {
  it('daily every three days counts from the start date', () => {
    const r = rec({ frequency: 'Daily', interval: 3, startDate: '2026-03-01' })
    expect(occurrences(r, '2026-02-25', '2026-03-10')).toEqual([
      '2026-03-01',
      '2026-03-04',
      '2026-03-07',
      '2026-03-10',
    ])
  })

  it('weekdays only', () => {
    const r = rec({ frequency: 'Weekly', startDate: '2026-03-02', days: WeekDay.Weekdays })
    expect(occurrences(r, '2026-03-02', '2026-03-09')).toEqual([
      '2026-03-02',
      '2026-03-03',
      '2026-03-04',
      '2026-03-05',
      '2026-03-06',
      '2026-03-09',
    ])
  })

  it('fortnightly on two days skips alternate weeks even when starting midweek', () => {
    // Start on Wednesday 4 March; the start week counts as week 0 (its Monday is 2 March).
    const r = rec({
      frequency: 'Weekly',
      interval: 2,
      startDate: '2026-03-04',
      days: WeekDay.Monday | WeekDay.Thursday,
    })
    expect(occurrences(r, '2026-03-01', '2026-03-31')).toEqual([
      '2026-03-05',
      '2026-03-16',
      '2026-03-19',
      '2026-03-30',
    ])
  })

  it('weekly defaults to the weekday of the start date', () => {
    const r = rec({ frequency: 'Weekly', startDate: '2026-03-06' }) // Friday
    expect(occurrences(r, '2026-03-01', '2026-03-21')).toEqual([
      '2026-03-06',
      '2026-03-13',
      '2026-03-20',
    ])
  })

  it('monthly on the 31st falls on the last day of short months', () => {
    const r = rec({ frequency: 'Monthly', startDate: '2026-01-31' })
    expect(occurrences(r, '2026-01-01', '2026-05-31')).toEqual([
      '2026-01-31',
      '2026-02-28',
      '2026-03-31',
      '2026-04-30',
      '2026-05-31',
    ])
  })

  it('monthly last day handles leap years', () => {
    const r = rec({ frequency: 'Monthly', startDate: '2028-01-10', dayOfMonth: LAST_DAY_OF_MONTH })
    expect(occurrences(r, '2028-02-01', '2028-03-31')).toEqual(['2028-02-29', '2028-03-31'])
  })

  it('every six months', () => {
    const r = rec({ frequency: 'Monthly', interval: 6, startDate: '2026-01-15' })
    expect(occurrences(r, '2026-01-01', '2027-12-31')).toEqual([
      '2026-01-15',
      '2026-07-15',
      '2027-01-15',
      '2027-07-15',
    ])
  })

  it('yearly on 29 February uses 28 February in other years', () => {
    const r = rec({ frequency: 'Yearly', startDate: '2028-02-29' })
    expect(occurrences(r, '2028-01-01', '2030-12-31')).toEqual([
      '2028-02-29',
      '2029-02-28',
      '2030-02-28',
    ])
  })

  it('once and end dates are respected', () => {
    expect(
      occurrences(rec({ frequency: 'Once', startDate: '2026-05-01' }), '2026-04-01', '2026-06-01'),
    ).toEqual(['2026-05-01'])
    expect(
      occurrences(
        rec({ frequency: 'Daily', startDate: '2026-05-01', endDate: '2026-05-03' }),
        '2026-04-01',
        '2026-06-01',
      ),
    ).toHaveLength(3)
  })
})

describe('more calendar edges', () => {
  it('monthly on the 30th lands on 29 February in a leap year and 28 in others', () => {
    const r = rec({ frequency: 'Monthly', startDate: '2027-12-30' })
    expect(occurrences(r, '2028-01-01', '2028-03-31')).toEqual([
      '2028-01-30',
      '2028-02-29',
      '2028-03-30',
    ])
    expect(occurrences(r, '2029-02-01', '2029-02-28')).toEqual(['2029-02-28'])
  })

  it('an explicit day of month overrides the start date day', () => {
    const r = rec({ frequency: 'Monthly', startDate: '2026-01-05', dayOfMonth: 15 })
    expect(occurrences(r, '2026-01-01', '2026-03-31')).toEqual([
      '2026-01-15',
      '2026-02-15',
      '2026-03-15',
    ])
  })

  it('a chosen day before the start date first falls in the next month', () => {
    const r = rec({ frequency: 'Monthly', startDate: '2026-01-20', dayOfMonth: 5 })
    expect(nextOccurrences(r, '2026-01-01', 2)).toEqual(['2026-02-05', '2026-03-05'])
  })

  it('month intervals are counted in calendar months across year ends', () => {
    const r = rec({ frequency: 'Monthly', interval: 5, startDate: '2026-11-30' })
    expect(nextOccurrences(r, '2026-11-30', 3)).toEqual(['2026-11-30', '2027-04-30', '2027-09-30'])
  })

  it('last day of February follows the leap-year rule including century years', () => {
    const r = rec({ frequency: 'Yearly', startDate: '2096-02-29', dayOfMonth: LAST_DAY_OF_MONTH })
    expect(nextOccurrences(r, '2096-01-01', 6)).toEqual([
      '2096-02-29',
      '2097-02-28',
      '2098-02-28',
      '2099-02-28',
      '2100-02-28', // 2100 is not a leap year
      '2101-02-28',
    ])
  })

  it('yearly with an interval skips years', () => {
    const r = rec({ frequency: 'Yearly', interval: 2, startDate: '2026-06-10' })
    expect(nextOccurrences(r, '2026-01-01', 3)).toEqual(['2026-06-10', '2028-06-10', '2030-06-10'])
  })

  it('yearly with a day of month uses it inside the start month', () => {
    const r = rec({ frequency: 'Yearly', startDate: '2026-03-09', dayOfMonth: LAST_DAY_OF_MONTH })
    expect(nextOccurrences(r, '2026-01-01', 2)).toEqual(['2026-03-31', '2027-03-31'])
  })

  it('weeks start on Monday, so a Sunday start is the last day of week 0', () => {
    // Sunday 1 March 2026 closes the week of Monday 23 February (week 0); fortnightly, Sunday + Monday.
    const r = rec({
      frequency: 'Weekly',
      interval: 2,
      startDate: '2026-03-01',
      days: WeekDay.Sunday | WeekDay.Monday,
    })
    expect(occurrences(r, '2026-03-01', '2026-03-31')).toEqual([
      '2026-03-01',
      '2026-03-09',
      '2026-03-15',
      '2026-03-23',
      '2026-03-29',
    ])
  })

  it('nothing happens before the start date or after the end date', () => {
    const r = rec({ frequency: 'Daily', startDate: '2026-05-10', endDate: '2026-05-12' })
    expect(occursOn(r, '2026-05-09')).toBe(false)
    expect(occursOn(r, '2026-05-12')).toBe(true)
    expect(occursOn(r, '2026-05-13')).toBe(false)
  })

  it('crosses year boundaries without time zone drift', () => {
    const r = rec({ frequency: 'Daily', startDate: '2026-12-30' })
    expect(nextOccurrences(r, '2026-12-30', 4)).toEqual([
      '2026-12-30',
      '2026-12-31',
      '2027-01-01',
      '2027-01-02',
    ])
  })

  it('dates around DST changes are still one day apart', () => {
    const spring = rec({ frequency: 'Daily', startDate: '2026-03-28' })
    expect(nextOccurrences(spring, '2026-03-28', 4)).toEqual([
      '2026-03-28',
      '2026-03-29',
      '2026-03-30',
      '2026-03-31',
    ])
    const autumn = rec({ frequency: 'Daily', startDate: '2026-10-24' })
    expect(nextOccurrences(autumn, '2026-10-24', 4)).toEqual([
      '2026-10-24',
      '2026-10-25',
      '2026-10-26',
      '2026-10-27',
    ])
  })
})

describe('nextOccurrences', () => {
  it('starts at the later of today and the start date', () => {
    const r = rec({ frequency: 'Daily', startDate: '2030-01-01' })
    expect(nextOccurrences(r, '2026-01-01', 2)).toEqual(['2030-01-01', '2030-01-02'])
    expect(nextOccurrences(r, '2030-01-05', 2)).toEqual(['2030-01-05', '2030-01-06'])
  })

  it('returns ten by default and stops early at the end date', () => {
    expect(
      nextOccurrences(rec({ frequency: 'Daily', startDate: '2026-01-01' }), '2026-01-01'),
    ).toHaveLength(10)
    expect(
      nextOccurrences(
        rec({ frequency: 'Daily', startDate: '2026-01-01', endDate: '2026-01-03' }),
        '2026-01-01',
      ),
    ).toEqual(['2026-01-01', '2026-01-02', '2026-01-03'])
  })

  it('is empty once everything is in the past', () => {
    expect(
      nextOccurrences(rec({ frequency: 'Once', startDate: '2020-01-01' }), '2026-01-01'),
    ).toEqual([])
    expect(
      nextOccurrences(
        rec({ frequency: 'Daily', startDate: '2020-01-01', endDate: '2020-02-01' }),
        '2026-01-01',
      ),
    ).toEqual([])
  })

  it('finds the far-off date of the longest rule', () => {
    const r = rec({ frequency: 'Yearly', interval: 52, startDate: '2026-02-03' })
    expect(nextOccurrences(r, '2026-02-04', 1)).toEqual(['2078-02-03'])
  })
})

describe('validateRecurrence (ported from Recurrence.Validate)', () => {
  const start = '2026-01-01'
  it.each([
    [{ frequency: 'Daily', interval: 0 }, 'Interval must be between 1 and 52.'],
    [{ frequency: 'Daily', interval: 53 }, 'Interval must be between 1 and 52.'],
    [
      { frequency: 'Daily', dayOfMonth: 5 },
      'Day of month only applies to monthly and yearly schedules.',
    ],
    [{ frequency: 'Monthly', dayOfMonth: 32 }, 'Day of month must be 1-31 or -1 for the last day.'],
    [{ frequency: 'Monthly', dayOfMonth: 0 }, 'Day of month must be 1-31 or -1 for the last day.'],
    [
      { frequency: 'Daily', days: WeekDay.Monday },
      'Days of the week only apply to weekly schedules.',
    ],
    [{ frequency: 'Weekly', days: 128 }, 'Unknown day of the week.'],
    [{ frequency: 'Daily', endDate: '2025-12-31' }, 'End date cannot be before the start date.'],
  ] as const)('reports %j', (partial, message) => {
    expect(validateRecurrence({ interval: 1, startDate: start, ...partial })).toContain(message)
  })

  it('accepts valid recurrences', () => {
    expect(
      validateRecurrence(rec({ frequency: 'Monthly', startDate: start, dayOfMonth: -1 })),
    ).toEqual([])
    expect(
      validateRecurrence(
        rec({ frequency: 'Weekly', startDate: start, days: WeekDay.Weekdays, endDate: start }),
      ),
    ).toEqual([])
    expect(
      validateRecurrence(rec({ frequency: 'Yearly', interval: 52, startDate: start })),
    ).toEqual([])
  })
})

describe('describeRecurrence (ported from RecurrenceSummaryTests.cs)', () => {
  const start = '2026-03-09' // a Monday

  it.each([
    [{ frequency: 'Daily' }, 'Every day'],
    [{ frequency: 'Daily', interval: 3 }, 'Every 3 days'],
    [
      { frequency: 'Weekly', interval: 2, days: WeekDay.Thursday | WeekDay.Monday },
      'Every 2 weeks on Mon, Thu',
    ],
    [{ frequency: 'Weekly' }, 'Every week on Mon'],
    [{ frequency: 'Monthly', dayOfMonth: 15 }, 'Every month on day 15'],
    [{ frequency: 'Monthly', interval: 6 }, 'Every 6 months on day 9'],
    [{ frequency: 'Monthly', dayOfMonth: -1 }, 'Every month on the last day'],
    [{ frequency: 'Yearly' }, 'Every year on 9 Mar'],
    [{ frequency: 'Yearly', dayOfMonth: -1 }, 'Every year on 9 Mar (day last)'],
    [{ frequency: 'Yearly', dayOfMonth: 20 }, 'Every year on 9 Mar (day 20)'],
    [{ frequency: 'Once' }, 'Once on 9 Mar 2026'],
    [{ frequency: 'Daily', endDate: '2026-12-31' }, 'Every day, until 31 Dec 2026'],
  ] as const)('%j -> %s', (r, text) => {
    expect(describeRecurrence({ interval: 1, startDate: start, ...r })).toBe(text)
  })
})

describe('calendar helpers', () => {
  it('round-trips day numbers', () => {
    for (const iso of ['1999-12-31', '2000-02-29', '2026-03-09', '2100-02-28']) {
      expect(fromDayNumber(dayNumber(iso))).toBe(iso)
    }
    expect(dayNumber('2026-03-10') - dayNumber('2026-03-09')).toBe(1)
  })

  it('validates calendar dates', () => {
    expect(isValidDate('2028-02-29')).toBe(true)
    expect(isValidDate('2027-02-29')).toBe(false)
    expect(isValidDate('2026-13-01')).toBe(false)
    expect(isValidDate('')).toBe(false)
    expect(isValidDate('3/4/2026')).toBe(false)
  })

  it('converts week day flags', () => {
    expect(daysFromList(['Monday', 'Thursday'])).toBe(WeekDay.Monday | WeekDay.Thursday)
    expect(daysToList(WeekDay.Weekend)).toEqual(['Saturday', 'Sunday'])
    expect(formatWeekDays(0)).toBe('None')
    expect(formatWeekDays(WeekDay.Weekdays)).toBe('Monday, Tuesday, Wednesday, Thursday, Friday')
  })

  it('parses the .NET flags strings the API returns', () => {
    expect(parseWeekDays('None')).toBe(0)
    expect(parseWeekDays('Monday, Thursday')).toBe(9)
    expect(parseWeekDays('Weekdays')).toBe(WeekDay.Weekdays)
    expect(parseWeekDays('Weekdays, Saturday')).toBe(WeekDay.Weekdays | WeekDay.Saturday)
    expect(parseWeekDays('All')).toBe(WeekDay.All)
    expect(parseWeekDays('9')).toBe(9)
    expect(parseWeekDays(null)).toBe(0)
  })
})
