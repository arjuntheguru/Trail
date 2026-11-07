import { describe, expect, it } from 'vitest'

import { field } from '@/test/fixtures'

import {
  describeRange,
  evaluateAnswers,
  isAnswered,
  parseDecimal,
  ruleHint,
  unitOf,
} from './evaluate'
import type { ServerAnswers, TaskField } from './types'

const GUID = '8f14e45f-ceea-467a-9575-8f3c1b1f4c2a'

function run(f: TaskField, answer: ServerAnswers[string] | undefined, requireAll = true) {
  return evaluateAnswers([f], answer ? { [f.key]: answer } : {}, requireAll)
}

describe('parseDecimal', () => {
  it.each([
    ['4', 4],
    ['4.5', 4.5],
    ['4,5', 4.5],
    ['-18', -18],
    [' 3 ', 3],
    ['.5', 0.5],
    [7, 7],
  ])('parses %s', (input, expected) => expect(parseDecimal(input)).toBe(expected))

  it.each(['', '-', 'abc', '1e3', '1.2.3', null, undefined, NaN, true])('rejects %s', (input) =>
    expect(parseDecimal(input)).toBeNull(),
  )
})

describe('units and ranges', () => {
  it('defaults temperature to degrees celsius', () => {
    expect(unitOf(field({ key: 't', type: 'Temperature' }))).toBe('°C')
    expect(unitOf(field({ key: 't', type: 'Temperature', unit: '°F' }))).toBe('°F')
    expect(unitOf(field({ key: 'n', type: 'Number' }))).toBe('')
  })

  it('describes ranges', () => {
    const t = (min?: number, max?: number) => field({ key: 't', type: 'Temperature', min, max })
    expect(describeRange(t(0, 5))).toBe('0–5 °C')
    expect(describeRange(t(63))).toBe('at least 63 °C')
    expect(describeRange(t(undefined, 5))).toBe('at most 5 °C')
    expect(describeRange(t())).toBeNull()
    expect(describeRange(field({ key: 'n', type: 'Number', min: 1, max: 9 }))).toBe('1–9')
  })

  it('hints the rule under the input', () => {
    expect(ruleHint(field({ key: 't', type: 'Temperature', min: 0, max: 5 }))).toBe(
      'Expected 0–5 °C',
    )
    expect(ruleHint(field({ key: 'y', type: 'YesNo', expectedAnswer: false }))).toBe(
      'Expected answer: No',
    )
    expect(ruleHint(field({ key: 'y', type: 'YesNo' }))).toBeNull()
    expect(ruleHint(field({ key: 'x', type: 'Text' }))).toBeNull()
  })
})

describe('Checkbox', () => {
  const f = field({ key: 'c', type: 'Checkbox' })
  it('is answered only when ticked', () => {
    expect(isAnswered(f, { value: true })).toBe(true)
    expect(isAnswered(f, { value: false })).toBe(false)
  })
  it('requires a tick when required', () => {
    expect(run(f, { value: false }).problems).toEqual([
      { fieldKey: 'c', message: "'c' is required." },
    ])
    expect(run(f, { value: true }).isValid).toBe(true)
  })
  it('allows leaving an optional one empty', () => {
    expect(run({ ...f, required: false }, undefined).isValid).toBe(true)
  })
})

describe('YesNo', () => {
  const f = field({ key: 'y', type: 'YesNo', expectedAnswer: true })
  it('passes the expected answer', () => {
    const result = run(f, { value: true })
    expect(result.isValid).toBe(true)
    expect(result.failures).toEqual([])
  })
  it('fails the other answer and asks for a comment', () => {
    const result = run(f, { value: false })
    expect(result.failures).toEqual([
      { fieldKey: 'y', label: 'y', reason: 'Answered no, expected yes' },
    ])
    expect(result.problems.map((p) => p.message)).toEqual([
      "'y' failed: describe the corrective action taken.",
    ])
    expect(run(f, { value: false, comment: 'Told the manager' }).isValid).toBe(true)
  })
  it('fails nothing without an expected answer', () => {
    expect(run(field({ key: 'y', type: 'YesNo' }), { value: false }).failures).toEqual([])
  })
  it('accepts false as an answer', () => {
    expect(isAnswered(f, { value: false })).toBe(true)
  })
  it('rejects non-booleans', () => {
    expect(run(f, { value: 'yes' }).problems[0]?.message).toBe('Answer yes or no.')
  })
})

