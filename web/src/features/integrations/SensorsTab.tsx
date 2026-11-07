import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Check, Copy, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState, type ComponentProps } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { selectClass } from '@/features/reports/Filters'
import { applyServerError } from '@/features/settings/formErrors'
import {
  ConfirmDialog,
  EmptyState,
  FormDialog,
  Hint,
  ListSkeleton,
  LoadError,
  SectionHeader,
} from '@/features/settings/shared'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'
import { sitesQuery } from '@/features/today/queries'
import { formatDateTime } from '@/lib/dates'

import {
  AREAS,
  recordLogTemplatesQuery,
  sensorDevicesQuery,
  templateDetailQuery,
  type SensorDevice,
} from './queries'
import { readingsExample, readingsUrl } from './readings'

const deviceSchema = z.object({
  externalId: z.string().trim().min(1, 'Enter the id the device sends.').max(100, 'Too long.'),
  name: z.string().trim().min(1, 'Enter a name.').max(200, 'The name is too long.'),
  siteId: z.string().min(1, 'Choose a site.'),
  templateId: z.string().min(1, 'Choose a record log template.'),
  fieldKey: z.string().min(1, 'Choose the temperature field.'),
})
type DeviceValues = z.infer<typeof deviceSchema>

export function SensorsTab() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canSeeSites = usePermission(Permissions.Sites.View)
  const canSeeTemplates = usePermission(Permissions.Templates.View)
  const [page, setPage] = useState(1)
  const devices = useQuery(sensorDevicesQuery(client, organizationId, page))
  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const templates = useQuery({
    ...recordLogTemplatesQuery(client, organizationId),
    enabled: canSeeTemplates,
  })
  const [editing, setEditing] = useState<SensorDevice | 'new' | null>(null)
  const [deleting, setDeleting] = useState<SensorDevice | null>(null)
  const siteName = new Map((sites.data?.items ?? []).map((s) => [s.id, s.name]))
  const templateName = new Map((templates.data?.items ?? []).map((t) => [t.id, t.name]))
  const canAdd = canSeeSites && canSeeTemplates

  return (
    <section className="flex flex-col gap-4" aria-label="Sensors">
      <SectionHeader
        title="Sensors"
        description="Map each device to a temperature field of a record log. Readings it sends fill that log automatically."
        action={
          <Button onClick={() => setEditing('new')} disabled={!canAdd}>
            <Plus aria-hidden />
            Add device
          </Button>
        }
      />
      {!canAdd && (
        <Hint>Adding or editing a device needs permission to view sites and templates.</Hint>
      )}
      {devices.isPending ? (
        <ListSkeleton />
      ) : devices.isError ? (
        <LoadError error={devices.error} retry={() => void devices.refetch()} />
      ) : devices.data.items.length === 0 ? (
        <EmptyState>No sensor devices yet.</EmptyState>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {devices.data.items.map((device) => (
              <li
                key={device.id}
                className="flex flex-wrap items-center gap-3 rounded-xl border bg-card p-3"
              >
                <div className="flex min-w-0 flex-1 flex-col gap-1">
                  <span className="font-medium">{device.name}</span>
                  <p className="text-sm text-muted-foreground">
                    <code className="font-mono text-xs">{device.externalId}</code> ·{' '}
                    {siteName.get(device.siteId) ?? 'Site'} ·{' '}
                    {templateName.get(device.templateId) ?? 'Template'} / {device.fieldKey}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {device.lastReadingAt
                      ? `Last reading ${device.lastValue ?? ''} at ${formatDateTime(device.lastReadingAt)}`
                      : 'No readings yet'}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Edit device ${device.name}`}
                  disabled={!canAdd}
                  onClick={() => setEditing(device)}
                >
                  <Pencil aria-hidden />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Delete device ${device.name}`}
                  onClick={() => setDeleting(device)}
                >
                  <Trash2 aria-hidden />
                </Button>
              </li>
            ))}
          </ul>
          {(page > 1 || devices.data.hasNextPage) && (
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
                Page {devices.data.page} of {Math.max(devices.data.totalPages ?? 1, 1)}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!devices.data.hasNextPage}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
      <HowItWorks />
      {editing && (
        <DeviceDialog
          device={editing === 'new' ? undefined : editing}
          sites={sites.data?.items ?? []}
          templates={templates.data?.items ?? []}
          onClose={() => setEditing(null)}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title="Delete device?"
          description={`"${deleting.name}" will stop being matched to readings. Readings already recorded are kept.`}
          confirmLabel="Delete device"
          successMessage="Device deleted."
          areas={[AREAS.devices]}
          run={() =>
            unwrap(
              client.DELETE('/api/v1/sensor-devices/{id}', {
                params: { path: { id: deleting.id } },
              }),
            )
          }
          onClose={() => setDeleting(null)}
        />
      )}
    </section>
  )
}

function HowItWorks() {
  const [copied, setCopied] = useState(false)
  const url = readingsUrl(import.meta.env.VITE_API_BASE_URL, window.location.origin)
  const example = readingsExample(url)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(example)
      setCopied(true)
    } catch {
      toast.error('Could not copy. Select the text and copy it by hand.')
    }
  }
  return (
    <section aria-labelledby="sensor-how" className="flex flex-col gap-2 rounded-xl border p-4">
      <h3 id="sensor-how" className="font-semibold">
        Sending readings
      </h3>
      <p className="text-sm text-muted-foreground">
        Create an API key with the <code>sensors.write</code> scope on the API keys tab, then have
        each device (or its gateway) post readings to <code>{url}</code> with the key in the{' '}
        <code>X-Api-Key</code> header. <code>deviceId</code> is the device id you entered above.
      </p>
      <pre
        data-testid="sensor-example"
        className="overflow-x-auto rounded-md border bg-muted p-3 font-mono text-xs"
      >
        {example}
      </pre>
      <div>
        <Button variant="outline" size="sm" onClick={() => void copy()}>
          {copied ? <Check aria-hidden /> : <Copy aria-hidden />}
          {copied ? 'Copied' : 'Copy example'}
        </Button>
      </div>
    </section>
  )
}

