import { describe, expect, it } from 'vitest'

import { templateField } from '@/test/templateFixtures'

import {
  addField,
  addOption,
  draftIssues,
  moveField,
  moveOption,
  removeField,
  removeOption,
  snapshotOf,
  toDraftField,
  toTemplateField,
  updateField,
  updateOption,
  type DraftField,
} from './draft'
import { slugify, uniqueKey } from './keys'

const build = (...types: Parameters<typeof addField>[1][]) =>
  types.reduce<DraftField[]>((fields, type) => addField(fields, type), [])

describe('slugify / uniqueKey', () => {
  it('makes a key from free text', () => {
    expect(slugify('Walk-in fridge temp (°C)')).toBe('walk_in_fridge_temp_c')
    expect(slugify('  Café  crème ')).toBe('cafe_creme')
    expect(slugify('')).toBe('field')
    expect(slugify('???')).toBe('field')
    expect(slugify('???', 'option')).toBe('option')
  })

  it('keeps keys within 40 characters without a trailing underscore', () => {
    const key = slugify('word '.repeat(20))
    expect(key.length).toBeLessThanOrEqual(40)
    expect(key.endsWith('_')).toBe(false)
  })

  it('numbers a clash and stays within the limit', () => {
    expect(uniqueKey('temp', ['other'])).toBe('temp')
    expect(uniqueKey('temp', ['temp'])).toBe('temp_2')
    expect(uniqueKey('temp', ['temp', 'temp_2'])).toBe('temp_3')
    const long = 'a'.repeat(40)
    const next = uniqueKey(long, [long])
    expect(next).toHaveLength(40)
    expect(next.endsWith('_2')).toBe(true)
  })
})

describe('field list operations', () => {
  it('adds fields with the API defaults', () => {
    expect(build('Text')[0]).toMatchObject({
      type: 'Text',
      required: true,
      commentRequiredOnFailure: true,
      raiseIssueOnFailure: true,
      photoRequiredOnFailure: false,
    })
    expect(build('Instruction')[0]?.required).toBe(false)
  })

  it('starts select fields with two empty options', () => {
    expect(build('SingleSelect')[0]?.options).toHaveLength(2)
    expect(build('Text')[0]?.options).toHaveLength(0)
  })

  it('moves, clamps and removes', () => {
    const fields = build('Text', 'Number', 'Date')
    const [a, b, c] = fields.map((f) => f.id) as [string, string, string]
    expect(moveField(fields, c, -1).map((f) => f.id)).toEqual([a, c, b])
    expect(moveField(fields, a, -1).map((f) => f.id)).toEqual([a, b, c])
    expect(moveField(fields, c, 1).map((f) => f.id)).toEqual([a, b, c])
    expect(moveField(fields, a, 2).map((f) => f.id)).toEqual([b, c, a])
    expect(removeField(fields, b).map((f) => f.id)).toEqual([a, c])
  })

  it('generates the key from the label and keeps it unique', () => {
    let fields = build('Number', 'Number')
    const [a, b] = fields.map((f) => f.id) as [string, string]
    fields = updateField(fields, a, { label: 'Fridge temp' })
    fields = updateField(fields, b, { label: 'Fridge temp' })
    expect(fields.map((f) => f.key)).toEqual(['fridge_temp', 'fridge_temp_2'])
  })

  it('stops following the label once the key is edited', () => {
    let fields = build('Number')
    const id = fields[0]!.id
    fields = updateField(fields, id, { label: 'Fridge' })
    fields = updateField(fields, id, { key: 'my-key' })
    fields = updateField(fields, id, { label: 'Freezer' })
    expect(fields[0]).toMatchObject({ key: 'my-key', label: 'Freezer', keyTouched: true })
  })

  it('never rewrites the key of an existing field', () => {
    const draft = toDraftField(templateField({ key: 'old_key', label: 'Old', type: 'Text' }))
    const [next] = updateField([draft], draft.id, { label: 'Brand new label' })
    expect(next?.key).toBe('old_key')
  })

  it('gives a select field options when the type changes to one', () => {
    let fields = build('Text')
    fields = updateField(fields, fields[0]!.id, { type: 'MultiSelect' })
    expect(fields[0]?.options).toHaveLength(2)
  })
})

