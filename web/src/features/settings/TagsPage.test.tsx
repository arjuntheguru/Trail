import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { recordBodies, signIn, url } from '@/test/settingsFixtures'

const BREAKFAST = { id: 'tag-1', name: 'Breakfast', color: '#1F8A70' }
const LUNCH = { id: 'tag-2', name: 'Lunch', color: null }

function serveTags(tags: object[] = [BREAKFAST, LUNCH]) {
  server.use(http.get(url('/tags'), () => HttpResponse.json(tags)))
}

describe('tags', () => {
  it('lists tags with their colour', async () => {
    signIn()
    serveTags()
    await renderApp('/settings/tags')

    expect(await screen.findByText('Breakfast')).toBeInTheDocument()
    expect(screen.getByText('Lunch')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit tag Breakfast' })).toBeInTheDocument()
  })

  it('creates a tag and shows it after the list reloads', async () => {
    signIn()
    let tags: object[] = [BREAKFAST]
    server.use(http.get(url('/tags'), () => HttpResponse.json(tags)))
    const bodies = recordBodies('post', '/tags', (body) => {
      tags = [...tags, { id: 'tag-9', ...(body as object) }]
      return HttpResponse.json({ id: 'tag-9', ...(body as object) }, { status: 201 })
    })
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Add tag' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add tag' })
    await user.type(within(dialog).getByLabelText('Name'), '  Dinner ')
    await user.type(within(dialog).getByLabelText('Colour'), '#AA00BB')
    await user.click(within(dialog).getByRole('button', { name: 'Add tag' }))

    expect(await screen.findByText('Dinner')).toBeInTheDocument()
    expect(bodies).toEqual([{ name: 'Dinner', color: '#AA00BB' }])
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('edits a tag, sending null when the colour is cleared', async () => {
    signIn()
    serveTags()
    const bodies = recordBodies('put', '/tags/tag-1', (body) =>
      HttpResponse.json({ id: 'tag-1', ...(body as object) }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Edit tag Breakfast' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit tag' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Breakfast')
    await user.clear(within(dialog).getByLabelText('Colour'))
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodies).toEqual([{ name: 'Breakfast', color: null }])
  })

  it('validates before sending', async () => {
    signIn()
    serveTags([])
    const bodies = recordBodies('post', '/tags')
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Add tag' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Add tag' }))
    expect(await within(dialog).findByText('Enter a name.')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Name'), 'Dinner')
    await user.type(within(dialog).getByLabelText('Colour'), 'red')
    await user.click(within(dialog).getByRole('button', { name: 'Add tag' }))
    expect(await within(dialog).findByText(/Use a hex colour/)).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('shows a taken name against the name field and keeps the dialog open', async () => {
    signIn()
    serveTags()
    recordBodies('post', '/tags', () =>
      HttpResponse.json(problem(409, 'tag.name_taken', 'A tag with this name already exists.'), {
        status: 409,
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Add tag' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Name'), 'Lunch')
    await user.click(within(dialog).getByRole('button', { name: 'Add tag' }))

    const message = await within(dialog).findByText('A tag with this name already exists.')
    expect(message).toHaveAttribute('role', 'alert')
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
  })

  it('maps 400 validation errors onto fields', async () => {
    signIn()
    serveTags()
    recordBodies('post', '/tags', () =>
      HttpResponse.json(
        problem(400, 'validation.failed', 'Invalid.', {
          errors: { Color: ['Color must be a hex value such as #1F8A70.'], Other: ['Odd.'] },
        }),
        { status: 400 },
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Add tag' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Name'), 'Dinner')
    await user.click(within(dialog).getByRole('button', { name: 'Add tag' }))

    expect(
      await within(dialog).findByText('Color must be a hex value such as #1F8A70.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByText('Odd.')).toBeInTheDocument()
  })

  it('asks before deleting, then removes the tag', async () => {
    signIn()
    let tags: object[] = [BREAKFAST, LUNCH]
    server.use(http.get(url('/tags'), () => HttpResponse.json(tags)))
    server.use(
      http.delete(url('/tags/tag-2'), () => {
        tags = [BREAKFAST]
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Delete tag Lunch' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/"Lunch" will be removed/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Delete tag' }))

    await waitFor(() => expect(screen.queryByText('Lunch')).not.toBeInTheDocument())
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('shows the server refusal inside the delete dialog', async () => {
    signIn()
    serveTags()
    server.use(
      http.delete(url('/tags/tag-1'), () =>
        HttpResponse.json(problem(409, 'tag.in_use', 'This tag is used by 3 templates.'), {
          status: 409,
        }),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/tags')

    await user.click(await screen.findByRole('button', { name: 'Delete tag Breakfast' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete tag' }))

    expect(await within(dialog).findByText('This tag is used by 3 templates.')).toBeInTheDocument()
    expect(screen.getByText('Breakfast')).toBeInTheDocument()
  })

  it('hides editing without the tags.manage permission', async () => {
    signIn(['templates.view'])
    serveTags()
    await renderApp('/settings/tags')

    expect(await screen.findByText('Breakfast')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add tag' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Edit tag/ })).not.toBeInTheDocument()
  })
})
