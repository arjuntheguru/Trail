import { describe, expect, it } from 'vitest'

import { formatDateTime, fromLocalInput, isPastLocalInput, toLocalInput } from './dates'

describe('site time zone conversion', () => {
  it('reads a wall-clock input in the zone of the site', () => {
    // London is on GMT in winter and BST (UTC+1) in summer.
    expect(fromLocalInput('2099-01-15T09:00', 'Europe/London')).toBe('2099-01-15T09:00:00.000Z')
    expect(fromLocalInput('2099-06-15T09:00', 'Europe/London')).toBe('2099-06-15T08:00:00.000Z')
    expect(fromLocalInput('2099-06-15T09:00', 'America/New_York')).toBe('2099-06-15T13:00:00.000Z')
    expect(fromLocalInput('2099-06-15T09:00', 'Asia/Kolkata')).toBe('2099-06-15T03:30:00.000Z')
  })

  it('round-trips through the input value', () => {
    for (const zone of ['Europe/London', 'America/New_York', 'Pacific/Auckland']) {
      const iso = fromLocalInput('2099-03-10T18:45', zone)
      expect(toLocalInput(iso, zone)).toBe('2099-03-10T18:45')
    }
  })

  it('shows an instant as the wall clock of the zone', () => {
    expect(toLocalInput('2099-06-15T08:00:00Z', 'Europe/London')).toBe('2099-06-15T09:00')
    expect(toLocalInput('2099-06-15T08:00:00Z', 'Asia/Tokyo')).toBe('2099-06-15T17:00')
  })

  it('resolves a time skipped by a clock change to just after the gap', () => {
    // 01:30 does not exist in London on 2099-03-29 (clocks go from 01:00 to 02:00).
    const iso = fromLocalInput('2099-03-29T01:30', 'Europe/London')
    expect(toLocalInput(iso, 'Europe/London')).toBe('2099-03-29T02:30')
  })

  it('formats an instant in the given zone', () => {
    expect(formatDateTime('2026-11-08T00:30:00Z', 'Asia/Tokyo')).toMatch(/9:30|09:30/)
    expect(formatDateTime('2026-11-08T00:30:00Z', 'America/Los_Angeles')).toMatch(/4:30|16:30/)
  })

  it('tells whether an input is in the past in that zone', () => {
    expect(isPastLocalInput('2020-01-01T00:00', 'Europe/London')).toBe(true)
    expect(isPastLocalInput('2099-01-01T00:00', 'Europe/London')).toBe(false)
  })
})
