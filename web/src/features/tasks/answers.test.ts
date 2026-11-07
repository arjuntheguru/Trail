import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api/problem'
import { field, taskDetail } from '@/test/fixtures'

import { groupBySection, toDraftAnswers, toFormAnswers, toServerAnswers } from './answers'
import { mapServerErrors } from './errors'
import { workability, addDays } from './workability'

const fields = [
  field({ key: 'done', type: 'Checkbox' }),
  field({ key: 'yes', type: 'YesNo' }),
  field({ key: 'temp', type: 'Temperature' }),
  field({ key: 'name', type: 'Text' }),
  field({
    key: 'many',
    type: 'MultiSelect',
    options: [{ value: 'a', label: 'A', isFailure: false }],
  }),
  field({ key: 'help', type: 'Instruction' }),
  field({ key: 'pic', type: 'Photo' }),
]

describe('form <-> server answers', () => {
  it('starts every answerable field empty and skips instructions', () => {
    const form = toFormAnswers(fields, undefined)
    expect(Object.keys(form)).toEqual(['done', 'yes', 'temp', 'name', 'many', 'pic'])
    expect(form.done?.value).toBe(false)
    expect(form.yes?.value).toBeNull()
    expect(form.many?.value).toEqual([])
  })

  it('loads a saved draft, turning numbers into editable text', () => {
    const form = toFormAnswers(fields, {
      temp: { value: 4.5, photoIds: [], comment: 'ok' },
      done: { value: true },
    })
    expect(form.temp).toEqual({ value: '4.5', photoIds: [], comment: 'ok' })
    expect(form.done?.value).toBe(true)
  })

  it('sends numbers as numbers and leaves out unanswered fields', () => {
    const form = toFormAnswers(fields, undefined)
    form.temp = { value: ' 4,5 ', photoIds: [], comment: '' }
    form.yes = { value: false, photoIds: [], comment: ' why ' }
    form.name = { value: '   ', photoIds: [], comment: '' }
    form.pic = { value: '', photoIds: ['p1'], comment: '' }
    expect(toServerAnswers(fields, form)).toEqual({
      temp: { value: 4.5, photoIds: [] },
      yes: { value: false, photoIds: [], comment: 'why' },
      pic: { photoIds: ['p1'] },
    })
  })

  it('never sends an unticked checkbox or an empty multi-select', () => {
    const answers = toServerAnswers(fields, toFormAnswers(fields, undefined))
    expect(answers).toEqual({})
  })

  it('drops half-typed numbers from drafts but keeps them for completion checks', () => {
    const form = toFormAnswers(fields, undefined)
    form.name = { value: 'Sam', photoIds: [], comment: '' }
    form.temp = { value: '4.', photoIds: [], comment: '' }
    expect(toServerAnswers(fields, form).temp).toEqual({ value: 4, photoIds: [] })
    form.temp = { value: '--', photoIds: [], comment: '' }
    expect(toServerAnswers(fields, form).temp).toEqual({ value: '--', photoIds: [] })
    expect(toDraftAnswers(fields, form)).toEqual({ name: { value: 'Sam', photoIds: [] } })
  })

  it('keeps photos and comments of a field whose value is dropped from a draft', () => {
    const form = toFormAnswers(fields, undefined)
    form.temp = { value: '--', photoIds: ['p1'], comment: '' }
    expect(toDraftAnswers(fields, form)).toEqual({ temp: { photoIds: ['p1'] } })
  })
})

describe('groupBySection', () => {
  it('groups consecutive fields and keeps order', () => {
    const groups = groupBySection([
      field({ key: 'a', type: 'Text', section: 'Fridge' }),
      field({ key: 'b', type: 'Text', section: 'Fridge' }),
      field({ key: 'c', type: 'Text' }),
      field({ key: 'd', type: 'Text', section: 'Fridge' }),
    ])
    expect(groups.map((g) => [g.section, g.fields.map((f) => f.key)])).toEqual([
      ['Fridge', ['a', 'b']],
      [null, ['c']],
      ['Fridge', ['d']],
    ])
  })
})

describe('mapServerErrors', () => {
  it('maps answers.<key> onto fields and the command properties onto their sections', () => {
    const error = new ApiError({
      status: 400,
      errors: {
        'answers.temp': ['Enter a number.'],
        'answers.name': ['One.', 'Two.'],
        photoIds: ['Attach at least one photo.'],
        note: ['Too long.'],
        other: ['Odd.'],
      },
    })
    expect(mapServerErrors(error)).toEqual({
      fields: { temp: 'Enter a number.', name: 'One. Two.' },
      photos: 'Attach at least one photo.',
      note: 'Too long.',
      general: ['Odd.'],
    })
  })

  it('matches the prefix whatever the casing', () => {
    const mapped = mapServerErrors(new ApiError({ status: 400, errors: { 'Answers.temp': ['x'] } }))
    expect(mapped.fields).toEqual({ temp: 'x' })
  })

  it('falls back to a general message', () => {
    expect(
      mapServerErrors(new ApiError({ status: 409, detail: 'The task is not open.' })).general,
    ).toEqual(['The task is not open.'])
    expect(mapServerErrors(new Error('boom')).general).toHaveLength(1)
  })
})

describe('workability', () => {
  const now = Date.parse('2026-11-07T09:00:00Z')

  it('lets people with permission work open tasks', () => {
    expect(workability(taskDetail(), now, true)).toEqual({ workable: true })
  })
  it('needs the complete permission', () => {
    expect(workability(taskDetail(), now, false).workable).toBe(false)
  })
  it('waits for the availability window', () => {
    const result = workability(taskDetail({ availableAt: '2026-11-07T10:00:00Z' }), now, true)
    expect(result.workable).toBe(false)
    expect(result.message).toMatch(/Not open yet/)
    expect(
      workability(taskDetail({ availableAt: '2026-11-07T08:00:00Z' }), now, true).workable,
    ).toBe(true)
  })
  it('allows late completion only inside the window', () => {
    const late = workability(taskDetail({ status: 'Missed', canCompleteLate: true }), now, true)
    expect(late.workable).toBe(true)
    expect(late.message).toMatch(/late/)
    expect(workability(taskDetail({ status: 'Missed' }), now, true).workable).toBe(false)
  })
  it('does not offer completed or cancelled tasks for editing', () => {
    expect(workability(taskDetail({ status: 'Completed' }), now, true).workable).toBe(false)
    expect(workability(taskDetail({ status: 'Cancelled' }), now, true).workable).toBe(false)
  })
})

describe('addDays', () => {
  it('moves across month and year ends', () => {
    expect(addDays('2026-11-07', 1)).toBe('2026-11-08')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2026-02-27', 2)).toBe('2026-03-01')
  })
})
