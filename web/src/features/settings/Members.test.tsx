import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import {
  department,
  member,
  paged,
  recordBodies,
  region,
  role,
  SELF_MEMBERSHIP_ID,
  SELF_USER_ID,
  signIn,
  site,
  url,
} from '@/test/settingsFixtures'

const MANAGER_PERMISSIONS = [
  'members.view',
  'members.manage',
  'sites.view',
  'tasks.view',
  'tasks.complete',
]

const TEAM = role({
  id: 'role-team',
  name: 'Team Member',
  isSystem: true,
  permissions: ['tasks.view'],
})
const LEAD = role({
  id: 'role-lead',
  name: 'Lead',
  permissions: ['tasks.view', 'tasks.complete'],
})
// Needs issues.manage, which the signed-in manager does not hold.
const SUPERVISOR = role({ id: 'role-sup', name: 'Supervisor', permissions: ['issues.manage'] })
const ROLES = [TEAM, LEAD, SUPERVISOR]

const NORTH = region({ id: 'region-north', name: 'North' })
const HARBOUR = site({ id: 'site-1', name: 'Harbour Cafe' })
const QUAY = site({ id: 'site-2', name: 'Quay Bar' })
const KITCHEN = department({ id: 'dept-1', name: 'Kitchen' })

const PAT = member({
  id: 'm-pat',
  userId: 'u-pat',
  firstName: 'Pat',
  lastName: 'Manager',
  roleId: 'role-team',
})
const SELF = member({
  id: SELF_MEMBERSHIP_ID,
  userId: SELF_USER_ID,
  firstName: 'Sam',
  lastName: 'Chef',
  email: 'sam@example.com',
  roleId: 'role-lead',
  roleName: 'Lead',
})

function serveDirectory(members = [PAT, SELF]) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get(url('/members'), ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      const status = params.get('status')
      return HttpResponse.json(paged(members.filter((m) => !status || m.status === status)))
    }),
    http.get(url('/roles'), () => HttpResponse.json(ROLES)),
    http.get(url('/regions'), () => HttpResponse.json([NORTH])),
    http.get(url('/departments'), () => HttpResponse.json([KITCHEN])),
    http.get(url('/sites'), () => HttpResponse.json(paged([HARBOUR, QUAY]))),
  )
  return queries
}

function serveMember(
  m: ReturnType<typeof member>,
  access: { allSites: boolean; siteIds: string[]; regionIds: string[]; departmentIds: string[] } = {
    allSites: false,
    siteIds: ['site-1'],
    regionIds: [],
    departmentIds: [],
  },
) {
  server.use(
    http.get(url(`/members/${m.id}`), () => HttpResponse.json(m)),
    http.get(url(`/members/${m.id}/access`), () => HttpResponse.json(access)),
  )
}

