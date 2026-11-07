import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import {
  ChevronDown,
  ChevronUp,
  KeyRound,
  Pencil,
  Plus,
  RotateCw,
  Send,
  Trash2,
} from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
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
import { formatDateTime } from '@/lib/dates'

import {
  AREAS,
  deliveriesQuery,
  type Webhook,
  type WebhookDelivery,
  webhookEventTypesQuery,
  webhooksQuery,
} from './queries'
import { SecretDialog } from './SecretDialog'
import { webhookUrlProblem } from './urlRules'

const webhookSchema = z.object({
  url: z
    .string()
    .max(2000, 'The URL is too long.')
    .superRefine((value, ctx) => {
      const problem = webhookUrlProblem(value)
      if (problem) ctx.addIssue({ code: 'custom', message: problem })
    }),
  description: z.string().trim().max(300, 'The description is too long.'),
  eventTypes: z
    .array(z.string())
    .min(1, 'Choose at least one event type.')
    .max(20, 'Choose at most 20 event types.'),
  isActive: z.boolean(),
})
type WebhookValues = z.infer<typeof webhookSchema>

/** What to show once, and for which action. */
type Reveal = { title: string; secret: string }

export function WebhooksTab() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const webhooks = useQuery(webhooksQuery(client, organizationId))
  const [editing, setEditing] = useState<Webhook | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Webhook | null>(null)
  const [rotating, setRotating] = useState<Webhook | null>(null)
  const [reveal, setReveal] = useState<Reveal | null>(null)
  const [open, setOpen] = useState<string | null>(null)

  return (
    <section className="flex flex-col gap-4" aria-label="Webhooks">
      <SectionHeader
        title="Webhooks"
        description="Trail calls your URL when things happen, signed with a secret so you can verify it came from us."
        action={
          <Button onClick={() => setEditing('new')}>
            <Plus aria-hidden />
            Add webhook
          </Button>
        }
      />
      {webhooks.isPending ? (
        <ListSkeleton />
      ) : webhooks.isError ? (
        <LoadError error={webhooks.error} retry={() => void webhooks.refetch()} />
      ) : webhooks.data.length === 0 ? (
        <EmptyState>No webhooks yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {webhooks.data.map((hook) => (
            <li key={hook.id} className="rounded-xl border bg-card">
              <WebhookRow
                hook={hook}
                expanded={open === hook.id}
                onToggle={() => setOpen(open === hook.id ? null : hook.id)}
                onEdit={() => setEditing(hook)}
                onRotate={() => setRotating(hook)}
                onDelete={() => setDeleting(hook)}
              />
              {open === hook.id && <Deliveries hook={hook} />}
            </li>
          ))}
        </ul>
      )}
      {editing && (
        <WebhookDialog
          hook={editing === 'new' ? undefined : editing}
          onCreated={(secret) => setReveal({ title: 'Webhook created', secret })}
          onClose={() => setEditing(null)}
        />
      )}
      {rotating && (
        <ConfirmDialog
          title="Rotate signing secret?"
          description={`The current secret for ${rotating.url} stops working straight away. You will get a new one to copy.`}
          confirmLabel="Rotate secret"
          destructive={false}
          successMessage="Secret rotated."
          areas={[AREAS.webhooks]}
          run={async () => {
            const result = await unwrap(
              client.POST('/api/v1/webhooks/{id}/rotate-secret', {
                params: { path: { id: rotating.id } },
              }),
            )
            setReveal({ title: 'New signing secret', secret: result.secret })
          }}
          onClose={() => setRotating(null)}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title="Delete webhook?"
          description={`${deleting.url} will stop receiving events and its delivery history is removed.`}
          confirmLabel="Delete webhook"
          successMessage="Webhook deleted."
          areas={[AREAS.webhooks, AREAS.deliveries]}
          run={() =>
            unwrap(
              client.DELETE('/api/v1/webhooks/{id}', { params: { path: { id: deleting.id } } }),
            )
          }
          onClose={() => setDeleting(null)}
        />
      )}
      {reveal && (
        <SecretDialog
          title={reveal.title}
          label="Signing secret"
          value={reveal.secret}
          onClose={() => setReveal(null)}
        />
      )}
    </section>
  )
}

