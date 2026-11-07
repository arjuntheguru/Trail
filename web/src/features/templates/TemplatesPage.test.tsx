import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { signIn, url } from '@/test/settingsFixtures'
import { serveTemplates, summary } from '@/test/templateFixtures'

vi.setConfig({ testTimeout: 20_000 })

const OPENING = summary({
  id: 'template-1',
  name: 'Opening checks',
  fieldCount: 4,
  version: 3,
  requiresApproval: true,
  tags: [{ id: 'tag-1', name: 'Morning', color: null }],
})
const FRIDGE = summary({ id: 'template-2', name: 'Fridge log', kind: 'RecordLog', fieldCount: 1 })
const RETIRED = summary({ id: 'template-3', name: 'Old checks', isActive: false })

describe('templates list', () => {
  it('shows kind, field count, version, tags and flags', async () => {
    signIn()
    serveTemplates([OPENING, FRIDGE])
    await renderApp('/settings/templates')

    expect(await screen.findByText('Opening checks')).toBeInTheDocument()
    expect(screen.getByText('4 fields · Version 3')).toBeInTheDocument()
    expect(screen.getByText('1 field · Version 1')).toBeInTheDocument()
    expect(screen.getByText('Record log', { selector: 'span' })).toBeInTheDocument()
    expect(screen.getByText('Needs approval')).toBeInTheDocument()
    expect(screen.getByText('Morning')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'New template' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Browse library' })).toBeInTheDocument()
  })

  it('filters by kind, status and search on the server', async () => {
    signIn()
    const queries = serveTemplates([OPENING, FRIDGE, RETIRED])
    const user = userEvent.setup()
    await renderApp('/settings/templates')

    expect(await screen.findByText('Opening checks')).toBeInTheDocument()
    expect(screen.queryByText('Old checks')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Status'), 'all')
    expect(await screen.findByText('Old checks')).toBeInTheDocument()
    expect(screen.getByText('Inactive')).toBeInTheDocument()
    expect(queries.at(-1)?.get('IncludeInactive')).toBe('true')

    await user.selectOptions(screen.getByLabelText('Kind'), 'RecordLog')
    await waitFor(() => expect(screen.queryByText('Opening checks')).not.toBeInTheDocument())
    expect(screen.getByText('Fridge log')).toBeInTheDocument()
    expect(queries.at(-1)?.get('Kind')).toBe('RecordLog')

    await user.selectOptions(screen.getByLabelText('Kind'), '')
    await user.type(screen.getByLabelText('Search templates'), 'fridge')
    await waitFor(() => expect(queries.at(-1)?.get('Search')).toBe('fridge'))
    await waitFor(() => expect(screen.queryByText('Opening checks')).not.toBeInTheDocument())
  })

  it('filters by tag', async () => {
    signIn()
    const queries = serveTemplates([OPENING])
    server.use(
      http.get(url('/tags'), () =>
        HttpResponse.json([{ id: 'tag-1', name: 'Morning', color: null }]),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates')

    await screen.findByText('Opening checks')
    await screen.findByRole('option', { name: 'Morning' })
    await user.selectOptions(screen.getByLabelText('Tag'), 'tag-1')
    await waitFor(() => expect(queries.at(-1)?.get('TagId')).toBe('tag-1'))
  })

  it('shows an empty state', async () => {
    signIn()
    serveTemplates([])
    await renderApp('/settings/templates')
    expect(await screen.findByText(/No templates found/)).toBeInTheDocument()
  })

  it('deactivates after confirming, then activates again', async () => {
    signIn()
    let active = true
    server.use(
      http.get(url('/templates'), ({ request }) => {
        const all = new URL(request.url).searchParams.get('IncludeInactive') === 'true'
        const items = active || all ? [{ ...OPENING, isActive: active }] : []
        return HttpResponse.json({ items, page: 1, pageSize: 25, totalCount: items.length })
      }),
      http.get(url('/tags'), () => HttpResponse.json([])),
      http.post(url('/templates/template-1/deactivate'), () => {
        active = false
        return new HttpResponse(null, { status: 204 })
      }),
      http.post(url('/templates/template-1/activate'), () => {
        active = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates')

    await user.click(
      await screen.findByRole('button', { name: 'Deactivate template Opening checks' }),
    )
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Deactivate template' }))

    // The default filter hides inactive templates, so the list empties.
    expect(await screen.findByText(/No templates found/)).toBeInTheDocument()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Status'), 'all')
    await user.click(
      await screen.findByRole('button', { name: 'Activate template Opening checks' }),
    )
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Activate template',
      }),
    )
    expect(
      await screen.findByRole('button', { name: 'Deactivate template Opening checks' }),
    ).toBeInTheDocument()
  })

  it('asks before deleting, then removes the template', async () => {
    signIn()
    let items = [OPENING, FRIDGE]
    server.use(
      http.get(url('/templates'), () =>
        HttpResponse.json({ items, page: 1, pageSize: 25, totalCount: items.length }),
      ),
      http.get(url('/tags'), () => HttpResponse.json([])),
      http.delete(url('/templates/template-2'), () => {
        items = [OPENING]
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates')

    await user.click(await screen.findByRole('button', { name: 'Delete template Fridge log' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/"Fridge log" will be removed/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Delete template' }))

    await waitFor(() => expect(screen.queryByText('Fridge log')).not.toBeInTheDocument())
    expect(screen.getByText('Opening checks')).toBeInTheDocument()
  })

  it('shows the server refusal when a schedule still uses the template', async () => {
    signIn()
    serveTemplates([OPENING])
    server.use(
      http.delete(url('/templates/template-1'), () =>
        HttpResponse.json(
          problem(
            409,
            'template.in_use',
            'A schedule still uses this template. Delete the schedule first.',
          ),
          { status: 409 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates')

    await user.click(await screen.findByRole('button', { name: 'Delete template Opening checks' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete template' }))

    expect(
      await within(dialog).findByText(
        'A schedule still uses this template. Delete the schedule first.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Opening checks')).toBeInTheDocument()
  })

  it('offers no editing to someone who can only view templates', async () => {
    signIn(['templates.view'])
    serveTemplates([OPENING])
    await renderApp('/settings/templates')

    expect(await screen.findByText('Opening checks')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'New template' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Edit template/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Delete template/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /activate template/i })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Browse library' })).toBeInTheDocument()
  })

  it('refuses people without templates.view', async () => {
    signIn(['tasks.view'])
    await renderApp('/settings/templates')
    expect(await screen.findByText(/do not have permission/)).toBeInTheDocument()
  })
})