describe('Number and Temperature', () => {
  const temp = field({ key: 't', type: 'Temperature', min: 0, max: 5, label: 'Fridge' })

  it('passes inside the range, inclusive', () => {
    for (const value of [0, 3.2, 5]) expect(run(temp, { value }).failures).toEqual([])
  })

  it('fails outside the range with the unit', () => {
    const result = run(temp, { value: 9 })
    expect(result.failures).toEqual([
      { fieldKey: 't', label: 'Fridge', reason: 'Out of range: 9 °C, expected 0–5 °C' },
    ])
    const below = run(temp, { value: -1 })
    expect(below.failures[0]?.reason).toBe('Out of range: -1 °C, expected 0–5 °C')
  })

  it('uses a custom unit and open-ended ranges', () => {
    const hot = field({ key: 'h', type: 'Temperature', min: 63, unit: '°F' })
    expect(run(hot, { value: 40 }).failures[0]?.reason).toBe(
      'Out of range: 40 °F, expected at least 63 °F',
    )
    const count = field({ key: 'n', type: 'Number', max: 10 })
    expect(run(count, { value: 11 }).failures[0]?.reason).toBe(
      'Out of range: 11, expected at most 10',
    )
    expect(run(count, { value: 10 }).failures).toEqual([])
  })

  it('accepts typed text and rejects what is not a number', () => {
    expect(run(temp, { value: '4,5' }).isValid).toBe(true)
    expect(run(temp, { value: '4,5' }).failures).toEqual([])
    expect(run(temp, { value: 'warm' }).problems[0]?.message).toBe('Enter a number.')
  })

  it('needs a photo and a comment on failure when the field asks', () => {
    const strict = { ...temp, photoRequiredOnFailure: true }
    const messages = run(strict, { value: 9 }).problems.map((p) => p.message)
    expect(messages).toEqual([
      "'Fridge' failed: add a photo as evidence.",
      "'Fridge' failed: describe the corrective action taken.",
    ])
    expect(run(strict, { value: 9, comment: 'Moved stock', photoIds: [GUID] }).isValid).toBe(true)
  })

  it('does not need a comment when the field does not require one', () => {
    const lax = { ...temp, commentRequiredOnFailure: false }
    expect(run(lax, { value: 9 }).isValid).toBe(true)
  })

  it('does not demand evidence while drafting', () => {
    const result = run(temp, { value: 9 }, false)
    expect(result.isValid).toBe(true)
    expect(result.failures).toHaveLength(1)
  })

  it('treats 0 as an answer', () => {
    expect(isAnswered(temp, { value: 0 })).toBe(true)
  })
})

describe('Text and LongText', () => {
  it('requires non-blank text', () => {
    const f = field({ key: 'x', type: 'Text' })
    expect(isAnswered(f, { value: '   ' })).toBe(false)
    expect(run(f, { value: '  ' }).problems).toHaveLength(1)
    expect(run(f, { value: 'ok' }).isValid).toBe(true)
  })
  it('limits the length by type', () => {
    expect(
      run(field({ key: 'x', type: 'Text' }), { value: 'a'.repeat(501) }).problems[0]?.message,
    ).toBe('Text is limited to 500 characters.')
    expect(run(field({ key: 'x', type: 'LongText' }), { value: 'a'.repeat(501) }).isValid).toBe(
      true,
    )
    expect(
      run(field({ key: 'x', type: 'LongText' }), { value: 'a'.repeat(5001) }).problems[0]?.message,
    ).toBe('Text is limited to 5000 characters.')
  })
})

