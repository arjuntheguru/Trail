import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Link, useBlocker, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Loader2, Plus } from 'lucide-react'
import { useMemo, useRef, useState, type FormEvent } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert, FormField } from '@/components/FormField'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button, buttonVariants } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { tagsQuery } from '@/features/settings/queries'
import { ListSkeleton, LoadError, RequirePermission } from '@/features/settings/shared'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'

import {
  addField,
  draftIssues,
  moveField,
  removeField,
  snapshotOf,
  toDraftField,
  toTemplateField,
  updateField,
  type DraftField,
} from './draft'
import { FieldCard } from './FieldCard'
import { Preview } from './Preview'
import { templateQuery } from './queries'
import { bareMessage, mapTemplateError, type MappedTemplateError } from './serverErrors'
import {
  FIELD_TYPE_LABELS,
  FIELD_TYPES,
  KIND_HINTS,
  KIND_LABELS,
  KINDS,
  LIMITS,
  type FieldType,
  type Template,
  type TemplateBody,
} from './types'
import { validateFields } from './validate'

// Mirrors the API's limits (TaskTemplate.NameMaxLength / DescriptionMaxLength); the server stays the authority.
const headerSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Enter a name.')
    .max(LIMITS.name, `The name can be at most ${LIMITS.name} characters.`),
  description: z
    .string()
    .trim()
    .max(LIMITS.description, `The description can be at most ${LIMITS.description} characters.`),
  kind: z.enum(KINDS),
  requiresApproval: z.boolean(),
  requiresPhoto: z.boolean(),
  tagIds: z.array(z.string()),
})
type HeaderValues = z.infer<typeof headerSchema>

const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

function toHeader(template?: Template): HeaderValues {
  return {
    name: template?.name ?? '',
    description: template?.description ?? '',
    kind: template?.kind ?? 'Checklist',
    requiresApproval: template?.requiresApproval ?? false,
    requiresPhoto: template?.requiresPhoto ?? false,
    tagIds: [...(template?.tagIds ?? [])],
  }
}

export function TemplateEditorPage({ templateId }: { templateId?: string }) {
  return (
    <RequirePermission permission={Permissions.Templates.Manage}>
      <TemplateLoader templateId={templateId} />
    </RequirePermission>
  )
}

function TemplateLoader({ templateId }: { templateId?: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const template = useQuery({
    ...templateQuery(client, organizationId, templateId ?? ''),
    enabled: !!templateId,
    // The editor copies the template once; a background refetch must not be mistaken for the user's edits.
    refetchOnWindowFocus: false,
  })

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/settings/templates"
        className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Back to templates
      </Link>
      <h2 className="text-xl font-semibold">{templateId ? 'Edit template' : 'New template'}</h2>
      {templateId && template.isPending ? (
        <ListSkeleton rows={3} />
      ) : templateId && template.isError ? (
        <LoadError error={template.error} retry={() => void template.refetch()} />
      ) : (
        <TemplateEditor template={template.data} />
      )}
    </section>
  )
}

