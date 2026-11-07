import { describe, expect, it, vi } from 'vitest'

import { applyFieldErrors, errorMessage, toApiError } from './problem'

describe('toApiError', () => {
  it('reads code, detail and validation errors from ProblemDetails', () => {
    const error = toApiError(400, {
      title: 'Bad',
      detail: 'Nope',
      code: 'auth.invalid_token',
      errors: { Email: ['Bad email.'], Junk: 'not-an-array' },
    })
    expect(error).toMatchObject({ status: 400, code: 'auth.invalid_token', detail: 'Nope' })
    expect(error.errors).toEqual({ Email: ['Bad email.'] })
  })

  it('tolerates empty and non-object bodies', () => {
    expect(toApiError(500, undefined).status).toBe(500)
    expect(toApiError(502, '<html>').code).toBeUndefined()
  })
})

describe('errorMessage', () => {
  it('has friendly text for network, throttling and server failures', () => {
    expect(errorMessage(toApiError(0, undefined))).toMatch(/Cannot reach the server/)
    expect(errorMessage(toApiError(429, undefined))).toMatch(/Too many/)
    expect(errorMessage(toApiError(500, { detail: 'stack trace' }))).not.toMatch(/stack trace/)
    expect(errorMessage(toApiError(401, { detail: 'Wrong.' }))).toBe('Wrong.')
  })
})

describe('applyFieldErrors', () => {
  it('matches PascalCase server properties to camelCase fields and returns the rest', () => {
    const setError = vi.fn()
    const rest = applyFieldErrors(
      toApiError(400, { errors: { NewPassword: ['Too short.'], Unknown: ['Other.'] } }),
      ['newPassword'],
      setError,
    )
    expect(setError).toHaveBeenCalledWith('newPassword', { type: 'server', message: 'Too short.' })
    expect(rest).toEqual(['Other.'])
  })

  it('falls back to the error message when there are no field errors', () => {
    expect(
      applyFieldErrors(toApiError(409, { detail: 'Email taken.' }), ['email'], vi.fn()),
    ).toEqual(['Email taken.'])
  })
})