describe('members list', () => {
  it('lists members, marks the signed-in one and sends the status and search filters', async () => {
    signIn(MANAGER_PERMISSIONS)
    const queries = serveDirectory([
      PAT,
      SELF,
      member({
        id: 'm-inv',
        userId: 'u-inv',
        firstName: 'Ina',
        email: 'ina@example.com',
        lastName: 'Invited',
        status: 'Invited',
      }),
    ])
    const user = userEvent.setup()
    await renderApp('/settings/members')

    expect(await screen.findByText('Pat Manager')).toBeInTheDocument()
    expect(screen.getByText('You')).toBeInTheDocument()
    expect(screen.getByText('pat@example.com · Team Member')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Status'), 'Invited')
    await waitFor(() => expect(screen.queryByText('Pat Manager')).not.toBeInTheDocument())
    expect(screen.getByText('Ina Invited')).toBeInTheDocument()
    expect(queries.at(-1)?.get('status')).toBe('Invited')

    await user.type(screen.getByLabelText('Search members'), 'ina')
    await waitFor(() => expect(queries.at(-1)?.get('search')).toBe('ina'))
  })

  it('hides invitations from someone who can only view members', async () => {
    signIn(['members.view'])
    serveDirectory()
    await renderApp('/settings/members')

    expect(await screen.findByText('Pat Manager')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Invite member' })).not.toBeInTheDocument()
  })
})

describe('invite', () => {
  async function openInvite() {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    const user = userEvent.setup()
    await renderApp('/settings/members')
    await user.click(await screen.findByRole('button', { name: 'Invite member' }))
    const dialog = await screen.findByRole('dialog', { name: 'Invite a member' })
    return { user, dialog }
  }

  it('sends email, role and all-sites access', async () => {
    const bodies = recordBodies('post', '/members/invitations', () =>
      HttpResponse.json(member({ id: 'm-new' }), { status: 201 }),
    )
    const { user, dialog } = await openInvite()

    await user.type(within(dialog).getByLabelText('Email'), 'new@example.com')
    await user.type(within(dialog).getByLabelText('First name'), 'Nia')
    await user.selectOptions(within(dialog).getByLabelText('Role'), 'role-lead')
    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      email: 'new@example.com',
      roleId: 'role-lead',
      firstName: 'Nia',
      lastName: null,
      allSites: true,
      siteIds: [],
      regionIds: [],
    })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('invites to chosen sites and regions in one call', async () => {
    const invitations = recordBodies('post', '/members/invitations', () =>
      HttpResponse.json(member({ id: 'm-new', allSites: false }), { status: 201 }),
    )
    const access = recordBodies('put', '/members/m-new/access')
    const { user, dialog } = await openInvite()

    await user.type(within(dialog).getByLabelText('Email'), 'new@example.com')
    await user.selectOptions(within(dialog).getByLabelText('Role'), 'role-team')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Access to all sites' }))
    await user.click(await within(dialog).findByRole('checkbox', { name: 'North' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Quay Bar' }))
    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(invitations).toHaveLength(1)
    expect(invitations[0]).toMatchObject({
      roleId: 'role-team',
      allSites: false,
      siteIds: ['site-2'],
      regionIds: ['region-north'],
    })
    expect(access).toEqual([])
  })

  it('keeps a caller who holds only some sites from inviting to all sites', async () => {
    server.use(
      http.get(url('/me'), () =>
        HttpResponse.json({
          access: { allSites: false, siteIds: ['site-2'], regionIds: [] },
        }),
      ),
    )
    const { dialog } = await openInvite()

    const allSites = within(dialog).getByRole('checkbox', { name: 'Access to all sites' })
    await waitFor(() => expect(allSites).not.toBeChecked())
    expect(allSites).toBeDisabled()
    expect(
      within(dialog).getByText(
        'You can only give access to the sites and regions you hold yourself.',
      ),
    ).toBeInTheDocument()
  })

  it('does not offer roles with permissions the caller lacks, and explains why', async () => {
    const { dialog } = await openInvite()

    const role = within(dialog).getByLabelText('Role')
    expect(within(role).getByRole('option', { name: 'Lead' })).toBeEnabled()
    expect(within(role).getByRole('option', { name: 'Supervisor' })).toBeDisabled()
    expect(
      within(dialog).getByText(/Supervisor\) include permissions you do not hold/),
    ).toBeInTheDocument()
  })

  it('validates the form and requires a site choice when not all sites', async () => {
    const bodies = recordBodies('post', '/members/invitations')
    const { user, dialog } = await openInvite()

    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))
    expect(await within(dialog).findByText('Enter an email address.')).toBeInTheDocument()
    expect(within(dialog).getByText('Choose a role.')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Email'), 'new@example.com')
    await user.selectOptions(within(dialog).getByLabelText('Role'), 'role-team')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Access to all sites' }))
    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))
    expect(
      await within(dialog).findByText(
        'Choose at least one site or region, or give access to all sites.',
      ),
    ).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('shows the server refusals: duplicate member against the email, escalation as a banner', async () => {
    let calls = 0
    recordBodies('post', '/members/invitations', () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(
            problem(
              409,
              'member.already_exists',
              'This person is already a member or has a pending invitation.',
            ),
            { status: 409 },
          )
        : HttpResponse.json(
            problem(
              403,
              'member.access_out_of_scope',
              'You can only invite members to the sites you can access.',
            ),
            { status: 403 },
          )
    })
    const { user, dialog } = await openInvite()

    await user.type(within(dialog).getByLabelText('Email'), 'pat@example.com')
    await user.selectOptions(within(dialog).getByLabelText('Role'), 'role-team')
    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))
    expect(
      await within(dialog).findByText(
        'This person is already a member or has a pending invitation.',
      ),
    ).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')

    await user.click(within(dialog).getByRole('button', { name: 'Send invitation' }))
    expect(
      await within(dialog).findByText('You can only invite members to the sites you can access.'),
    ).toBeInTheDocument()
  })
})

