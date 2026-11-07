import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { recordBodies, signIn, url } from '@/test/settingsFixtures'
import { serveTemplates, template, templateField } from '@/test/templateFixtures'

vi.setConfig({ testTimeout: 30_000 })

const MORNING = { id: 'tag-1', name: 'Morning', color: null }
const COLD = { id: 'tag-2', name: 'Cold chain', color: null }

type User = ReturnType<typeof userEvent.setup>

function serveTags() {
  server.use(http.get(url('/tags'), () => HttpResponse.json([MORNING, COLD])))
}

const card = (n: number) => screen.getByRole('listitem', { name: `Field ${n}` })

async function addField(user: User, type: string, label: string) {
  await user.selectOptions(screen.getByLabelText('New field type'), type)
  await user.click(screen.getByRole('button', { name: 'Add field' }))
  const count = screen.getAllByRole('listitem', { name: /^Field \d+$/ }).length
  await user.type(within(card(count)).getByLabelText('Label'), label)
  return card(count)
}

describe('template editor: building fields', () => {
  it('adds fields with type-specific settings', async () => {
    signIn()
    serveTags()
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    const temp = await addField(user, 'Temperature', 'Fridge temp')
    expect(within(temp).getByLabelText('Minimum')).toBeInTheDocument()
    expect(within(temp).getByLabelText('Maximum')).toBeInTheDocument()
    expect(within(temp).getByLabelText('Unit')).toBeInTheDocument()
    expect(within(temp).getByText('When an answer fails')).toBeInTheDocument()
    expect(within(temp).queryByText('Options')).not.toBeInTheDocument()

    const yesNo = await addField(user, 'YesNo', 'Clean?')
    expect(within(yesNo).getByLabelText('Passing answer')).toBeInTheDocument()
    expect(within(yesNo).queryByLabelText('Minimum')).not.toBeInTheDocument()

    const select = await addField(user, 'SingleSelect', 'Condition')
    expect(within(select).getByLabelText('Option 1 label')).toBeInTheDocument()
    expect(within(select).getByLabelText('Option 2 label')).toBeInTheDocument()
    await user.click(within(select).getByRole('button', { name: 'Add option' }))
    expect(within(select).getByLabelText('Option 3 label')).toBeInTheDocument()

    const text = await addField(user, 'Text', 'Notes')
    expect(within(text).queryByText('When an answer fails')).not.toBeInTheDocument()
    expect(within(text).queryByLabelText('Minimum')).not.toBeInTheDocument()

    const note = await addField(user, 'Instruction', 'Wash your hands')
    expect(within(note).queryByLabelText('Required')).not.toBeInTheDocument()
  })

  it('generates the key from the label, keeps keys unique and lets you edit them', async () => {
    signIn()
    serveTags()
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    const first = await addField(user, 'Number', 'Fridge temp')
    expect(within(first).getByLabelText('Key')).toHaveValue('fridge_temp')
    const second = await addField(user, 'Number', 'Fridge temp')
    expect(within(second).getByLabelText('Key')).toHaveValue('fridge_temp_2')

    const key = within(second).getByLabelText('Key')
    await user.clear(key)
    await user.type(key, 'custom')
    await user.type(within(second).getByLabelText('Label'), ' again')
    expect(key).toHaveValue('custom')
  })

  it('reorders fields with the buttons and the keyboard, and removes them', async () => {
    signIn()
    serveTags()
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    await addField(user, 'Text', 'Alpha')
    await addField(user, 'Text', 'Beta')
    await addField(user, 'Text', 'Gamma')
    const labels = () =>
      screen
        .getAllByRole('listitem', { name: /^Field \d+$/ })
        .map((li) => (within(li).getByLabelText('Label') as HTMLInputElement).value)
    expect(labels()).toEqual(['Alpha', 'Beta', 'Gamma'])

    await user.click(screen.getByRole('button', { name: 'Move field 3 up' }))
    expect(labels()).toEqual(['Alpha', 'Gamma', 'Beta'])
    expect(screen.getByRole('button', { name: 'Move field 1 up' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Move field 3 down' })).toBeDisabled()

    within(card(2)).getByLabelText('Label').focus()
    await user.keyboard('{Alt>}{ArrowUp}{/Alt}')
    expect(labels()).toEqual(['Gamma', 'Alpha', 'Beta'])

    await user.click(screen.getByRole('button', { name: 'Remove field 1' }))
    expect(labels()).toEqual(['Alpha', 'Beta'])
  })

  it('previews the fields live', async () => {
    signIn()
    serveTags()
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    const preview = screen.getByRole('complementary', { name: 'Preview' })
    expect(within(preview).getByText(/Add a field to see how it will look/)).toBeInTheDocument()

    const temp = await addField(user, 'Temperature', 'Fridge temp')
    await user.type(within(temp).getByLabelText('Maximum'), '5')
    expect(within(preview).getByText('Fridge temp')).toBeInTheDocument()
    expect(within(preview).getByText(/5/, { selector: 'p' })).toBeInTheDocument()

    // The sandbox accepts input and shows the failure a person would see.
    await user.type(within(preview).getByLabelText('Fridge temp'), '9')
    expect(await within(preview).findByTestId('failure')).toBeInTheDocument()
  })
})

describe('template editor: creating', () => {
  it('sends the full payload and returns to the list', async () => {
    signIn()
    serveTemplates([])
    serveTags()
    const bodies = recordBodies('post', '/templates', (body) =>
      HttpResponse.json(template({ id: 'template-9', ...(body as object) }), { status: 201 }),
    )
    const user = userEvent.setup({ delay: null })
    const { router } = await renderApp('/settings/templates/new')

    await user.type(await screen.findByLabelText('Name'), '  Fridge log ')
    await user.type(screen.getByLabelText('Description'), 'Twice a day')
    await user.selectOptions(screen.getByLabelText('Kind'), 'RecordLog')
    await user.click(screen.getByLabelText('A manager must approve completed tasks'))
    await user.click(await screen.findByLabelText('Cold chain'))

    const temp = await addField(user, 'Temperature', 'Fridge temp')
    await user.type(within(temp).getByLabelText('Minimum'), '0')
    await user.type(within(temp).getByLabelText('Maximum'), '5,5')
    await user.click(within(temp).getByLabelText('Require a photo'))

    const choice = await addField(user, 'SingleSelect', 'Condition')
    await user.type(within(choice).getByLabelText('Option 1 label'), 'Clean')
    await user.type(within(choice).getByLabelText('Option 2 label'), 'Dirty')
    await user.click(within(choice).getByLabelText('Option 2 is a failure'))

    const yesNo = await addField(user, 'YesNo', 'Door sealed?')
    await user.selectOptions(within(yesNo).getByLabelText('Passing answer'), 'Yes')

    await user.click(screen.getByRole('button', { name: 'Create template' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/settings/templates'))
    expect(bodies).toHaveLength(1)
    expect(bodies[0]).toEqual({
      name: 'Fridge log',
      description: 'Twice a day',
      kind: 'RecordLog',
      requiresApproval: true,
      requiresPhoto: false,
      tagIds: ['tag-2'],
      fields: [
        {
          key: 'fridge_temp',
          label: 'Fridge temp',
          type: 'Temperature',
          required: true,
          helpText: null,
          section: null,
          min: 0,
          max: 5.5,
          unit: null,
          expectedAnswer: null,
          options: [],
          photoRequiredOnFailure: true,
          commentRequiredOnFailure: true,
          raiseIssueOnFailure: true,
        },
        {
          key: 'condition',
          label: 'Condition',
          type: 'SingleSelect',
          required: true,
          helpText: null,
          section: null,
          min: null,
          max: null,
          unit: null,
          expectedAnswer: null,
          options: [
            { value: 'clean', label: 'Clean', isFailure: false },
            { value: 'dirty', label: 'Dirty', isFailure: true },
          ],
          photoRequiredOnFailure: false,
          commentRequiredOnFailure: true,
          raiseIssueOnFailure: true,
        },
        {
          key: 'door_sealed',
          label: 'Door sealed?',
          type: 'YesNo',
          required: true,
          helpText: null,
          section: null,
          min: null,
          max: null,
          unit: null,
          expectedAnswer: true,
          options: [],
          photoRequiredOnFailure: false,
          commentRequiredOnFailure: true,
          raiseIssueOnFailure: true,
        },
      ],
    })
  })

  it('blocks an invalid template before sending anything', async () => {
    signIn()
    serveTags()
    const bodies = recordBodies('post', '/templates')
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    await user.click(await screen.findByRole('button', { name: 'Create template' }))
    expect(await screen.findByText('Enter a name.')).toBeInTheDocument()
    expect(screen.getByText('A template needs at least one field.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Name'), 'Checks')
    const temp = await addField(user, 'Temperature', 'Temp')
    await user.type(within(temp).getByLabelText('Minimum'), '9')
    await user.type(within(temp).getByLabelText('Maximum'), '2')
    const select = await addField(user, 'MultiSelect', 'Pick')
    await user.type(within(select).getByLabelText('Option 1 label'), 'Same')
    await user.type(within(select).getByLabelText('Option 2 label'), 'Same')
    // Same label, different generated value, so make the values collide on purpose.
    const value = within(select).getByLabelText('Option 2 value')
    await user.clear(value)
    await user.type(value, 'same')
    await addField(user, 'Text', 'Temp')
    const dupe = card(3)
    await user.clear(within(dupe).getByLabelText('Key'))
    await user.type(within(dupe).getByLabelText('Key'), 'temp')

    await user.click(screen.getByRole('button', { name: 'Create template' }))

    expect(
      await within(temp).findByText('Minimum cannot be greater than maximum.'),
    ).toBeInTheDocument()
    expect(within(select).getByText('Option values must be unique.')).toBeInTheDocument()
    expect(within(dupe).getByText("Field key 'temp' is used more than once.")).toBeInTheDocument()
    expect(screen.getByText('Fix the highlighted problems before saving.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('flags a range bound that is not a number', async () => {
    signIn()
    serveTags()
    const bodies = recordBodies('post', '/templates')
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    await user.type(await screen.findByLabelText('Name'), 'Checks')
    const num = await addField(user, 'Number', 'Count')
    await user.type(within(num).getByLabelText('Minimum'), 'lots')
    await user.click(screen.getByRole('button', { name: 'Create template' }))

    expect(await within(num).findByText('Minimum must be a number.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('shows a taken name against the name input', async () => {
    signIn()
    serveTags()
    recordBodies('post', '/templates', () =>
      HttpResponse.json(
        problem(409, 'template.name_taken', 'A template with this name already exists.'),
        { status: 409 },
      ),
    )
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    await user.type(await screen.findByLabelText('Name'), 'Checks')
    await addField(user, 'Checkbox', 'Done')
    await user.click(screen.getByRole('button', { name: 'Create template' }))

    expect(await screen.findByText('A template with this name already exists.')).toHaveAttribute(
      'role',
      'alert',
    )
    expect(screen.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
  })

  it('maps server field-list problems onto the field they name', async () => {
    signIn()
    serveTags()
    recordBodies('post', '/templates', () =>
      HttpResponse.json(
        problem(400, 'template.invalid_fields', "The template's fields are not valid.", {
          errors: {
            fields: [
              "Field 'Condition': give between 2 and 50 options.",
              'A template needs at least one field that can be answered.',
            ],
            Description: ['Too long.'],
          },
        }),
        { status: 400 },
      ),
    )
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/new')

    await user.type(await screen.findByLabelText('Name'), 'Checks')
    // Valid on the client; the server disagrees (e.g. a newer rule).
    const done = await addField(user, 'Checkbox', 'Condition')
    await user.click(screen.getByRole('button', { name: 'Create template' }))

    expect(await within(done).findByText('Give between 2 and 50 options.')).toBeInTheDocument()
    expect(
      screen.getByText('A template needs at least one field that can be answered.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Too long.')).toBeInTheDocument()
  })

  it('refuses people who cannot manage templates', async () => {
    signIn(['templates.view'])
    await renderApp('/settings/templates/new')
    expect(await screen.findByText(/do not have permission/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Create template' })).not.toBeInTheDocument()
  })
})

describe('template editor: editing', () => {
  const existing = template({
    id: 'template-1',
    name: 'Opening checks',
    description: 'Every morning',
    kind: 'Checklist',
    requiresPhoto: true,
    tagIds: ['tag-1'],
    version: 4,
    fields: [
      templateField({ key: 'legacy_key', label: 'Doors unlocked', type: 'Checkbox' }),
      templateField({
        key: 'temp',
        label: 'Freezer',
        type: 'Temperature',
        min: -22,
        max: -18,
        unit: '°C',
        helpText: 'Probe',
        section: 'Kitchen',
      }),
    ],
  })

  function serveTemplate() {
    serveTemplates([])
    serveTags()
    server.use(http.get(url('/templates/template-1'), () => HttpResponse.json(existing)))
  }

  it('loads the template and sends the updated payload, keeping existing keys', async () => {
    signIn()
    serveTemplate()
    const bodies = recordBodies('put', '/templates/template-1', (body) =>
      HttpResponse.json({ ...existing, ...(body as object) }),
    )
    const user = userEvent.setup({ delay: null })
    const { router } = await renderApp('/settings/templates/template-1')

    expect(await screen.findByLabelText('Name')).toHaveValue('Opening checks')
    expect(screen.getByLabelText('Description')).toHaveValue('Every morning')
    expect(await screen.findByLabelText('Morning')).toBeChecked()
    expect(screen.getByLabelText('Cold chain')).not.toBeChecked()
    expect(within(card(2)).getByLabelText('Minimum')).toHaveValue('-22')
    expect(within(card(2)).getByLabelText('Key')).toHaveValue('temp')

    await user.clear(screen.getByLabelText('Name'))
    await user.type(screen.getByLabelText('Name'), 'Opening checks v2')
    const first = within(card(1)).getByLabelText('Label')
    await user.clear(first)
    await user.type(first, 'Front door unlocked')
    expect(within(card(1)).getByLabelText('Key')).toHaveValue('legacy_key')
    await user.click(within(card(2)).getByRole('button', { name: /Move field 2 up/ }))
    await user.click(screen.getByLabelText('Cold chain'))

    await user.click(screen.getByRole('button', { name: 'Save template' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/settings/templates'))
    expect(bodies).toHaveLength(1)
    const body = bodies[0] as {
      name: string
      requiresPhoto: boolean
      tagIds: string[]
      fields: { key: string; label: string; min: number | null; section: string | null }[]
    }
    expect(body.name).toBe('Opening checks v2')
    expect(body.requiresPhoto).toBe(true)
    expect(body.tagIds).toEqual(['tag-1', 'tag-2'])
    expect(body.fields.map((f) => f.key)).toEqual(['temp', 'legacy_key'])
    expect(body.fields[0]).toMatchObject({ min: -22, max: -18, unit: '°C', section: 'Kitchen' })
    expect(body.fields[1]?.label).toBe('Front door unlocked')
  })

  it('shows a missing template as an error', async () => {
    signIn()
    serveTags()
    server.use(
      http.get(url('/templates/template-1'), () =>
        HttpResponse.json(problem(404, 'template.not_found', 'Template not found.'), {
          status: 404,
        }),
      ),
    )
    await renderApp('/settings/templates/template-1')
    expect(await screen.findByText('Template not found.')).toBeInTheDocument()
  })
})

describe('template editor: unsaved changes', () => {
  function setup() {
    signIn()
    serveTags()
    serveTemplates([])
    server.use(http.get(url('/templates/template-1'), () => HttpResponse.json(template())))
  }

  it('asks before leaving with edits, and can keep editing or discard', async () => {
    setup()
    const user = userEvent.setup({ delay: null })
    const { router } = await renderApp('/settings/templates/template-1')

    await user.type(await screen.findByLabelText('Name'), ' changed')
    await user.click(screen.getByRole('link', { name: 'Cancel' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Discard unsaved changes?')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Keep editing' }))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/settings/templates/template-1')
    expect(screen.getByLabelText('Name')).toHaveValue('Opening checks changed')

    await user.click(screen.getByRole('link', { name: 'Cancel' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Discard changes',
      }),
    )
    await waitFor(() => expect(router.state.location.pathname).toBe('/settings/templates'))
  })

  it('blocks on field edits too', async () => {
    setup()
    const user = userEvent.setup({ delay: null })
    await renderApp('/settings/templates/template-1')

    await screen.findByLabelText('Name')
    await user.click(screen.getByRole('button', { name: 'Remove field 1' }))
    await user.click(screen.getByRole('link', { name: 'Back to templates' }))
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
  })

  it('leaves freely when nothing changed, or when the changes were reverted', async () => {
    setup()
    const user = userEvent.setup({ delay: null })
    const { router } = await renderApp('/settings/templates/template-1')

    const name = await screen.findByLabelText('Name')
    await user.type(name, 'x')
    await user.type(name, '{Backspace}')
    await user.click(screen.getByRole('link', { name: 'Cancel' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/settings/templates'))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })
})