function WebhookRow({
  hook,
  expanded,
  onToggle,
  onEdit,
  onRotate,
  onDelete,
}: {
  hook: Webhook
  expanded: boolean
  onToggle: () => void
  onEdit: () => void
  onRotate: () => void
  onDelete: () => void
}) {
  const { client } = useAuthService()
  const test = useSettingsMutation({
    run: () =>
      unwrap(client.POST('/api/v1/webhooks/{id}/test', { params: { path: { id: hook.id } } })),
    areas: [AREAS.deliveries],
    onSuccess: () => toast.success('Test event queued. Check the deliveries for the result.'),
  })

  return (
    <div className="flex flex-col gap-3 p-3">
      <div className="flex flex-wrap items-start gap-2">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <span className="font-medium break-all">{hook.url}</span>
          {hook.description && (
            <span className="text-sm text-muted-foreground">{hook.description}</span>
          )}
          <div className="flex flex-wrap gap-1">
            {hook.eventTypes.map((type) => (
              <Badge key={type} variant="outline">
                {type}
              </Badge>
            ))}
          </div>
        </div>
        {hook.disabledAt ? (
          <Badge variant="destructive">Disabled</Badge>
        ) : hook.isActive ? (
          <Badge>Active</Badge>
        ) : (
          <Badge variant="secondary">Off</Badge>
        )}
      </div>
      {hook.disabledAt && (
        <p role="status" className="text-sm text-destructive">
          Switched off automatically on {formatDateTime(hook.disabledAt)} after repeated failures.
          Edit it and turn it back on once your endpoint is fixed.
        </p>
      )}
      <p className="text-sm text-muted-foreground">
        {hook.lastSuccessAt
          ? `Last success ${formatDateTime(hook.lastSuccessAt)}`
          : 'Never succeeded'}
        {hook.lastFailureAt && ` · last failure ${formatDateTime(hook.lastFailureAt)}`}
        {hook.failureCount > 0 && ` · ${hook.failureCount} failures in a row`}
      </p>
      <div className="flex flex-wrap gap-1">
        <Button variant="outline" size="sm" onClick={onToggle} aria-expanded={expanded}>
          {expanded ? <ChevronUp aria-hidden /> : <ChevronDown aria-hidden />}
          Deliveries
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={test.isPending || !hook.isActive}
          onClick={() => test.mutate(undefined, { onError: (e) => toast.error(errorMessage(e)) })}
          aria-label={`Send test to ${hook.url}`}
        >
          <Send aria-hidden />
          Send test
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={onRotate}
          aria-label={`Rotate secret for ${hook.url}`}
        >
          <KeyRound aria-hidden />
          Rotate secret
        </Button>
        <Button
          variant="ghost"
          size="icon"
          onClick={onEdit}
          aria-label={`Edit webhook ${hook.url}`}
        >
          <Pencil aria-hidden />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          onClick={onDelete}
          aria-label={`Delete webhook ${hook.url}`}
        >
          <Trash2 aria-hidden />
        </Button>
      </div>
    </div>
  )
}

const STATUS_VARIANT: Record<WebhookDelivery['status'], 'default' | 'secondary' | 'destructive'> = {
  Succeeded: 'default',
  Failed: 'destructive',
  Pending: 'secondary',
  Sending: 'secondary',
}

function Deliveries({ hook }: { hook: Webhook }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const [page, setPage] = useState(1)
  const deliveries = useQuery(deliveriesQuery(client, organizationId, hook.id, page))

  return (
    <div className="flex flex-col gap-2 border-t p-3" aria-label={`Deliveries for ${hook.url}`}>
      {deliveries.isPending ? (
        <ListSkeleton rows={3} />
      ) : deliveries.isError ? (
        <LoadError error={deliveries.error} retry={() => void deliveries.refetch()} />
      ) : deliveries.data.items.length === 0 ? (
        <EmptyState>No deliveries yet. Send a test to try it out.</EmptyState>
      ) : (
        <>
          <ul className="flex flex-col divide-y">
            {deliveries.data.items.map((delivery) => (
              <DeliveryRow key={delivery.id} delivery={delivery} />
            ))}
          </ul>
          <div className="flex items-center justify-between gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </Button>
            <span className="text-sm text-muted-foreground">
              Page {deliveries.data.page} of {Math.max(deliveries.data.totalPages ?? 1, 1)}
            </span>
            <Button
              variant="outline"
              size="sm"
              disabled={!deliveries.data.hasNextPage}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </>
      )}
    </div>
  )
}