describe('member detail', () => {
  it('changes the role and only offers roles the caller can grant', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT)
    const bodies = recordBodies('put', '/members/m-pat/role', () =>
      HttpResponse.json({ ...PAT, roleId: 'role-lead', roleName: 'Lead' }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    const select = await screen.findByRole('combobox', { name: 'Role' })
    expect(within(select).getByRole('option', { name: 'Supervisor' })).toBeDisabled()
    const save = screen.getByRole('button', { name: 'Change role' })
    expect(save).toBeDisabled()

    await user.selectOptions(select, 'role-lead')
    await user.click(save)

    await waitFor(() => expect(bodies).toEqual([{ roleId: 'role-lead' }]))
  })

  it('shows the server message when the role change is refused', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT)
    recordBodies('put', '/members/m-pat/role', () =>
      HttpResponse.json(
        problem(409, 'member.last_owner', 'An organization must keep at least one active Owner.'),
        {
          status: 409,
        },
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Role' }), 'role-lead')
    await user.click(screen.getByRole('button', { name: 'Change role' }))

    expect(
      await screen.findByText('An organization must keep at least one active Owner.'),
    ).toBeInTheDocument()
  })

  it('loads access and saves the full replacement', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT, {
      allSites: false,
      siteIds: ['site-1'],
      regionIds: [],
      departmentIds: ['dept-1'],
    })
    const bodies = recordBodies('put', '/members/m-pat/access', (body) =>
      HttpResponse.json(body as object),
    )
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    const harbour = await screen.findByRole('checkbox', { name: 'Harbour Cafe' })
    expect(harbour).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Quay Bar' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Kitchen' })).toBeChecked()

    await user.click(screen.getByRole('checkbox', { name: 'Quay Bar' }))
    await user.click(harbour)
    await user.click(screen.getByRole('checkbox', { name: 'North' }))
    await user.click(screen.getByRole('button', { name: 'Save access' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      allSites: false,
      siteIds: ['site-2'],
      regionIds: ['region-north'],
      departmentIds: ['dept-1'],
    })
  })

  it('shows the refusal when access is outside the caller scope', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT)
    recordBodies('put', '/members/m-pat/access', () =>
      HttpResponse.json(
        problem(
          403,
          'member.access_out_of_scope',
          'You can only manage access within your own sites and regions.',
        ),
        { status: 403 },
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    await user.click(await screen.findByRole('checkbox', { name: 'Quay Bar' }))
    await user.click(screen.getByRole('button', { name: 'Save access' }))

    expect(
      await screen.findByText('You can only manage access within your own sites and regions.'),
    ).toBeInTheDocument()
  })

  it('offers no changes to yourself and says why', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(SELF)
    await renderApp(`/settings/members/${SELF_MEMBERSHIP_ID}`)

    expect(
      await screen.findByText('You cannot change your own membership or access.', {
        selector: 'p[role=status]',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Role' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Change role' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save access' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Remove/ })).not.toBeInTheDocument()
    expect(await screen.findByRole('checkbox', { name: 'Harbour Cafe' })).toBeDisabled()
  })

  it('treats the same person under another membership id as yourself', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember({ ...SELF, id: 'm-other-org-row' })
    await renderApp('/settings/members/m-other-org-row')

    expect(await screen.findByText(/cannot change your own/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
  })

  it('blocks managing someone whose role holds permissions the caller lacks', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember({ ...PAT, roleId: 'role-sup', roleName: 'Supervisor' })
    await renderApp('/settings/members/m-pat')

    expect(
      await screen.findByText(/permissions you do not hold yourself/, {
        selector: 'p[role=status]',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Change role' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
  })

  it('is read-only for someone without members.manage', async () => {
    signIn(['members.view', 'sites.view'])
    serveDirectory()
    serveMember(PAT)
    await renderApp('/settings/members/m-pat')

    expect(await screen.findByRole('checkbox', { name: 'Harbour Cafe' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Change role' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
  })

  it('suspends an active member after confirmation', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT)
    const bodies = recordBodies('post', '/members/m-pat/suspend')
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    await user.click(await screen.findByRole('button', { name: 'Suspend' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Suspend' }),
    )

    await waitFor(() => expect(bodies).toHaveLength(1))
  })

  it('reactivates suspended members and resends invitations', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember({ ...PAT, status: 'Suspended' })
    const reactivate = recordBodies('post', '/members/m-pat/reactivate')
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    expect(screen.queryByRole('button', { name: 'Resend invitation' })).not.toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: 'Reactivate' }))
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Reactivate' }),
    )
    await waitFor(() => expect(reactivate).toHaveLength(1))
  })

  it('resends an invitation to an invited member', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember({ ...PAT, status: 'Invited' })
    const bodies = recordBodies('post', '/members/m-pat/resend-invitation')
    const user = userEvent.setup()
    await renderApp('/settings/members/m-pat')

    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: 'Resend invitation' }))
    await waitFor(() => expect(bodies).toHaveLength(1))
  })

  it('removes a member, or shows why the last owner cannot be removed', async () => {
    signIn(MANAGER_PERMISSIONS)
    serveDirectory()
    serveMember(PAT)
    let calls = 0
    recordBodies('delete', '/members/m-pat', () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(
            problem(
              409,
              'member.last_owner',
              'An organization must keep at least one active Owner.',
            ),
            {
              status: 409,
            },
          )
        : new HttpResponse(null, { status: 204 })
    })
    const user = userEvent.setup()
    const { router } = await renderApp('/settings/members/m-pat')

    await user.click(await screen.findByRole('button', { name: 'Remove from organization' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Remove member' }))
    expect(
      await within(dialog).findByText('An organization must keep at least one active Owner.'),
    ).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Remove member' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/settings/members'))
  })
})