function TemplateEditor({ template }: { template?: Template }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const navigate = useNavigate()
  const tags = useQuery(tagsQuery(client, organizationId))

  const [initial] = useState(() => ({
    header: toHeader(template),
    fields: (template?.fields ?? []).map(toDraftField),
  }))
  const [fields, setFields] = useState<DraftField[]>(initial.fields)
  const [newType, setNewType] = useState<FieldType>('Checkbox')
  const [attempted, setAttempted] = useState(false)
  const [server, setServer] = useState<MappedTemplateError>({ byField: {}, general: [] })
  const [stopped, setStopped] = useState<string[]>([])

  const form = useForm<HeaderValues>({
    resolver: zodResolver(headerSchema),
    defaultValues: initial.header,
  })
  const { errors } = form.formState
  const header = useWatch({ control: form.control }) as HeaderValues

  // --- unsaved changes ---
  const snapshot = (h: HeaderValues, f: readonly DraftField[]) =>
    JSON.stringify([{ ...h, tagIds: [...(h.tagIds ?? [])].sort() }, snapshotOf(f)])
  const [initialSnapshot] = useState(() => snapshot(initial.header, initial.fields))
  const dirty = snapshot(header, fields) !== initialSnapshot
  const saved = useRef(false)
  const blocker = useBlocker({
    shouldBlockFn: () => dirty && !saved.current,
    enableBeforeUnload: () => dirty && !saved.current,
    withResolver: true,
  })

  // --- field problems ---
  const templateFields = useMemo(() => fields.map(toTemplateField), [fields])
  const issues = useMemo(() => validateFields(templateFields), [templateFields])
  const textIssues = useMemo(() => draftIssues(fields), [fields])
  const problemsByField = useMemo(() => {
    const byId: Record<string, string[]> = {}
    const add = (index: number, message: string) => {
      const id = fields[index]?.id
      if (id && !(byId[id] ??= []).includes(message)) byId[id].push(message)
    }
    for (const issue of issues) for (const i of issue.indexes) add(i, bareMessage(issue.message))
    for (const { index, message } of textIssues) add(index, message)
    for (const [id, messages] of Object.entries(server.byField)) {
      for (const m of messages) if (!(byId[id] ??= []).includes(m)) byId[id].push(m)
    }
    return byId
  }, [issues, textIssues, server.byField, fields])
  const listIssues = issues.filter((i) => i.indexes.length === 0).map((i) => i.message)
  const hasClientProblems = issues.length > 0 || textIssues.length > 0

  // Editing clears what the server said about the old content.
  const editFields = (next: DraftField[]) => {
    setFields(next)
    setServer((s) => ({ ...s, byField: {}, general: [] }))
  }

  const mutation = useSettingsMutation({
    run: (body: TemplateBody) =>
      template
        ? unwrap(
            client.PUT('/api/v1/templates/{id}', { params: { path: { id: template.id } }, body }),
          )
        : unwrap(client.POST('/api/v1/templates', { body })),
    areas: [['templates']],
    onSuccess: () => {
      saved.current = true
      toast.success(template ? 'Template saved.' : 'Template created.')
      void navigate({ to: '/settings/templates' })
    },
  })

  const submit = form.handleSubmit((values) => {
    setStopped([])
    if (hasClientProblems) {
      setStopped(['Fix the highlighted problems before saving.'])
      return
    }
    mutation.mutate(
      {
        name: values.name,
        description: values.description || null,
        kind: values.kind,
        fields: templateFields,
        requiresApproval: values.requiresApproval,
        requiresPhoto: values.requiresPhoto,
        tagIds: values.tagIds,
      },
      {
        onError: (error) => {
          const mapped = mapTemplateError(error, fields)
          if (mapped.name) form.setError('name', { type: 'server', message: mapped.name })
          if (mapped.description)
            form.setError('description', { type: 'server', message: mapped.description })
          setServer(mapped)
        },
      },
    )
  })
  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    setAttempted(true)
    setServer({ byField: {}, general: [] })
    void submit(e)
  }

  const toggleTag = (id: string, on: boolean) => {
    const current = form.getValues('tagIds')
    form.setValue('tagIds', on ? [...current, id] : current.filter((t) => t !== id), {
      shouldDirty: true,
    })
  }

  const fieldProblems = (field: DraftField) =>
    attempted || (server.byField[field.id]?.length ?? 0) > 0
      ? (problemsByField[field.id] ?? [])
      : []

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
      <form onSubmit={onSubmit} noValidate className="flex min-w-0 flex-col gap-5">
        <FormField label="Name" autoComplete="off" error={errors.name} {...form.register('name')} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="template-description">Description</Label>
          <Textarea
            id="template-description"
            aria-invalid={errors.description ? true : undefined}
            {...form.register('description')}
          />
          {errors.description && (
            <p role="alert" className="text-sm text-destructive">
              {errors.description.message}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="template-kind">Kind</Label>
          <select id="template-kind" className={selectClass} {...form.register('kind')}>
            {KINDS.map((k) => (
              <option key={k} value={k}>
                {KIND_LABELS[k]}
              </option>
            ))}
          </select>
          <p className="text-sm text-muted-foreground">{KIND_HINTS[header.kind]}</p>
        </div>

        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 font-medium">Requirements</legend>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-5 accent-primary"
              {...form.register('requiresApproval')}
            />
            A manager must approve completed tasks
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-5 accent-primary"
              {...form.register('requiresPhoto')}
            />
            At least one photo must be attached on completion
          </label>
        </fieldset>

        {(tags.data?.length ?? 0) > 0 && (
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 font-medium">Tags</legend>
            <div className="flex flex-wrap gap-x-4 gap-y-2">
              {tags.data?.map((tag) => (
                <label key={tag.id} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    className="size-5 accent-primary"
                    checked={header.tagIds?.includes(tag.id) ?? false}
                    onChange={(e) => toggleTag(tag.id, e.target.checked)}
                  />
                  {tag.name}
                </label>
              ))}
            </div>
          </fieldset>
        )}

        <section aria-label="Fields" className="flex flex-col gap-3">
          <h3 className="font-medium">Fields</h3>
          <p className="text-sm text-muted-foreground">
            The questions and checks people complete. Use the arrows, or Alt with the up and down
            keys, to change the order.
          </p>
          {attempted && listIssues.length > 0 && <FormAlert messages={listIssues} />}
          {fields.length > 0 && (
            <ol className="flex flex-col gap-3">
              {fields.map((field, index) => (
                <FieldCard
                  key={field.id}
                  field={field}
                  index={index}
                  count={fields.length}
                  problems={fieldProblems(field)}
                  onChange={(patch) => editFields(updateField(fields, field.id, patch))}
                  onReplace={(next) =>
                    editFields(fields.map((f) => (f.id === field.id ? next : f)))
                  }
                  onMove={(delta) => editFields(moveField(fields, field.id, delta))}
                  onRemove={() => editFields(removeField(fields, field.id))}
                />
              ))}
            </ol>
          )}
          <div className="flex flex-wrap items-center gap-2">
            <select
              aria-label="New field type"
              className={`${selectClass} w-auto`}
              value={newType}
              onChange={(e) => setNewType(e.target.value as FieldType)}
            >
              {FIELD_TYPES.map((t) => (
                <option key={t} value={t}>
                  {FIELD_TYPE_LABELS[t]}
                </option>
              ))}
            </select>
            <Button
              type="button"
              variant="outline"
              disabled={fields.length >= LIMITS.maxFields}
              onClick={() => editFields(addField(fields, newType))}
            >
              <Plus aria-hidden />
              Add field
            </Button>
          </div>
        </section>

        <FormAlert messages={[...stopped, ...server.general]} />
        <div className="flex gap-2">
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
            {template ? 'Save template' : 'Create template'}
          </Button>
          <Link to="/settings/templates" className={buttonVariants({ variant: 'outline' })}>
            Cancel
          </Link>
        </div>
      </form>

      <aside aria-label="Preview" className="flex min-w-0 flex-col gap-3 lg:sticky lg:top-4">
        <h3 className="font-medium">Preview</h3>
        <p className="text-sm text-muted-foreground">
          How the fields look to the person completing the task. Nothing entered here is saved.
        </p>
        <Preview fields={templateFields} />
      </aside>

      {blocker.status === 'blocked' && (
        <AlertDialog open onOpenChange={(open) => !open && blocker.reset()}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Discard unsaved changes?</AlertDialogTitle>
              <AlertDialogDescription>
                Your changes to this template have not been saved and will be lost.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <Button variant="outline" onClick={() => blocker.reset()}>
                Keep editing
              </Button>
              <Button variant="destructive" onClick={() => blocker.proceed()}>
                Discard changes
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  )
}