function DeliveryRow({ delivery }: { delivery: WebhookDelivery }) {
  const { client } = useAuthService()
  const redeliver = useSettingsMutation({
    run: () =>
      unwrap(
        client.POST('/api/v1/webhooks/deliveries/{id}/redeliver', {
          params: { path: { id: delivery.id } },
        }),
      ),
    areas: [AREAS.deliveries],
    onSuccess: () => toast.success('Delivery queued again.'),
  })
  const inFlight = delivery.status === 'Pending' || delivery.status === 'Sending'
  return (
    <li className="flex flex-col gap-1 py-2" data-testid="delivery">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={STATUS_VARIANT[delivery.status]}>{delivery.status}</Badge>
        <span className="font-medium">{delivery.eventType}</span>
        <span className="ml-auto text-sm text-muted-foreground">
          {formatDateTime(delivery.createdAt)}
        </span>
      </div>
      <p className="text-sm text-muted-foreground">
        {delivery.attempts} {delivery.attempts === 1 ? 'attempt' : 'attempts'}
        {delivery.lastStatusCode != null && ` · response ${delivery.lastStatusCode}`}
        {delivery.deliveredAt && ` · delivered ${formatDateTime(delivery.deliveredAt)}`}
        {inFlight && ` · next attempt ${formatDateTime(delivery.nextAttemptAt)}`}
      </p>
      {delivery.lastError && <p className="text-sm text-destructive">{delivery.lastError}</p>}
      <div>
        <Button
          variant="outline"
          size="sm"
          disabled={inFlight || redeliver.isPending}
          aria-label={`Redeliver ${delivery.eventType} ${delivery.id}`}
          onClick={() =>
            redeliver.mutate(undefined, { onError: (e) => toast.error(errorMessage(e)) })
          }
        >
          <RotateCw aria-hidden />
          Redeliver
        </Button>
      </div>
    </li>
  )
}

function WebhookDialog({
  hook,
  onCreated,
  onClose,
}: {
  hook?: Webhook
  onCreated: (secret: string) => void
  onClose: () => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const eventTypes = useQuery(webhookEventTypesQuery(client, organizationId))
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<WebhookValues>({
    resolver: zodResolver(webhookSchema),
    defaultValues: {
      url: hook?.url ?? '',
      description: hook?.description ?? '',
      eventTypes: hook ? [...hook.eventTypes] : [],
      isActive: hook ? hook.isActive && !hook.disabledAt : true,
    },
  })
  const selected = useWatch({ control: form.control, name: 'eventTypes' })
  const isActive = useWatch({ control: form.control, name: 'isActive' })

  const mutation = useSettingsMutation({
    run: async (values: WebhookValues) => {
      const body = {
        url: values.url.trim(),
        description: values.description || null,
        eventTypes: values.eventTypes,
      }
      if (hook) {
        return unwrap(
          client.PUT('/api/v1/webhooks/{id}', {
            params: { path: { id: hook.id } },
            body: { ...body, isActive: values.isActive },
          }),
        )
      }
      // The secret goes straight to the one-time dialog; it is not returned, so it is not kept in the mutation.
      const created = await unwrap(client.POST('/api/v1/webhooks', { body }))
      onCreated(created.secret)
      return created.webhook
    },
    areas: [AREAS.webhooks],
    onSuccess: () => {
      if (hook) toast.success('Webhook saved.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, ['url', 'description', 'eventTypes'], form.setError, {
            'webhook.limit_reached': 'url',
          }),
        ),
    })
  })
  const toggle = (type: string, checked: boolean) =>
    form.setValue(
      'eventTypes',
      checked ? [...selected, type] : selected.filter((t) => t !== type),
      { shouldValidate: form.formState.isSubmitted },
    )

  return (
    <FormDialog
      title={hook ? 'Edit webhook' : 'Add webhook'}
      submitLabel={hook ? 'Save' : 'Add webhook'}
      pending={mutation.isPending}
      errors={general}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <FormField
        label="URL"
        type="url"
        autoComplete="off"
        placeholder="https://example.com/hooks/trail"
        hint="Must be a public https address."
        error={form.formState.errors.url}
        {...form.register('url')}
      />
      <FormField
        label="Description"
        autoComplete="off"
        hint="Optional."
        error={form.formState.errors.description}
        {...form.register('description')}
      />
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-medium">Event types</legend>
        {eventTypes.isPending ? (
          <ListSkeleton rows={3} />
        ) : eventTypes.isError ? (
          <LoadError error={eventTypes.error} retry={() => void eventTypes.refetch()} />
        ) : (
          <div className="grid max-h-56 gap-1 overflow-y-auto sm:grid-cols-2">
            {eventTypes.data.map((type) => (
              <label key={type} className="flex min-h-9 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-4 accent-primary"
                  checked={selected.includes(type)}
                  onChange={(e) => toggle(type, e.target.checked)}
                />
                {type}
              </label>
            ))}
          </div>
        )}
        {form.formState.errors.eventTypes && (
          <p role="alert" className="text-sm text-destructive">
            {form.formState.errors.eventTypes.message}
          </p>
        )}
      </fieldset>
      {hook && (
        <label className="flex min-h-9 items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 accent-primary"
            checked={isActive}
            onChange={(e) => form.setValue('isActive', e.target.checked)}
          />
          Active
        </label>
      )}
    </FormDialog>
  )
}
