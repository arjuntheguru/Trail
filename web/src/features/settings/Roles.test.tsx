import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { CATALOG, recordBodies, role, signIn, url } from '@/test/settingsFixtures'

// A manager who can manage roles but holds no audit or integration permissions.
const MANAGER = [
  'members.view',
  'roles.manage',
  'tasks.view',
  'tasks.complete',
  'tasks.create_ad_hoc',
  'tasks.approve',
  'issues.view',
  'issues.raise',
]

const OWNER = role({
  id: 'role-owner',
  name: 'Owner',
  isSystem: true,
  permissions: CATALOG.map((p) => p.key),
  memberCount: 1,
})
const TEAM = role({
  id: 'role-team',
  name: 'Team Member',
  isSystem: true,
  permissions: ['tasks.view'],
  memberCount: 4,
})
const CUSTOM = role({
  id: 'role-custom',
  name: 'Closer',
  description: 'Locks up',
  permissions: ['tasks.view', 'tasks.complete'],
  memberCount: 2,
})
const AUDITOR = role({ id: 'role-aud', name: 'Auditor', permissions: ['audit.view'] })

function serveRoles(roles = [OWNER, TEAM, CUSTOM, AUDITOR]) {
  server.use(
    http.get(url('/roles'), () => HttpResponse.json(roles)),
    http.get(url('/roles/permissions'), () => HttpResponse.json(CATALOG)),
  )
}

const perm = (key: string) => new RegExp(key.replace('.', '\\.'))

describe('roles', () => {
  it('lists roles, marking built-in ones and only allowing custom roles to be deleted', async () => {
    signIn(MANAGER)
    serveRoles()
    await renderApp('/settings/roles')

    expect(await screen.findByText('Closer')).toBeInTheDocument()
    expect(screen.getAllByText('Built-in')).toHaveLength(2)
    expect(screen.getByText('2 permissions · 2 members')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete role Closer' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete role Owner' })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Delete role Team Member' }),
    ).not.toBeInTheDocument()
  })

  it('creates a role from the permission tree', async () => {
    signIn(MANAGER)
    serveRoles([TEAM])
    const bodies = recordBodies('post', '/roles', (body) =>
      HttpResponse.json({ ...role(), ...(body as object) }, { status: 201 }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Add role' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add role' })
    await user.type(within(dialog).getByLabelText('Name'), ' Shift lead ')
    // The whole Tasks group, then one permission from Issues.
    await user.click(await within(dialog).findByRole('checkbox', { name: 'Tasks' }))
    await user.click(within(dialog).getByRole('checkbox', { name: perm('issues.raise') }))
    await user.click(within(dialog).getByRole('button', { name: 'Add role' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({ name: 'Shift lead', description: null })
    expect([...(bodies[0] as { permissions: string[] }).permissions].sort()).toEqual([
      'issues.raise',
      'tasks.approve',
      'tasks.complete',
      'tasks.create_ad_hoc',
      'tasks.view',
    ])
  })

  it('shows a group as partly selected and toggles it off again', async () => {
    signIn(MANAGER)
    serveRoles([CUSTOM])
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Edit role Closer' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit role' })
    const group = await within(dialog).findByRole('checkbox', { name: 'Tasks' })
    expect(group).toHaveAttribute('aria-checked', 'mixed')
    expect(within(dialog).getByRole('checkbox', { name: perm('tasks.view') })).toBeChecked()
    expect(within(dialog).getByRole('checkbox', { name: perm('tasks.approve') })).not.toBeChecked()

    await user.click(group)
    expect(group).toHaveAttribute('aria-checked', 'true')
    await user.click(group)
    expect(group).toHaveAttribute('aria-checked', 'false')
  })

  it('does not let permissions the caller lacks be switched on', async () => {
    signIn(MANAGER)
    serveRoles([])
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Add role' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('checkbox', { name: perm('audit.view') })).toBeDisabled()
    expect(within(dialog).getByRole('checkbox', { name: 'Audit' })).toBeDisabled()
    expect(within(dialog).getByRole('checkbox', { name: perm('tasks.view') })).toBeEnabled()
  })

  it('edits a role, keeping the name of built-in roles fixed', async () => {
    signIn(MANAGER)
    serveRoles([TEAM, CUSTOM])
    const bodies = recordBodies('put', '/roles/role-team', (body) =>
      HttpResponse.json({ ...TEAM, ...(body as object) }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Edit role Team Member' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit role' })
    expect(within(dialog).getByLabelText('Name')).toBeDisabled()
    await user.click(await within(dialog).findByRole('checkbox', { name: perm('tasks.complete') }))
    await user.click(within(dialog).getByRole('button', { name: 'Save role' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      name: 'Team Member',
      description: null,
      permissions: ['tasks.view', 'tasks.complete'],
    })
  })

  it('shows roles beyond the caller read-only and explains why', async () => {
    signIn(MANAGER)
    serveRoles()
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Edit role Auditor' }))
    const dialog = await screen.findByRole('dialog')
    expect(
      await within(dialog).findByText(
        /permissions you do not hold yourself, so you cannot change it/,
      ),
    ).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Save role' })).not.toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toBeDisabled()
  })

  it('treats the Owner role as fixed', async () => {
    signIn(MANAGER)
    serveRoles()
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Edit role Owner' }))
    const dialog = await screen.findByRole('dialog')
    expect(
      await within(dialog).findByText(/Owner role always has every permission/),
    ).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Save role' })).not.toBeInTheDocument()
  })

  it('maps a duplicate name and server validation to the form', async () => {
    signIn(MANAGER)
    serveRoles([TEAM])
    let calls = 0
    recordBodies('post', '/roles', () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(
            problem(409, 'role.name_taken', 'A role with this name already exists.'),
            {
              status: 409,
            },
          )
        : HttpResponse.json(
            problem(
              403,
              'member.privilege_escalation',
              'You cannot create a role with permissions you do not have yourself.',
            ),
            { status: 403 },
          )
    })
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Add role' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Name'), 'Team Member')
    await user.click(within(dialog).getByRole('button', { name: 'Add role' }))
    expect(
      await within(dialog).findByText('A role with this name already exists.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')

    await user.click(within(dialog).getByRole('button', { name: 'Add role' }))
    expect(
      await within(dialog).findByText(
        'You cannot create a role with permissions you do not have yourself.',
      ),
    ).toBeInTheDocument()
  })

  it('validates the name before sending', async () => {
    signIn(MANAGER)
    serveRoles([])
    const bodies = recordBodies('post', '/roles')
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Add role' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Add role' }))

    expect(await within(dialog).findByText('Enter a name.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('shows why a role in use cannot be deleted', async () => {
    signIn(MANAGER)
    serveRoles()
    server.use(
      http.delete(url('/roles/role-custom'), () =>
        HttpResponse.json(
          problem(409, 'role.in_use', 'Move the members of this role to another role first.'),
          { status: 409 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    await user.click(await screen.findByRole('button', { name: 'Delete role Closer' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/still assigned to 2 members/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Delete role' }))

    expect(
      await within(dialog).findByText('Move the members of this role to another role first.'),
    ).toBeInTheDocument()
  })

  it('lets someone without roles.manage look but not change', async () => {
    signIn(['members.view', 'tasks.view'])
    serveRoles()
    const user = userEvent.setup()
    await renderApp('/settings/roles')

    expect(await screen.findByText('Closer')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add role' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Delete role/ })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'View role Closer' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('checkbox', { name: perm('tasks.view') })).toBeDisabled()
    expect(within(dialog).queryByRole('button', { name: 'Save role' })).not.toBeInTheDocument()
  })
})