describe('option operations', () => {
  const select = () => build('SingleSelect')[0]!

  it('generates option values from labels, unique within the field', () => {
    let field = select()
    const [o1, o2] = field.options.map((o) => o.id) as [string, string]
    field = updateOption(field, o1, { label: 'In range' })
    field = updateOption(field, o2, { label: 'In range' })
    expect(field.options.map((o) => o.value)).toEqual(['in_range', 'in_range_2'])
  })

  it('keeps a hand-typed value', () => {
    let field = select()
    const id = field.options[0]!.id
    field = updateOption(field, id, { value: 'ok' })
    field = updateOption(field, id, { label: 'Everything fine' })
    expect(field.options[0]).toMatchObject({ value: 'ok', label: 'Everything fine' })
  })

  it('adds, moves and removes', () => {
    let field = addOption(select())
    expect(field.options).toHaveLength(3)
    const [a, b, c] = field.options.map((o) => o.id) as [string, string, string]
    field = moveOption(field, c, -1)
    expect(field.options.map((o) => o.id)).toEqual([a, c, b])
    field = removeOption(field, a)
    expect(field.options.map((o) => o.id)).toEqual([c, b])
  })
})

describe('toTemplateField', () => {
  it('round-trips an existing field', () => {
    const original = templateField({
      key: 'temp',
      label: 'Temp',
      type: 'Temperature',
      min: -2.5,
      max: 5,
      unit: '°F',
      helpText: 'Use the probe',
      section: 'Fridge',
      photoRequiredOnFailure: true,
      commentRequiredOnFailure: false,
      raiseIssueOnFailure: false,
    })
    expect(toTemplateField(toDraftField(original))).toEqual(original)
  })

  it('round-trips yes/no expectations and options', () => {
    const yes = templateField({ key: 'y', type: 'YesNo', expectedAnswer: false })
    expect(toTemplateField(toDraftField(yes)).expectedAnswer).toBe(false)
    const select = templateField({
      key: 's',
      type: 'SingleSelect',
      options: [
        { value: 'a', label: 'A', isFailure: false },
        { value: 'b', label: 'B', isFailure: true },
      ],
    })
    expect(toTemplateField(toDraftField(select))).toEqual(select)
  })

  it('drops settings that do not apply to the type', () => {
    let fields = build('Number')
    const id = fields[0]!.id
    fields = updateField(fields, id, { label: 'Count', min: '1', max: '9', unit: 'kg' })
    fields = updateField(fields, id, { type: 'Text' })
    expect(toTemplateField(fields[0]!)).toMatchObject({
      min: null,
      max: null,
      unit: null,
      options: [],
    })
  })

  it('accepts decimals typed with a comma', () => {
    const draft = { ...build('Number')[0]!, min: '1,5', max: ' 7 ' }
    expect(toTemplateField(draft)).toMatchObject({ min: 1.5, max: 7 })
  })

  it('flags range bounds that are not numbers', () => {
    const draft = { ...build('Temperature')[0]!, min: 'cold', max: '' }
    expect(draftIssues([draft])).toEqual([{ index: 0, message: 'Minimum must be a number.' }])
    expect(draftIssues([{ ...draft, type: 'Text' }])).toEqual([])
  })

  it('ignores client ids when comparing snapshots', () => {
    const f = templateField({ key: 'a', type: 'Text' })
    expect(snapshotOf([toDraftField(f)])).toBe(snapshotOf([toDraftField(f)]))
    expect(snapshotOf([toDraftField(f)])).not.toBe(snapshotOf([toDraftField({ ...f, label: 'x' })]))
  })
})
