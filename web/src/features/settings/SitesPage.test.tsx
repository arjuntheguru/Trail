import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { paged, recordBodies, region, signIn, site, url } from '@/test/settingsFixtures'

const NORTH = region({ id: 'region-north', name: 'North' })
const HARBOUR = site({
  id: 'site-1',
  name: 'Harbour Cafe',
  code: 'HC1',
  regionId: NORTH.id,
  regionName: 'North',
})
const QUAY = site({ id: 'site-2', name: 'Quay Bar', isActive: false })

function serveSites(items = [HARBOUR, QUAY]) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get(url('/sites'), ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      const active = params.get('IsActive')
      return HttpResponse.json(
        paged(items.filter((s) => active === null || String(s.isActive) === active)),
      )
    }),
    http.get(url('/regions'), () => HttpResponse.json([NORTH])),
  )
  return queries
}

describe('sites list', () => {
  it('shows sites with region, zone and inactive state, and filters by status', async () => {
    signIn()
    const queries = serveSites()
    const user = userEvent.setup()
    await renderApp('/settings/sites')

    expect(await screen.findByText('Harbour Cafe')).toBeInTheDocument()
    expect(screen.getByText('North · Europe/London')).toBeInTheDocument()
    expect(screen.getByText('Inactive', { selector: 'span' })).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Status'), 'inactive')
    await waitFor(() => expect(screen.queryByText('Harbour Cafe')).not.toBeInTheDocument())
    expect(screen.getByText('Quay Bar')).toBeInTheDocument()
    expect(queries.at(-1)?.get('IsActive')).toBe('false')
  })

  it('offers no editing to someone who can only view sites', async () => {
    signIn(['sites.view'])
    serveSites()
    await renderApp('/settings/sites')

    expect(await screen.findByText('Harbour Cafe')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Add site' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Delete site/ })).not.toBeInTheDocument()
  })

  it('deactivates and activates after confirmation', async () => {
    signIn()
    serveSites()
    const deactivate = recordBodies('post', '/sites/site-1/deactivate')
    const activate = recordBodies('post', '/sites/site-2/activate')
    const user = userEvent.setup()
    await renderApp('/settings/sites')

    await user.click(await screen.findByRole('button', { name: 'Deactivate site Harbour Cafe' }))
    await user.click(await screen.findByRole('button', { name: 'Deactivate site' }))
    await waitFor(() => expect(deactivate).toHaveLength(1))

    await user.click(await screen.findByRole('button', { name: 'Activate site Quay Bar' }))
    await user.click(await screen.findByRole('button', { name: 'Activate site' }))
    await waitFor(() => expect(activate).toHaveLength(1))
  })

  it('deletes after confirmation and shows a refusal inside the dialog', async () => {
    signIn()
    serveSites()
    server.use(
      http.delete(url('/sites/site-1'), () =>
        HttpResponse.json(problem(409, 'site.in_use', 'The site still has open tasks.'), {
          status: 409,
        }),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/sites')

    await user.click(await screen.findByRole('button', { name: 'Delete site Harbour Cafe' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete site' }))

    expect(await within(dialog).findByText('The site still has open tasks.')).toBeInTheDocument()
  })
})

describe('site form', () => {
  it('creates a site with a searched time zone, region, address and opening hours', async () => {
    signIn()
    serveSites([])
    const bodies = recordBodies('post', '/sites', (body) =>
      HttpResponse.json({ ...site(), ...(body as object) }, { status: 201 }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/sites/new')

    await user.type(await screen.findByLabelText('Name'), ' Old Dock ')
    await user.type(screen.getByLabelText('Code'), 'OD1')
    await user.selectOptions(screen.getByLabelText('Region'), 'region-north')

    await user.type(screen.getByPlaceholderText(/Search, for example/), 'lisbon')
    await user.click(await screen.findByRole('option', { name: 'Europe/Lisbon' }))
    expect(screen.getByTestId('selected-time-zone')).toHaveTextContent('Europe/Lisbon')

    await user.type(screen.getByLabelText('Address line 1'), '1 Wharf Road')
    await user.type(screen.getByLabelText('City'), 'Lisbon')
    await user.type(screen.getByLabelText('Country code'), 'pt')

    await user.click(screen.getByRole('checkbox', { name: 'Monday' }))
    await user.click(screen.getByRole('button', { name: 'Add site' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      name: 'Old Dock',
      code: 'OD1',
      regionId: 'region-north',
      timeZone: 'Europe/Lisbon',
      phone: null,
      email: null,
      address: {
        line1: '1 Wharf Road',
        line2: null,
        city: 'Lisbon',
        stateOrRegion: null,
        postalCode: null,
        countryCode: 'PT',
      },
      openingHours: [{ day: 'Monday', opens: '09:00:00', closes: '17:00:00' }],
    })
    // Back on the list afterwards.
    expect(await screen.findByRole('link', { name: 'Add site' })).toBeInTheDocument()
  })

  it('defaults the time zone to the browser zone', async () => {
    signIn()
    serveSites([])
    await renderApp('/settings/sites/new')

    await screen.findByLabelText('Name')
    expect(screen.getByTestId('selected-time-zone')).toHaveTextContent(
      Intl.DateTimeFormat().resolvedOptions().timeZone,
    )
  })

  it('validates required fields and half-filled addresses', async () => {
    signIn()
    serveSites([])
    const bodies = recordBodies('post', '/sites')
    const user = userEvent.setup()
    await renderApp('/settings/sites/new')

    await user.click(await screen.findByRole('button', { name: 'Add site' }))
    expect(await screen.findByText('Enter a name.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Name'), 'Old Dock')
    await user.type(screen.getByLabelText('City'), 'Lisbon')
    await user.type(screen.getByLabelText('Email'), 'nope')
    await user.click(screen.getByRole('button', { name: 'Add site' }))

    expect(await screen.findByText('Enter the first address line.')).toBeInTheDocument()
    expect(screen.getByText(/two-letter country code such as GB/)).toBeInTheDocument()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('maps a taken name and nested address errors onto their fields', async () => {
    signIn()
    serveSites([])
    let calls = 0
    recordBodies('post', '/sites', () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(
            problem(409, 'site.name_taken', 'A site with this name already exists.'),
            {
              status: 409,
            },
          )
        : HttpResponse.json(
            problem(400, 'validation.failed', 'Invalid.', {
              errors: { 'Address.PostalCode': ['Postal code is not valid.'] },
            }),
            { status: 400 },
          )
    })
    const user = userEvent.setup()
    await renderApp('/settings/sites/new')

    await user.type(await screen.findByLabelText('Name'), 'Harbour Cafe')
    await user.click(screen.getByRole('button', { name: 'Add site' }))
    expect(await screen.findByText('A site with this name already exists.')).toBeInTheDocument()
    expect(screen.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')

    await user.click(screen.getByRole('button', { name: 'Add site' }))
    expect(await screen.findByText('Postal code is not valid.')).toBeInTheDocument()
    expect(screen.getByLabelText('Postal code')).toHaveAttribute('aria-invalid', 'true')
  })

  it('loads a site for editing and saves the change', async () => {
    signIn()
    serveSites()
    server.use(http.get(url('/sites/site-1'), () => HttpResponse.json(HARBOUR)))
    const bodies = recordBodies('put', '/sites/site-1', (body) =>
      HttpResponse.json({ ...HARBOUR, ...(body as object) }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/sites/site-1')

    const name = await screen.findByLabelText('Name')
    expect(name).toHaveValue('Harbour Cafe')
    expect(screen.getByLabelText('Region')).toHaveValue('region-north')
    expect(screen.getByTestId('selected-time-zone')).toHaveTextContent('Europe/London')
    await user.clear(name)
    await user.type(name, 'Harbour Cafe Two')
    await user.click(screen.getByRole('button', { name: 'Save site' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      name: 'Harbour Cafe Two',
      code: 'HC1',
      regionId: 'region-north',
      timeZone: 'Europe/London',
      address: null,
      openingHours: [],
    })
  })

  it('says so when the site is not found', async () => {
    signIn()
    server.use(
      http.get(url('/sites/missing'), () =>
        HttpResponse.json(problem(404, 'site.not_found', 'Site not found.'), { status: 404 }),
      ),
      http.get(url('/regions'), () => HttpResponse.json([])),
    )
    await renderApp('/settings/sites/missing')

    expect(await screen.findByText('Site not found.')).toBeInTheDocument()
  })

  it('does not show the form without sites.manage', async () => {
    signIn(['sites.view'])
    await renderApp('/settings/sites/new')

    expect(await screen.findByText(/do not have permission/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Name')).not.toBeInTheDocument()
  })
})
