import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { recordBodies, signIn, url } from '@/test/settingsFixtures'

const ORGANIZATION = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Acme Kitchens',
  slug: 'acme',
  defaultTimeZone: 'Europe/London',
}

function serveOrganization() {
  server.use(http.get(url('/organizations/current'), () => HttpResponse.json(ORGANIZATION)))
}

function sectionLinks() {
  return within(screen.getByRole('navigation', { name: 'Settings' }))
    .getAllByRole('link')
    .map((l) => l.textContent)
}

describe('settings layout', () => {
  it('lands on the organization and lists every section for a full-access user', async () => {
    signIn()
    serveOrganization()
    const { router } = await renderApp('/settings')

    expect(await screen.findByRole('heading', { name: 'Settings' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/settings/organization')
    expect(sectionLinks()).toEqual([
      'Organization',
      'Sites',
      'Regions',
      'Departments',
      'Tags',
      'Members',
      'Roles',
    ])
  })

  it('shows only the sections the permissions open', async () => {
    signIn(['sites.view'])
    serveOrganization()
    await renderApp('/settings')

    await screen.findByRole('navigation', { name: 'Settings' })
    expect(sectionLinks()).toEqual(['Organization', 'Sites', 'Regions', 'Departments'])
  })

  it('shows a plain member only the organization', async () => {
    signIn(['tasks.view'])
    serveOrganization()
    await renderApp('/settings')

    await screen.findByRole('navigation', { name: 'Settings' })
    expect(sectionLinks()).toEqual(['Organization'])
  })

  it.each(['/settings/members', '/settings/roles', '/settings/tags', '/settings/sites'])(
    'refuses %s opened by address without its permission',
    async (path) => {
      signIn(['tasks.view'])
      await renderApp(path)

      expect(
        await screen.findByText(/do not have permission to view this section/),
      ).toBeInTheDocument()
    },
  )

  it('navigates between sections', async () => {
    signIn()
    serveOrganization()
    server.use(http.get(url('/tags'), () => HttpResponse.json([])))
    const user = userEvent.setup()
    await renderApp('/settings/organization')

    await user.click(
      within(await screen.findByRole('navigation', { name: 'Settings' })).getByRole('link', {
        name: 'Tags',
      }),
    )
    expect(await screen.findByText('No tags yet.')).toBeInTheDocument()
  })
})

describe('organization', () => {
  it('saves the name and default time zone, and updates the name in the app', async () => {
    signIn()
    serveOrganization()
    const bodies = recordBodies('put', '/organizations/current', (body) =>
      HttpResponse.json({ ...ORGANIZATION, ...(body as object) }),
    )
    const user = userEvent.setup()
    const { auth } = await renderApp('/settings/organization')

    const name = await screen.findByLabelText('Name')
    expect(name).toHaveValue('Acme Kitchens')
    expect(screen.getByText('acme')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled()

    await user.clear(name)
    await user.type(name, 'Acme Group')
    await user.type(screen.getByPlaceholderText(/Search, for example/), 'berlin')
    await user.click(await screen.findByRole('option', { name: 'Europe/Berlin' }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({ name: 'Acme Group', defaultTimeZone: 'Europe/Berlin' })
    await waitFor(() =>
      expect(auth.store.getState().session?.organization?.name).toBe('Acme Group'),
    )
  })

  it('shows validation and server errors', async () => {
    signIn()
    serveOrganization()
    server.use(
      http.put(url('/organizations/current'), () =>
        HttpResponse.json(
          {
            title: 'Bad',
            status: 400,
            code: 'validation.failed',
            detail: 'Invalid.',
            errors: {
              DefaultTimeZone: ['Time zone must be a valid IANA id such as Europe/London.'],
            },
          },
          { status: 400 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/organization')

    const name = await screen.findByLabelText('Name')
    await user.clear(name)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('Enter a name.')).toBeInTheDocument()

    await user.type(name, 'Acme Group')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(
      await screen.findByText('Time zone must be a valid IANA id such as Europe/London.'),
    ).toBeInTheDocument()
  })

  it('is read-only without organization.manage', async () => {
    signIn(['tasks.view'])
    serveOrganization()
    await renderApp('/settings/organization')

    expect(await screen.findByLabelText('Name')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument()
    expect(screen.getByText('Europe/London')).toBeInTheDocument()
  })
})