function DeviceDialog({
  device,
  sites,
  templates,
  onClose,
}: {
  device?: SensorDevice
  sites: { id: string; name: string }[]
  templates: { id: string; name: string }[]
  onClose: () => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<DeviceValues>({
    resolver: zodResolver(deviceSchema),
    defaultValues: {
      externalId: device?.externalId ?? '',
      name: device?.name ?? '',
      siteId: device?.siteId ?? '',
      templateId: device?.templateId ?? '',
      fieldKey: device?.fieldKey ?? '',
    },
  })
  const templateId = useWatch({ control: form.control, name: 'templateId' })
  const detail = useQuery({
    ...templateDetailQuery(client, organizationId, templateId),
    enabled: templateId !== '',
  })
  const temperatureFields = (detail.data?.fields ?? []).filter((f) => f.type === 'Temperature')
  const { errors } = form.formState

  const mutation = useSettingsMutation({
    run: (values: DeviceValues) => {
      const body = { ...values, externalId: values.externalId.trim(), name: values.name.trim() }
      return device
        ? unwrap(
            client.PUT('/api/v1/sensor-devices/{id}', {
              params: { path: { id: device.id } },
              body,
            }),
          )
        : unwrap(client.POST('/api/v1/sensor-devices', { body }))
    },
    areas: [AREAS.devices],
    onSuccess: () => {
      toast.success(device ? 'Device saved.' : 'Device added.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(
            error,
            ['externalId', 'name', 'siteId', 'templateId', 'fieldKey'],
            form.setError,
            { 'sensor_device.external_id_taken': 'externalId' },
          ),
        ),
    })
  })

  return (
    <FormDialog
      title={device ? 'Edit device' : 'Add device'}
      submitLabel={device ? 'Save' : 'Add device'}
      pending={mutation.isPending}
      errors={general}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <FormField
        label="Device id"
        autoComplete="off"
        hint="The deviceId the sensor sends with each reading."
        error={errors.externalId}
        {...form.register('externalId')}
      />
      <FormField
        label="Name"
        autoComplete="off"
        placeholder="Walk-in fridge"
        error={errors.name}
        {...form.register('name')}
      />
      <SelectField label="Site" error={errors.siteId?.message} {...form.register('siteId')}>
        <option value="">Choose a site</option>
        {sites.map((s) => (
          <option key={s.id} value={s.id}>
            {s.name}
          </option>
        ))}
      </SelectField>
      <SelectField
        label="Record log template"
        error={errors.templateId?.message}
        {...form.register('templateId', { onChange: () => form.setValue('fieldKey', '') })}
      >
        <option value="">Choose a template</option>
        {templates.map((t) => (
          <option key={t.id} value={t.id}>
            {t.name}
          </option>
        ))}
      </SelectField>
      <SelectField
        label="Temperature field"
        error={errors.fieldKey?.message}
        key={detail.data?.id ?? 'none'}
        disabled={templateId === '' || detail.isPending}
        {...form.register('fieldKey')}
      >
        <option value="">
          {templateId !== '' && !detail.isPending && temperatureFields.length === 0
            ? 'No temperature fields'
            : 'Choose a field'}
        </option>
        {temperatureFields.map((f) => (
          <option key={f.key} value={f.key}>
            {f.label}
            {f.unit ? ` (${f.unit})` : ''}
          </option>
        ))}
      </SelectField>
    </FormDialog>
  )
}

function SelectField({
  label,
  error,
  children,
  ...props
}: { label: string; error?: string } & ComponentProps<'select'>) {
  const id = `sel-${label.replace(/\W+/g, '-').toLowerCase()}`
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <select id={id} className={selectClass} aria-invalid={error ? true : undefined} {...props}>
        {children}
      </select>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
