import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormField } from '@/components/FormField'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

import { applyServerError } from './formErrors'
import { holdsAll } from './memberRules'
import { PermissionTree } from './PermissionTree'
import { permissionCatalogQuery, rolesQuery, type Role } from './queries'
import {
  ConfirmDialog,
  EmptyState,
  FormDialog,
  Hint,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from './shared'
import { useSettingsMutation } from './useSettingsMutation'

// Mirrors Role.NameMaxLength / DescriptionMaxLength; the server stays the authority.
const roleSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(64, 'The name is too long.'),
  description: z.string().trim().max(500, 'The description is too long.'),
})
type RoleValues = z.infer<typeof roleSchema>

export function RolesPage() {
  return (
    <RequirePermission permission={Permissions.Members.View}>
      <RolesSection />
    </RequirePermission>
  )
}

function RolesSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Roles.Manage)
  const roles = useQuery(rolesQuery(client, organizationId))
  const [editing, setEditing] = useState<Role | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Role | null>(null)

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Roles"
        description="A role is a set of permissions. Built-in roles are managed by Trail."
        action={
          canManage && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              Add role
            </Button>
          )
        }
      />
      {roles.isPending ? (
        <ListSkeleton />
      ) : roles.isError ? (
        <LoadError error={roles.error} retry={() => void roles.refetch()} />
      ) : roles.data.length === 0 ? (
        <EmptyState>No roles yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {roles.data.map((role) => (
            <li key={role.id} className="flex flex-col gap-2 rounded-xl border bg-card p-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{role.name}</span>
                {role.isSystem && <Badge variant="secondary">Built-in</Badge>}
              </div>
              {role.description && (
                <p className="text-sm text-muted-foreground">{role.description}</p>
              )}
              <p className="text-sm text-muted-foreground">
                {role.permissions.length} permissions · {role.memberCount}{' '}
                {role.memberCount === 1 ? 'member' : 'members'}
              </p>
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  aria-label={`${canManage ? 'Edit' : 'View'} role ${role.name}`}
                  onClick={() => setEditing(role)}
                >
                  {canManage ? <Pencil aria-hidden /> : <Eye aria-hidden />}
                  {canManage ? 'Edit' : 'View'}
                </Button>
                {canManage && !role.isSystem && (
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Delete role ${role.name}`}
                    onClick={() => setDeleting(role)}
                  >
                    <Trash2 aria-hidden />
                    Delete
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
      {editing && (
        <RoleDialog
          role={editing === 'new' ? undefined : editing}
          canManage={canManage}
          onClose={() => setEditing(null)}
        />
      )}
      {deleting && <DeleteRoleDialog role={deleting} onClose={() => setDeleting(null)} />}
    </section>
  )
}

function RoleDialog({
  role,
  canManage,
  onClose,
}: {
  role?: Role
  canManage: boolean
  onClose: () => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const held = useSession().permissions
  const catalog = useQuery(permissionCatalogQuery(client, organizationId))
  const [permissions, setPermissions] = useState<string[]>([...(role?.permissions ?? [])])
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<RoleValues>({
    resolver: zodResolver(roleSchema),
    defaultValues: { name: role?.name ?? '', description: role?.description ?? '' },
  })
  const mutation = useSettingsMutation({
    run: (values: RoleValues) => {
      const body = {
        name: values.name,
        description: values.description || null,
        permissions,
      }
      return role
        ? unwrap(
            client.PUT('/api/v1/roles/{roleId}', { params: { path: { roleId: role.id } }, body }),
          )
        : unwrap(client.POST('/api/v1/roles', { body }))
    },
    areas: [['roles'], ['members']],
    onSuccess: () => {
      toast.success(role ? 'Role saved.' : 'Role added.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, ['name', 'description'], form.setError, {
            'role.name_taken': 'name',
          }),
        ),
    })
  })

  // The Owner role is fixed, and nobody can edit a role holding permissions they lack themselves.
  const ownerFixed = role?.isSystem && role.name === 'Owner'
  const beyondReach = !!role && !holdsAll(held, role.permissions)
  const lockedReason = !canManage
    ? 'You can view this role but do not have permission to change it.'
    : ownerFixed
      ? 'The Owner role always has every permission and cannot be changed.'
      : beyondReach
        ? 'This role has permissions you do not hold yourself, so you cannot change it.'
        : undefined
  const readOnly = !!lockedReason
  const descriptionError = form.formState.errors.description

  return (
    <FormDialog
      title={!canManage ? 'Role' : role ? 'Edit role' : 'Add role'}
      className="sm:max-w-2xl"
      submitLabel={role ? 'Save role' : 'Add role'}
      pending={mutation.isPending}
      errors={general}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
      readOnly={readOnly}
    >
      <FormField
        label="Name"
        autoComplete="off"
        disabled={readOnly || role?.isSystem}
        hint={role?.isSystem ? 'Built-in roles cannot be renamed.' : undefined}
        error={form.formState.errors.name}
        {...form.register('name')}
      />
      <div className="flex flex-col gap-2">
        <Label htmlFor="role-description">Description</Label>
        <Textarea
          id="role-description"
          rows={2}
          disabled={readOnly}
          aria-invalid={descriptionError ? true : undefined}
          {...form.register('description')}
        />
        {descriptionError && (
          <p role="alert" className="text-sm text-destructive">
            {descriptionError.message}
          </p>
        )}
      </div>
      <div className="flex flex-col gap-2">
        <p className="text-sm font-medium">Permissions</p>
        {catalog.isPending ? (
          <ListSkeleton rows={3} />
        ) : catalog.isError ? (
          <LoadError error={catalog.error} retry={() => void catalog.refetch()} />
        ) : (
          <PermissionTree
            catalog={catalog.data}
            value={permissions}
            onChange={setPermissions}
            grantable={held}
            disabled={readOnly}
          />
        )}
        {lockedReason ? (
          <Hint>{lockedReason}</Hint>
        ) : (
          <Hint>You can only give a role permissions that you hold yourself.</Hint>
        )}
      </div>
    </FormDialog>
  )
}

function DeleteRoleDialog({ role, onClose }: { role: Role; onClose: () => void }) {
  const { client } = useAuthService()
  return (
    <ConfirmDialog
      title="Delete role?"
      description={
        role.memberCount > 0
          ? `"${role.name}" is still assigned to ${role.memberCount} ${role.memberCount === 1 ? 'member' : 'members'}. Move them to another role first.`
          : `"${role.name}" will be removed. This cannot be undone.`
      }
      confirmLabel="Delete role"
      successMessage="Role deleted."
      areas={[['roles']]}
      run={() =>
        unwrap(client.DELETE('/api/v1/roles/{roleId}', { params: { path: { roleId: role.id } } }))
      }
      onClose={onClose}
    />
  )
}
