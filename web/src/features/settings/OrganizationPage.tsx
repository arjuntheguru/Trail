import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert, FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { applyServerError } from './formErrors'
import { organizationQuery, type Organization } from './queries'
import { Hint, ListSkeleton, LoadError, SectionHeader } from './shared'
import { useSettingsMutation } from './useSettingsMutation'
import { TimeZonePicker } from './TimeZonePicker'

// Mirrors Organization.NameMaxLength; the server stays the authority.
const organizationSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(200, 'The name is too long.'),
  defaultTimeZone: z.string().min(1, 'Choose a time zone.'),
})
type OrganizationValues = z.infer<typeof organizationSchema>

export function OrganizationPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const organization = useQuery(organizationQuery(client, organizationId))
  return (
    <section className="flex flex-col gap-4">
      <SectionHeader title="Organization" description="Your organization's name and defaults." />
      {organization.isPending ? (
        <ListSkeleton rows={2} />
      ) : organization.isError ? (
        <LoadError error={organization.error} retry={() => void organization.refetch()} />
      ) : (
        <OrganizationForm organization={organization.data} />
      )}
    </section>
  )
}

function OrganizationForm({ organization }: { organization: Organization }) {
  const auth = useAuthService()
  const session = useSession()
  const canManage = usePermission(Permissions.Organization.Manage)
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<OrganizationValues>({
    resolver: zodResolver(organizationSchema),
    defaultValues: { name: organization.name, defaultTimeZone: organization.defaultTimeZone },
  })
  const mutation = useSettingsMutation({
    run: (body: OrganizationValues) =>
      unwrap(auth.client.PUT('/api/v1/organizations/current', { body })),
    areas: [['organization']],
    onSuccess: (saved) => {
      // The switcher and menus read the name from the session.
      const rename = <T extends { id: string; name: string }>(o: T): T =>
        o.id === saved.id ? { ...o, name: saved.name } : o
      auth.store.patchSession({
        organization: session.organization ? rename(session.organization) : null,
        organizations: session.organizations.map(rename),
      })
      form.reset({ name: saved.name, defaultTimeZone: saved.defaultTimeZone })
      toast.success('Organization saved.')
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(applyServerError(error, ['name', 'defaultTimeZone'], form.setError)),
    })
  })
  const timeZone = useWatch({ control: form.control, name: 'defaultTimeZone' })

  return (
    <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-4">
      <FormField
        label="Name"
        autoComplete="off"
        disabled={!canManage}
        error={form.formState.errors.name}
        {...form.register('name')}
      />
      <div className="flex flex-col gap-1">
        <p className="text-sm font-medium">Web address name</p>
        <p className="text-sm text-muted-foreground">{organization.slug}</p>
      </div>
      {canManage ? (
        <TimeZonePicker
          label="Default time zone"
          value={timeZone}
          onChange={(zone) => form.setValue('defaultTimeZone', zone, { shouldDirty: true })}
          error={form.formState.errors.defaultTimeZone?.message}
        />
      ) : (
        <div className="flex flex-col gap-1">
          <p className="text-sm font-medium">Default time zone</p>
          <p className="text-sm text-muted-foreground">{organization.defaultTimeZone}</p>
        </div>
      )}
      <FormAlert messages={general} />
      {canManage ? (
        <div>
          <Button type="submit" disabled={mutation.isPending || !form.formState.isDirty}>
            {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
            Save changes
          </Button>
        </div>
      ) : (
        <Hint>You can view these details but do not have permission to change them.</Hint>
      )}
    </form>
  )
}
