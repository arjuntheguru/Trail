import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'

import { problem, sessionResponse } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const LOGIN = `${API}/api/v1/auth/login`

beforeEach(() => {
  // Not signed in: the cookie-based restore on start fails.
  server.use(
    http.post(`${API}/api/v1/auth/refresh`, () =>
      HttpResponse.json(problem(401, 'auth.invalid_refresh_token', 'No session'), { status: 401 }),
    ),
  )
})

async function openLogin(path = '/login') {
  const app = await renderApp(path)
  await screen.findByLabelText('Email')
  return { ...app, user: userEvent.setup() }
}

describe('login form', () => {
  it('validates before calling the API', async () => {
    let called = false
    server.use(
      http.post(LOGIN, () => {
        called = true
        return HttpResponse.json(sessionResponse())
      }),
    )
    const { user } = await openLogin()

    await user.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Email'), 'not-an-email')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(called).toBe(false)
  })

  it('shows the server error message for wrong credentials', async () => {
    server.use(
      http.post(LOGIN, () =>
        HttpResponse.json(
          problem(401, 'auth.invalid_credentials', 'The email or password is incorrect.'),
          { status: 401 },
        ),
      ),
    )
    const { user, auth } = await openLogin()

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'wrong-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The email or password is incorrect.',
    )
    expect(auth.store.getState().status).toBe('anonymous')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled()
  })

  it('maps ProblemDetails validation errors onto the matching fields', async () => {
    server.use(
      http.post(LOGIN, () =>
        HttpResponse.json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { Email: ['Email is not allowed.'], Other: ['Something else is wrong.'] },
          },
          { status: 400 },
        ),
      ),
    )
    const { user } = await openLogin()

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Email is not allowed.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Something else is wrong.')).toBeInTheDocument() // no field: form-level
  })

  it('shows a friendly message when the API cannot be reached', async () => {
    server.use(http.post(LOGIN, () => HttpResponse.error()))
    const { user } = await openLogin()

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot reach the server')
  })

  it('disables the button while signing in, then lands on the page the user was heading to', async () => {
    let release: () => void = () => {}
    const gate = new Promise<void>((resolve) => (release = resolve))
    server.use(
      http.post(LOGIN, async () => {
        await gate
        return HttpResponse.json(sessionResponse())
      }),
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
      ),
    )
    const { user, router } = await openLogin('/login?redirect=%2Fsettings')

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('button', { name: /Signing in/ })).toBeDisabled()
    release()

    expect(await screen.findByText('Coming soon.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/settings')
  })

  it('ignores an off-site redirect target', async () => {
    server.use(
      http.post(LOGIN, () => HttpResponse.json(sessionResponse())),
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
      ),
    )
    const { user, router } = await openLogin('/login?redirect=https%3A%2F%2Fevil.example%2F')

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
  })
})
