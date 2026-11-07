import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { department, recordBodies, region, signIn, url } from '@/test/settingsFixtures'

describe('regions', () => {
  it('creates a region with an optional description', async () => {
    signIn()
    server.use(http.get(url('/regions'), () => HttpResponse.json([region({ name: 'North' })])))
    const bodies = recordBodies('post', '/regions', (body) =>
      HttpResponse.json({ id: 'r2', ...(body as object) }, { status: 201 }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/regions')

    await user.click(await screen.findByRole('button', { name: 'Add region' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add region' })
    await user.type(within(dialog).getByLabelText('Name'), 'South')
    await user.click(within(dialog).getByRole('button', { name: 'Add region' }))

    await waitFor(() => expect(bodies).toEqual([{ name: 'South', description: null }]))
  })

  it('explains that a region with sites cannot be deleted', async () => {
    signIn()
    server.use(
      http.get(url('/regions'), () => HttpResponse.json([region({ id: 'r1', name: 'North' })])),
      http.delete(url('/regions/r1'), () =>
        HttpResponse.json(
          problem(409, 'region.has_sites', 'Move or delete the sites in this region first.'),
          { status: 409 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/regions')

    await user.click(await screen.findByRole('button', { name: 'Delete region North' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete region' }))

    expect(
      await within(dialog).findByText('Move or delete the sites in this region first.'),
    ).toBeInTheDocument()
    expect(screen.getByText('North')).toBeInTheDocument()
  })

  it('rejects an over-long description before sending', async () => {
    signIn()
    server.use(http.get(url('/regions'), () => HttpResponse.json([])))
    const bodies = recordBodies('post', '/regions')
    const user = userEvent.setup()
    await renderApp('/settings/regions')

    await user.click(await screen.findByRole('button', { name: 'Add region' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Name'), 'South')
    await user.click(within(dialog).getByLabelText('Description'))
    await user.paste('x'.repeat(501))
    await user.click(within(dialog).getByRole('button', { name: 'Add region' }))

    expect(await within(dialog).findByText('The description is too long.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })
})

describe('departments', () => {
  it('renames a department and maps a duplicate name to the field', async () => {
    signIn()
    server.use(
      http.get(url('/departments'), () =>
        HttpResponse.json([department({ id: 'd1', name: 'Kitchen' })]),
      ),
      http.put(url('/departments/d1'), () =>
        HttpResponse.json(
          problem(409, 'department.name_taken', 'A department with this name already exists.'),
          { status: 409 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/departments')

    await user.click(await screen.findByRole('button', { name: 'Edit department Kitchen' }))
    const dialog = await screen.findByRole('dialog')
    await user.clear(within(dialog).getByLabelText('Name'))
    await user.type(within(dialog).getByLabelText('Name'), 'Bar')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(
      await within(dialog).findByText('A department with this name already exists.'),
    ).toBeInTheDocument()
  })

  it('deletes a department after confirmation', async () => {
    signIn()
    let items = [department({ id: 'd1', name: 'Kitchen' })]
    server.use(
      http.get(url('/departments'), () => HttpResponse.json(items)),
      http.delete(url('/departments/d1'), () => {
        items = []
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/departments')

    await user.click(await screen.findByRole('button', { name: 'Delete department Kitchen' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Delete department',
      }),
    )

    expect(await screen.findByText('No departments yet.')).toBeInTheDocument()
  })
})