describe('SingleSelect and MultiSelect', () => {
  const options = [
    { value: 'ok', label: 'Clean', isFailure: false },
    { value: 'bad', label: 'Dirty', isFailure: true },
    { value: 'meh', label: 'Greasy', isFailure: true },
  ]

  it('single select fails on a failure option', () => {
    const f = field({ key: 's', type: 'SingleSelect', options })
    expect(run(f, { value: 'ok' }).failures).toEqual([])
    expect(run(f, { value: 'bad' }).failures[0]?.reason).toBe("'Dirty' was selected")
  })

  it('single select rejects unknown options', () => {
    const f = field({ key: 's', type: 'SingleSelect', options })
    expect(run(f, { value: 'nope' }).problems[0]?.message).toBe('Choose one of the listed options.')
    expect(run(f, { value: 4 }).problems[0]?.message).toBe('Choose an option.')
  })

  it('multi select lists every failing option', () => {
    const f = field({ key: 'm', type: 'MultiSelect', options })
    expect(run(f, { value: ['ok'] }).failures).toEqual([])
    expect(run(f, { value: ['ok', 'bad', 'meh'] }).failures[0]?.reason).toBe(
      "'Dirty', 'Greasy' selected",
    )
  })

  it('multi select needs at least one when required and only listed options', () => {
    const f = field({ key: 'm', type: 'MultiSelect', options })
    expect(run(f, { value: [] }).problems[0]?.message).toBe("'m' is required.")
    expect(run(f, { value: ['x'] }).problems[0]?.message).toBe('Choose only the listed options.')
    // Like the domain, anything but a non-empty array counts as not answered.
    expect(run(f, { value: 'ok' }).problems[0]?.message).toBe("'m' is required.")
  })
})

describe('Date, Time and Signature', () => {
  it('validates dates', () => {
    const f = field({ key: 'd', type: 'Date' })
    expect(run(f, { value: '2026-11-07' }).isValid).toBe(true)
    expect(run(f, { value: '2026-02-30' }).problems[0]?.message).toBe('Enter a valid date.')
    expect(run(f, { value: '07/11/2026' }).problems[0]?.message).toBe('Enter a valid date.')
  })

  it('validates times', () => {
    const f = field({ key: 't', type: 'Time' })
    expect(run(f, { value: '09:30' }).isValid).toBe(true)
    expect(run(f, { value: '24:00' }).problems[0]?.message).toBe('Enter a valid time.')
    expect(run(f, { value: '9:30' }).problems[0]?.message).toBe('Enter a valid time.')
  })

  it('needs a signature id', () => {
    const f = field({ key: 'sig', type: 'Signature' })
    expect(run(f, { value: GUID }).isValid).toBe(true)
    expect(run(f, { value: 'scribble' }).problems[0]?.message).toBe('Add a signature.')
    expect(run(f, undefined).problems[0]?.message).toBe("'sig' is required.")
  })
})

describe('Photo and Instruction', () => {
  it('a photo field is answered by its photos', () => {
    const f = field({ key: 'p', type: 'Photo' })
    expect(run(f, { photoIds: [] }).problems).toHaveLength(1)
    expect(run(f, { photoIds: [GUID] }).isValid).toBe(true)
  })

  it('instructions are never asked for', () => {
    const f = field({ key: 'i', type: 'Instruction' })
    expect(run(f, undefined).isValid).toBe(true)
    expect(run(f, { value: 'x' }).problems[0]?.message).toBe(
      'This field does not exist on the task.',
    )
  })
})

describe('whole-task evaluation', () => {
  it('flags answers for unknown fields', () => {
    const result = evaluateAnswers(
      [field({ key: 'a', type: 'Text' })],
      { zzz: { value: 'x' } },
      false,
    )
    expect(result.problems).toEqual([
      { fieldKey: 'zzz', message: 'This field does not exist on the task.' },
    ])
  })

  it('limits comments and photos per answer', () => {
    const f = field({ key: 'a', type: 'Text' })
    expect(run(f, { value: 'x', comment: 'c'.repeat(2001) }).problems[0]?.message).toBe(
      'Comments are limited to 2000 characters.',
    )
    expect(run(f, { value: 'x', photoIds: Array(11).fill(GUID) }).problems[0]?.message).toBe(
      'Attach at most 10 photos.',
    )
  })

  it('skips unanswered optional fields and reports each required one while submitting', () => {
    const fields = [
      field({ key: 'a', type: 'Text', label: 'A' }),
      field({ key: 'b', type: 'Text', label: 'B', required: false }),
      field({ key: 'c', type: 'Number', label: 'C' }),
    ]
    const result = evaluateAnswers(fields, {}, true)
    expect(result.problems.map((p) => p.fieldKey)).toEqual(['a', 'c'])
    expect(evaluateAnswers(fields, {}, false).problems).toEqual([])
  })
})
