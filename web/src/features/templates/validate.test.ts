import { describe, expect, it } from 'vitest'

import { templateField as field } from '@/test/templateFixtures'

import { isValidKey, validateFields } from './validate'

const messages = (...fields: Parameters<typeof validateFields>[0]) =>
  validateFields(fields).map((i) => i.message)

const ok = field({ key: 'done', type: 'Checkbox' })

describe('validateFields', () => {
  it('accepts a minimal valid list', () => {
    expect(validateFields([ok])).toEqual([])
  })

  it('needs at least one field', () => {
    expect(messages()).toContain('A template needs at least one field.')
  })

  it('needs at least one answerable field', () => {
    expect(messages(field({ key: 'read', type: 'Instruction' }))).toEqual([
      'A template needs at least one field that can be answered.',
    ])
    expect(messages(field({ key: 'read', type: 'Instruction' }), ok)).toEqual([])
  })

  it('allows at most 200 fields', () => {
    const many = Array.from({ length: 201 }, (_, i) => field({ key: `f${i}`, type: 'Checkbox' }))
    expect(messages(...many)).toEqual(['A template can have at most 200 fields.'])
    expect(messages(...many.slice(0, 200))).toEqual([])
  })

  it('reports duplicate keys once and points at every use', () => {
    const issues = validateFields([ok, field({ key: 'x', type: 'Text' }), { ...ok }])
    expect(issues).toEqual([
      { message: "Field key 'done' is used more than once.", indexes: [0, 2] },
    ])
  })

  it('compares keys case-sensitively, like the server', () => {
    expect(messages(ok, field({ key: 'DONE', type: 'Checkbox' }))).toEqual([])
  })

  describe('keys', () => {
    it.each([
      ['done', true],
      ['Walk-in_fridge2', true],
      ['a'.repeat(40), true],
      ['a'.repeat(41), false],
      ['', false],
      ['   ', false],
      ['has space', false],
      ['dot.ted', false],
      ['café', false],
    ])('%j -> %s', (key, valid) => {
      expect(isValidKey(key)).toBe(valid)
      expect(validateFields([field({ key, type: 'Checkbox' })]).length === 0).toBe(valid)
    })

    it('names the field by its label, falling back to the key', () => {
      expect(messages(field({ key: 'bad key', label: 'Fridge', type: 'Checkbox' }))).toEqual([
        "Field 'Fridge': key must be 1-40 letters, digits, '_' or '-'.",
      ])
    })
  })

  describe('labels', () => {
    it('are required', () => {
      expect(messages(field({ key: 'a', label: '  ', type: 'Checkbox' }))).toEqual([
        "Field 'a': label is required (max 300 characters).",
      ])
    })

    it('are limited to 300 characters', () => {
      expect(messages(field({ key: 'a', label: 'x'.repeat(300), type: 'Checkbox' }))).toEqual([])
      expect(messages(field({ key: 'a', label: 'x'.repeat(301), type: 'Checkbox' }))).toHaveLength(
        1,
      )
    })
  })

  describe('ranges', () => {
    it('allows min <= max on number and temperature fields', () => {
      expect(messages(field({ key: 't', type: 'Temperature', min: 0, max: 5 }))).toEqual([])
      expect(messages(field({ key: 't', type: 'Number', min: 3, max: 3 }))).toEqual([])
      expect(messages(field({ key: 't', type: 'Number', min: 3 }))).toEqual([])
      expect(messages(field({ key: 't', type: 'Number', max: -3 }))).toEqual([])
    })

    it('rejects min greater than max', () => {
      expect(
        messages(field({ key: 't', label: 'Temp', type: 'Temperature', min: 6, max: 5 })),
      ).toEqual(["Field 'Temp': minimum cannot be greater than maximum."])
    })

    it('rejects a range on other types, including zero', () => {
      expect(messages(field({ key: 't', label: 'Note', type: 'Text', min: 0 }))).toEqual([
        "Field 'Note': only number and temperature fields have a range.",
      ])
      expect(messages(field({ key: 't', label: 'Note', type: 'Text', max: 1 }))).toHaveLength(1)
    })
  })

  describe('options', () => {
    const opt = (value: string, label = value) => ({ value, label, isFailure: false })
    const select = (options: ReturnType<typeof opt>[]) =>
      messages(field({ key: 's', label: 'Choice', type: 'SingleSelect', options }))

    it('need 2 to 50 on select fields', () => {
      const count = ["Field 'Choice': give between 2 and 50 options."]
      expect(select([opt('a')])).toEqual(count)
      expect(select([])).toEqual(count)
      expect(select([opt('a'), opt('b')])).toEqual([])
      const fifty = Array.from({ length: 50 }, (_, i) => opt(`o${i}`))
      expect(select(fifty)).toEqual([])
      expect(select([...fifty, opt('extra')])).toEqual(count)
    })

    it('need a value and a label each', () => {
      const every = ["Field 'Choice': every option needs a value and a label."]
      expect(select([opt('a'), { value: 'b', label: ' ', isFailure: false }])).toEqual(every)
      expect(select([opt('a'), { value: '', label: 'B', isFailure: false }])).toEqual(every)
    })

    it('need unique values', () => {
      expect(select([opt('a'), opt('a', 'Other')])).toEqual([
        "Field 'Choice': option values must be unique.",
      ])
    })

    it('are refused on other types', () => {
      expect(
        messages(field({ key: 's', label: 'Note', type: 'Text', options: [opt('a')] })),
      ).toEqual(["Field 'Note': only select fields have options."])
    })
  })

  describe('expected answer', () => {
    it('is allowed on yes/no, including false', () => {
      expect(messages(field({ key: 'y', type: 'YesNo', expectedAnswer: true }))).toEqual([])
      expect(messages(field({ key: 'y', type: 'YesNo', expectedAnswer: false }))).toEqual([])
    })

    it('is refused on other types, including false', () => {
      expect(
        messages(field({ key: 'c', label: 'Done', type: 'Checkbox', expectedAnswer: false })),
      ).toEqual(["Field 'Done': only yes/no fields have an expected answer."])
    })
  })

  it('collects every problem in server order', () => {
    const issues = validateFields([
      field({ key: 'a', label: 'One', type: 'Number', min: 5, max: 1 }),
      field({ key: 'a', label: '', type: 'Checkbox' }),
    ])
    expect(issues.map((i) => i.message)).toEqual([
      "Field key 'a' is used more than once.",
      "Field 'One': minimum cannot be greater than maximum.",
      "Field 'a': label is required (max 300 characters).",
    ])
    expect(issues.map((i) => i.indexes)).toEqual([[0, 1], [0], [1]])
  })
})
