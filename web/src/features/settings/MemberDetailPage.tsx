import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Loader2, Mail, UserCheck, UserMinus, UserX } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { formatDateTime } from '@/lib/dates'

import { AccessEditor } from './AccessEditor'
import { cannotManageReason, holdsAll, useIsSelf } from './memberRules'
import { memberQuery, rolesQuery, type Member, type Role } from './queries'
import {
  ConfirmDialog,
  Hint,
  ListSkeleton,
  LoadError,
  RequirePermission,
  StatusBadge,
} from './shared'
import { useSettingsMutation } from './useSettingsMutation'

const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function MemberDetailPage({ membershipId }: { membershipId: string }) {
  return (
    <RequirePermission permission={Permissions.Members.View}>
      <MemberLoader membershipId={membershipId} />
    </RequirePermission>
  )
}

function MemberLoader({ membershipId }: { membershipId: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const member = useQuery(memberQuery(client, organizationId, membershipId))
  const roles = useQuery(rolesQuery(client, organizationId))

  return (
    <section className="flex flex-col gap-5">
      <Link
        to="/settings/members"
        className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Back to members
      </Link>
      {member.isPending || roles.isPending ? (
        <ListSkeleton rows={3} />
      ) : member.isError ? (
        <LoadError error={member.error} retry={() => void member.refetch()} />
      ) : roles.isError ? (
        <LoadError error={roles.error} retry={() => void roles.refetch()} />
      ) : (
        <MemberDetail member={member.data} roles={roles.data} />
      )}
    </section>
  )
}

function MemberDetail({ member, roles }: { member: Member; roles: readonly Role[] }) {
  const session = useSession()
  const canManagePermission = usePermission(Permissions.Members.Manage)
  const isSelf = useIsSelf()(member)
  const blocked = cannotManageReason(member, isSelf, roles, session.permissions)
  const canManage = canManagePermission && !blocked
  const name = `${member.firstName} ${member.lastName}`.trim() || member.email

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-xl font-semibold">{name}</h2>
          {isSelf && <Badge variant="outline">You</Badge>}
          <StatusBadge status={member.status} />
        </div>
        <p className="text-sm text-muted-foreground">{member.email}</p>
        <p className="text-sm text-muted-foreground">
          {member.lastLoginAt
            ? `Last signed in ${formatDateTime(member.lastLoginAt)}`
            : 'Has not signed in yet'}
        </p>
      </header>

      {canManagePermission && blocked && (
        <p role="status" className="rounded-md border bg-muted/50 p-3 text-sm">
          {blocked}
        </p>
      )}

      <RoleSection member={member} roles={roles} canManage={canManage} />
      <AccessEditor
        membershipId={member.id}
        readOnlyReason={
          canManage
            ? undefined
            : canManagePermission
              ? blocked
              : 'You can view this access but do not have permission to change it.'
        }
      />
      {canManage && <MemberActions member={member} name={name} />}
    </div>
  )
}

