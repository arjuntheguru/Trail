import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormField } from '@/components/FormField'
import { Label } from '@/components/ui/label'

import { AccessPicker, type AccessValue } from './AccessPicker'
import { applyServerError } from './formErrors'
import { holdsAll } from './memberRules'
import { callerAccessQuery, rolesQuery } from './queries'
import { FormDialog, Hint } from './shared'
import { useSettingsMutation } from './useSettingsMutation'
import { useAccessOptions } from './useAccessOptions'

// Mirrors InviteMemberValidator (email, role, names up to User.NameMaxLength); the server stays the authority.
const inviteSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Enter an email address.')
    .max(254, 'The email is too long.')
    .refine((v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), 'Enter a valid email address.'),
  firstName: z.string().trim().max(100, 'The name is too long.'),
  lastName: z.string().trim().max(100, 'The name is too long.'),
  roleId: z.string().min(1, 'Choose a role.'),
})
type InviteValues = z.infer<typeof inviteSchema>

const FIELDS = ['email', 'firstName', 'lastName', 'roleId'] as const
const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function InviteDialog({ onClose }: { onClose: () => void }) {
  const { client } = useAuthService()
  const session = useSession()
  const roles = useQuery(rolesQuery(client, session.organization?.id))
  const options = useAccessOptions()
  const [chosenAccess, setAccess] = useState<AccessValue>({
    allSites: true,
    siteIds: [],
    regionIds: [],
    departmentIds: [],
  })
  const callerAccess = useQuery(callerAccessQuery(client, session.organization?.id))
  const allSitesLocked = callerAccess.data?.allSites === false
  // A caller limited to some sites cannot invite to all of them: they start from an explicit choice instead.
  const access =
    allSitesLocked && chosenAccess.allSites ? { ...chosenAccess, allSites: false } : chosenAccess
  const [accessError, setAccessError] = useState<string>()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<InviteValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: '', firstName: '', lastName: '', roleId: '' },
  })
  const roleList = roles.data ?? []

  const mutation = useSettingsMutation({
    // One call: the invitation carries the chosen sites and regions, saved with the membership.
    run: (values: InviteValues) =>
      unwrap(
        client.POST('/api/v1/members/invitations', {
          body: {
            email: values.email,
            roleId: values.roleId,
            firstName: values.firstName || null,
            lastName: values.lastName || null,
            allSites: access.allSites,
            siteIds: access.allSites ? [] : access.siteIds,
            regionIds: access.allSites ? [] : access.regionIds,
          },
        }),
      ),
    areas: [['members']],
    onSuccess: () => {
      toast.success('Invitation sent.')
      onClose()
    },
  })

  const submit = form.handleSubmit((values) => {
    setGeneral([])
    if (!access.allSites && access.siteIds.length === 0 && access.regionIds.length === 0) {
      setAccessError('Choose at least one site or region, or give access to all sites.')
      return
    }
    setAccessError(undefined)
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, FIELDS, form.setError, {
            'member.already_exists': 'email',
            'member.unknown_role': 'roleId',
          }),
        ),
    })
  })

  const lockedRoles = roleList.filter((r) => !holdsAll(session.permissions, r.permissions))
  return (
    <FormDialog
      title="Invite a member"
      description="They get an email with a link to join. You can change their access later."
      submitLabel="Send invitation"
      pending={mutation.isPending}
      errors={[...general, ...(accessError ? [accessError] : [])]}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <FormField
        label="Email"
        type="email"
        autoComplete="off"
        error={form.formState.errors.email}
        {...form.register('email')}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label="First name"
          autoComplete="off"
          error={form.formState.errors.firstName}
          {...form.register('firstName')}
        />
        <FormField
          label="Last name"
          autoComplete="off"
          error={form.formState.errors.lastName}
          {...form.register('lastName')}
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="invite-role">Role</Label>
        <select id="invite-role" className={selectClass} {...form.register('roleId')}>
          <option value="">Choose a role</option>
          {roleList.map((r) => (
            <option
              key={r.id}
              value={r.id}
              disabled={!holdsAll(session.permissions, r.permissions)}
            >
              {r.name}
            </option>
          ))}
        </select>
        {form.formState.errors.roleId && (
          <p role="alert" className="text-sm text-destructive">
            {form.formState.errors.roleId.message}
          </p>
        )}
        {lockedRoles.length > 0 && (
          <Hint>
            Roles you cannot choose ({lockedRoles.map((r) => r.name).join(', ')}) include
            permissions you do not hold yourself.
          </Hint>
        )}
      </div>
      <div className="flex flex-col gap-2">
        <p className="text-sm font-medium">Site access</p>
        {options.isPending ? (
          <Hint>Loading sites…</Hint>
        ) : (
          <AccessPicker
            value={access}
            onChange={setAccess}
            sites={options.sites}
            regions={options.regions}
            allSitesLocked={allSitesLocked}
          />
        )}
        {!options.available && (
          <Hint>You cannot view sites, so only access to all sites can be chosen.</Hint>
        )}
      </div>
    </FormDialog>
  )
}
