import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Ban, Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormField } from '@/components/FormField'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { applyServerError } from '@/features/settings/formErrors'
import {
  ConfirmDialog,
  EmptyState,
  FormDialog,
  ListSkeleton,
  LoadError,
  SectionHeader,
} from '@/features/settings/shared'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'
import { sitesQuery } from '@/features/today/queries'
import { formatDateTime } from '@/lib/dates'

import { AREAS, API_KEY_SCOPES, apiKeysQuery, type ApiKey } from './queries'
import { SecretDialog } from './SecretDialog'

const keySchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'The name is too long.'),
  scopes: z.array(z.string()).min(1, 'Choose at least one scope.'),
  siteIds: z.array(z.string()).max(50, 'Restrict a key to at most 50 sites.'),
  expiresOn: z.string().refine((v) => v === '' || endOfDay(v) > new Date(), 'Pick a future date.'),
})
type KeyValues = z.infer<typeof keySchema>

function endOfDay(day: string) {
  return new Date(`${day}T23:59:59`)
}

export function ApiKeysTab() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canSeeSites = usePermission(Permissions.Sites.View)
  const keys = useQuery(apiKeysQuery(client, organizationId))
  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const [creating, setCreating] = useState(false)
  const [revoking, setRevoking] = useState<ApiKey | null>(null)
  const [revealed, setRevealed] = useState<string | null>(null)
  const siteName = new Map((sites.data?.items ?? []).map((s) => [s.id, s.name]))

  return (
    <section className="flex flex-col gap-4" aria-label="API keys">
      <SectionHeader
        title="API keys"
        description="Keys let devices and scripts call Trail without a user signing in."
        action={
          <Button onClick={() => setCreating(true)}>
            <Plus aria-hidden />
            Create API key
          </Button>
        }
      />
      {keys.isPending ? (
        <ListSkeleton />
      ) : keys.isError ? (
        <LoadError error={keys.error} retry={() => void keys.refetch()} />
      ) : keys.data.length === 0 ? (
        <EmptyState>No API keys yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {keys.data.map((key) => (
            <li
              key={key.id}
              className="flex flex-wrap items-start gap-3 rounded-xl border bg-card p-3"
            >
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{key.name}</span>
                  <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">
                    {key.prefix}…
                  </code>
                  {key.revokedAt ? (
                    <Badge variant="destructive">Revoked</Badge>
                  ) : key.expiresAt && new Date(key.expiresAt) < new Date() ? (
                    <Badge variant="secondary">Expired</Badge>
                  ) : (
                    <Badge>Active</Badge>
                  )}
                </div>
                <div className="flex flex-wrap gap-1">
                  {key.scopes.map((scope) => (
                    <Badge key={scope} variant="outline">
                      {scope}
                    </Badge>
                  ))}
                </div>
                <p className="text-sm text-muted-foreground">
                  {key.siteIds.length === 0
                    ? 'All sites'
                    : key.siteIds.map((id) => siteName.get(id) ?? 'A site').join(', ')}
                </p>
                <p className="text-sm text-muted-foreground">
                  Created {formatDateTime(key.createdAt)} ·{' '}
                  {key.lastUsedAt ? `last used ${formatDateTime(key.lastUsedAt)}` : 'never used'}
                  {key.expiresAt && ` · expires ${formatDateTime(key.expiresAt)}`}
                  {key.revokedAt && ` · revoked ${formatDateTime(key.revokedAt)}`}
                </p>
              </div>
              {!key.revokedAt && (
                <Button
                  variant="outline"
                  size="sm"
                  aria-label={`Revoke API key ${key.name}`}
                  onClick={() => setRevoking(key)}
                >
                  <Ban aria-hidden />
                  Revoke
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      {creating && (
        <CreateKeyDialog
          sites={sites.data?.items ?? []}
          canSeeSites={canSeeSites}
          onCreated={setRevealed}
          onClose={() => setCreating(false)}
        />
      )}
      {revoking && (
        <ConfirmDialog
          title="Revoke API key?"
          description={`"${revoking.name}" stops working immediately. Anything still using it will be refused. This cannot be undone.`}
          confirmLabel="Revoke key"
          successMessage="API key revoked."
          areas={[AREAS.apiKeys]}
          run={() =>
            unwrap(
              client.POST('/api/v1/api-keys/{id}/revoke', {
                params: { path: { id: revoking.id } },
              }),
            )
          }
          onClose={() => setRevoking(null)}
        />
      )}
      {revealed && (
        <SecretDialog
          title="API key created"
          label="API key"
          value={revealed}
          onClose={() => setRevealed(null)}
        />
      )}
    </section>
  )
}

function CreateKeyDialog({
  sites,
  canSeeSites,
  onCreated,
  onClose,
}: {
  sites: { id: string; name: string }[]
  canSeeSites: boolean
  onCreated: (key: string) => void
  onClose: () => void
}) {
  const { client } = useAuthService()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<KeyValues>({
    resolver: zodResolver(keySchema),
    defaultValues: {
      name: '',
      scopes: API_KEY_SCOPES.map((s) => s.value),
      siteIds: [],
      expiresOn: '',
    },
  })
  const scopes = useWatch({ control: form.control, name: 'scopes' })
  const siteIds = useWatch({ control: form.control, name: 'siteIds' })

  const mutation = useSettingsMutation({
    run: async (values: KeyValues) => {
      const created = await unwrap(
        client.POST('/api/v1/api-keys', {
          body: {
            name: values.name,
            scopes: values.scopes,
            siteIds: values.siteIds.length > 0 ? values.siteIds : null,
            expiresAt: values.expiresOn ? endOfDay(values.expiresOn).toISOString() : null,
          },
        }),
      )
      // The key goes straight to the one-time dialog; only the key's details are returned (and cached).
      onCreated(created.key)
      return created.apiKey
    },
    areas: [AREAS.apiKeys],
    onSuccess: onClose,
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(
            error,
            ['name', 'scopes', 'siteIds', 'expiresOn'],
            form.setError,
            {},
            {
              expiresAt: 'expiresOn',
            },
          ),
        ),
    })
  })
  const pick = (field: 'scopes' | 'siteIds', current: string[], value: string, on: boolean) =>
    form.setValue(field, on ? [...current, value] : current.filter((v) => v !== value))

  return (
    <FormDialog
      title="Create API key"
      submitLabel="Create key"
      pending={mutation.isPending}
      errors={general}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <FormField
        label="Name"
        autoComplete="off"
        placeholder="Kitchen fridge gateway"
        error={form.formState.errors.name}
        {...form.register('name')}
      />
      <fieldset className="flex flex-col gap-1">
        <legend className="mb-1 text-sm font-medium">Scopes</legend>
        {API_KEY_SCOPES.map((scope) => (
          <label key={scope.value} className="flex min-h-9 items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4 accent-primary"
              checked={scopes.includes(scope.value)}
              onChange={(e) => pick('scopes', scopes, scope.value, e.target.checked)}
            />
            {scope.label} <code className="text-xs text-muted-foreground">{scope.value}</code>
          </label>
        ))}
        {form.formState.errors.scopes && (
          <p role="alert" className="text-sm text-destructive">
            {form.formState.errors.scopes.message}
          </p>
        )}
      </fieldset>
      <fieldset className="flex flex-col gap-1">
        <legend className="mb-1 text-sm font-medium">Sites</legend>
        {canSeeSites ? (
          <>
            <p className="text-sm text-muted-foreground">
              Leave all unticked for every site, if you have access to all of them.
            </p>
            <div className="grid max-h-44 gap-1 overflow-y-auto sm:grid-cols-2">
              {sites.map((site) => (
                <label key={site.id} className="flex min-h-9 items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    className="size-4 accent-primary"
                    checked={siteIds.includes(site.id)}
                    onChange={(e) => pick('siteIds', siteIds, site.id, e.target.checked)}
                  />
                  {site.name}
                </label>
              ))}
            </div>
          </>
        ) : (
          <p className="text-sm text-muted-foreground">
            You need permission to view sites to restrict a key to some of them.
          </p>
        )}
        {form.formState.errors.siteIds && (
          <p role="alert" className="text-sm text-destructive">
            {form.formState.errors.siteIds.message}
          </p>
        )}
      </fieldset>
      <FormField
        label="Expires on"
        type="date"
        hint="Optional. The key stops working at the end of this day."
        error={form.formState.errors.expiresOn}
        {...form.register('expiresOn')}
      />
    </FormDialog>
  )
}