function RoleSection({
  member,
  roles,
  canManage,
}: {
  member: Member
  roles: readonly Role[]
  canManage: boolean
}) {
  const { client } = useAuthService()
  const held = useSession().permissions
  const [roleId, setRoleId] = useState(member.roleId)
  const mutation = useSettingsMutation({
    run: (id: string) =>
      unwrap(
        client.PUT('/api/v1/members/{membershipId}/role', {
          params: { path: { membershipId: member.id } },
          body: { roleId: id },
        }),
      ),
    areas: [['members'], ['roles']],
    onSuccess: () =>
      toast.success('Role changed. It applies the next time they sign in or refresh.'),
  })

  if (!canManage) {
    return (
      <section className="flex flex-col gap-1">
        <h3 className="text-lg font-semibold">Role</h3>
        <p>{member.roleName}</p>
      </section>
    )
  }
  const locked = roles.some((r) => !holdsAll(held, r.permissions))
  return (
    <section aria-labelledby="role-heading" className="flex flex-col gap-3">
      <h3 id="role-heading" className="text-lg font-semibold">
        Role
      </h3>
      <div className="flex flex-col gap-2">
        <Label htmlFor="member-role">Role</Label>
        <select
          id="member-role"
          className={selectClass}
          value={roleId}
          onChange={(e) => setRoleId(e.target.value)}
        >
          {roles.map((r) => (
            <option
              key={r.id}
              value={r.id}
              disabled={r.id !== member.roleId && !holdsAll(held, r.permissions)}
            >
              {r.name}
            </option>
          ))}
        </select>
        {locked && (
          <Hint>Roles with permissions you do not hold yourself cannot be assigned by you.</Hint>
        )}
      </div>
      {mutation.isError && <FormAlert messages={[errorMessage(mutation.error)]} />}
      <div>
        <Button
          disabled={mutation.isPending || roleId === member.roleId}
          onClick={() => mutation.mutate(roleId)}
        >
          {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
          Change role
        </Button>
      </div>
    </section>
  )
}

type Confirm = 'suspend' | 'reactivate' | 'remove'

function MemberActions({ member, name }: { member: Member; name: string }) {
  const { client } = useAuthService()
  const navigate = useNavigate()
  const [confirm, setConfirm] = useState<Confirm | null>(null)
  const path = { params: { path: { membershipId: member.id } } }
  const resend = useSettingsMutation({
    run: () => unwrap(client.POST('/api/v1/members/{membershipId}/resend-invitation', path)),
    areas: [],
    onSuccess: () => toast.success('Invitation sent again.'),
  })
  const close = () => setConfirm(null)
  const areas = [['members']] as const

  return (
    <section aria-labelledby="actions-heading" className="flex flex-col gap-3 border-t pt-4">
      <h3 id="actions-heading" className="text-lg font-semibold">
        Membership
      </h3>
      <div className="flex flex-wrap gap-2">
        {member.status === 'Invited' && (
          <Button variant="outline" disabled={resend.isPending} onClick={() => resend.mutate()}>
            <Mail aria-hidden />
            Resend invitation
          </Button>
        )}
        {member.status === 'Active' && (
          <Button variant="outline" onClick={() => setConfirm('suspend')}>
            <UserX aria-hidden />
            Suspend
          </Button>
        )}
        {member.status === 'Suspended' && (
          <Button variant="outline" onClick={() => setConfirm('reactivate')}>
            <UserCheck aria-hidden />
            Reactivate
          </Button>
        )}
        <Button variant="outline" onClick={() => setConfirm('remove')}>
          <UserMinus aria-hidden />
          Remove from organization
        </Button>
      </div>
      {resend.isError && <FormAlert messages={[errorMessage(resend.error)]} />}
      {confirm === 'suspend' && (
        <ConfirmDialog
          title="Suspend member?"
          description={`${name} will be signed out and cannot use this organization until you reactivate them.`}
          confirmLabel="Suspend"
          successMessage="Member suspended."
          areas={areas}
          run={() => unwrap(client.POST('/api/v1/members/{membershipId}/suspend', path))}
          onClose={close}
        />
      )}
      {confirm === 'reactivate' && (
        <ConfirmDialog
          title="Reactivate member?"
          description={`${name} will be able to use this organization again.`}
          confirmLabel="Reactivate"
          destructive={false}
          successMessage="Member reactivated."
          areas={areas}
          run={() => unwrap(client.POST('/api/v1/members/{membershipId}/reactivate', path))}
          onClose={close}
        />
      )}
      {confirm === 'remove' && (
        <ConfirmDialog
          title="Remove member?"
          description={`${name} will lose all access to this organization. Their past activity stays on record.`}
          confirmLabel="Remove member"
          successMessage="Member removed."
          areas={areas}
          run={() => unwrap(client.DELETE('/api/v1/members/{membershipId}', path))}
          onDone={() => void navigate({ to: '/settings/members' })}
          onClose={close}
        />
      )}
    </section>
  )
}
