import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { signIn, url } from '@/test/settingsFixtures'
import { librarySummary, libraryTemplate, template } from '@/test/templateFixtures'

// Each test renders the whole app and lazy-loads route chunks.
vi.setConfig({ testTimeout: 20_000 })

const FRIDGE = librarySummary()
const OPENING = librarySummary({
  key: 'opening',
  name: 'Opening checklist',
  category: 'Daily',
  description: 'Open the venue safely.',
  kind: 'Checklist',
  fieldCount: 6,
})

function serveLibrary() {
  server.use(
    http.get(url('/templates/library'), () => HttpResponse.json([FRIDGE, OPENING])),
    http.get(url('/templates/library/fridge-temps'), () => HttpResponse.json(libraryTemplate())),
    http.get(url('/tags'), () => HttpResponse.json([])),
  )
}

describe('template library', () => {
  it('groups built-in templates by category and filters them', async () => {
    signIn()
    serveLibrary()
    const user = userEvent.setup()
    await renderApp('/settings/templates/library')

    const food = await screen.findByRole('region', { name: 'Food safety' })
    expect(within(food).getByText('Fridge temperature log')).toBeInTheDocument()
    expect(within(food).getByText('Record log')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Daily' })).toBeInTheDocument()
    expect(screen.getByText('6 fields')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Search library'), 'opening')
    expect(screen.queryByText('Fridge temperature log')).not.toBeInTheDocument()
    expect(screen.getByText('Opening checklist')).toBeInTheDocument()
  })

  it('previews a template with its fields rendered', async () => {
    signIn()
    serveLibrary()
    const user = userEvent.setup()
    await renderApp('/settings/templates/library')

    await user.click(await screen.findByRole('button', { name: 'Preview Fridge temperature log' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('Fridge temperature')).toBeInTheDocument()
    expect(within(dialog).getByText(/Expected 0.5/)).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('copies a template and opens it in the editor', async () => {
    signIn()
    serveLibrary()
    let copied = false
    server.use(
      http.post(url('/templates/library/fridge-temps/copy'), () => {
        copied = true
        return HttpResponse.json(
          template({
            id: 'template-9',
            name: 'Fridge temperature log',
            kind: 'RecordLog',
            libraryKey: 'fridge-temps',
            fields: libraryTemplate().fields,
          }),
          { status: 201 },
        )
      }),
      http.get(url('/templates/template-9'), () =>
        HttpResponse.json(
          template({
            id: 'template-9',
            name: 'Fridge temperature log',
            kind: 'RecordLog',
            fields: libraryTemplate().fields,
          }),
        ),
      ),
    )
    const user = userEvent.setup()
    const { router } = await renderApp('/settings/templates/library')

    await user.click(
      await screen.findByRole('button', { name: 'Use template Fridge temperature log' }),
    )

    expect(await screen.findByRole('heading', { name: 'Edit template' })).toBeInTheDocument()
    expect(copied).toBe(true)
    expect(router.state.location.pathname).toBe('/settings/templates/template-9')
    expect(await screen.findByLabelText('Name')).toHaveValue('Fridge temperature log')
    expect(screen.getByRole('listitem', { name: 'Field 1' })).toBeInTheDocument()
  })

  it('can copy from inside the preview', async () => {
    signIn()
    serveLibrary()
    server.use(
      http.post(url('/templates/library/fridge-temps/copy'), () =>
        HttpResponse.json(template({ id: 'template-9' }), { status: 201 }),
      ),
      http.get(url('/templates/template-9'), () =>
        HttpResponse.json(template({ id: 'template-9' })),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates/library')

    await user.click(await screen.findByRole('button', { name: 'Preview Fridge temperature log' }))
    const dialog = await screen.findByRole('dialog')
    await within(dialog).findByText('Fridge temperature')
    await user.click(within(dialog).getByRole('button', { name: 'Use this template' }))

    expect(await screen.findByRole('heading', { name: 'Edit template' })).toBeInTheDocument()
  })

  it('shows why a copy failed and stays on the library', async () => {
    signIn()
    serveLibrary()
    server.use(
      http.post(url('/templates/library/fridge-temps/copy'), () =>
        HttpResponse.json(problem(409, 'template.name_taken', 'That name is already in use.'), {
          status: 409,
        }),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/templates/library')

    await user.click(
      await screen.findByRole('button', { name: 'Use template Fridge temperature log' }),
    )
    expect(await screen.findByText('That name is already in use.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Template library' })).toBeInTheDocument()
  })

  it('lets viewers browse and preview but not copy', async () => {
    signIn(['templates.view'])
    serveLibrary()
    const user = userEvent.setup()
    await renderApp('/settings/templates/library')

    await screen.findByText('Fridge temperature log')
    expect(screen.queryByRole('button', { name: /Use template/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Preview Fridge temperature log' }))
    const dialog = await screen.findByRole('dialog')
    await within(dialog).findByText('Fridge temperature')
    expect(
      within(dialog).queryByRole('button', { name: 'Use this template' }),
    ).not.toBeInTheDocument()
  })
})
