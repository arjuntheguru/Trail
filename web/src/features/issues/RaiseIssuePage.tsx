import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { ApiError, errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { PhotoPicker } from '@/components/Photos'
import { useUploadTracker } from '@/components/useUploadTracker'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { sitesQuery } from '@/features/today/queries'
import { useSelectedSite } from '@/features/today/useSelectedSite'
import { fromLocalInput, isPastLocalInput } from '@/lib/dates'

import { taskQuery } from '@/features/tasks/queries'

import { PRIORITY_LABEL } from './labels'
import { applyIssueUpdate } from './queries'
import { PRIORITIES } from './types'

// Mirrors the API's limits (Issue.TitleMaxLength / DescriptionMaxLength); the server stays the authority.
const raiseSchema = z.object({
  siteId: z.string().min(1, 'Choose a site.'),
  title: z.string().trim().min(1, 'Enter a title.').max(200, 'The title is too long.'),
  description: z.string().trim().max(5000, 'The description is too long.'),
  priority: z.enum(PRIORITIES),
  dueAt: z.string(),
})
type RaiseValues = z.infer<typeof raiseSchema>
type RaiseField = keyof RaiseValues

const FIELDS: readonly RaiseField[] = ['siteId', 'title', 'description', 'priority', 'dueAt']

/** Error codes that carry no property name but belong to one of the form's fields. */
const CODE_FIELD: Record<string, RaiseField> = {
  'issue.unknown_site': 'siteId',
  'issue.site_inactive': 'siteId',
  'issue.due_in_past': 'dueAt',
}

/** Failures about the linked task are shown as a banner with what to do about them. */
const CODE_MESSAGE: Record<string, string> = {
  'issue.unknown_task':
    'The linked task could not be found. Remove the link to raise the issue without it.',
  'issue.task_site_mismatch':
    "The linked task belongs to a different site. Remove the link or choose the task's site.",
}

const selectClass = 'h-12 w-full rounded-lg border border-input bg-card px-3 text-base'

export function RaiseIssuePage({
  initialSiteId,
  taskId,
}: {
  initialSiteId?: string
  taskId?: string
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canRaise = usePermission(Permissions.Issues.Raise)
  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canRaise })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  // A linked task decides the site and is shown on the form; it is optional, so a failure only drops the link.
  const task = useQuery({
    ...taskQuery(client, organizationId, taskId ?? ''),
    enabled: canRaise && !!taskId,
    retry: false,
  })
  const [remembered] = useSelectedSite(
    organizationId ?? '',
    siteList.map((s) => s.id),
  )

  if (!canRaise) {
    return (
      <section className="flex flex-col gap-2 py-12 text-center">
        <h1 className="text-2xl font-semibold">Raise an issue</h1>
        <p className="text-muted-foreground">You do not have permission to raise issues.</p>
      </section>
    )
  }
  if (sites.isError) {
    return (
      <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
        <p>{errorMessage(sites.error)}</p>
        <Button variant="outline" onClick={() => void sites.refetch()}>
          Try again
        </Button>
      </div>
    )
  }
  if (sites.isPending) {
    return (
      <div className="flex flex-col gap-4" role="status" aria-label="Loading">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-32 w-full" />
      </div>
    )
  }
  if (taskId && task.isPending) {
    return (
      <div className="flex flex-col gap-4" role="status" aria-label="Loading">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-12 w-full" />
      </div>
    )
  }
  if (siteList.length === 0) {
    return (
      <section className="flex flex-col gap-2 py-12 text-center">
        <h1 className="text-2xl font-semibold">Raise an issue</h1>
        <p className="text-muted-foreground">You do not have access to any sites yet.</p>
      </section>
    )
  }

  const linked =
    task.data && siteList.some((s) => s.id === task.data.siteId) ? task.data : undefined
  const preselected =
    linked?.siteId ??
    (initialSiteId && siteList.some((s) => s.id === initialSiteId) ? initialSiteId : remembered)
  return (
    <RaiseForm
      sites={siteList}
      initialSiteId={preselected ?? ''}
      linkedTask={linked ? { id: linked.id, title: linked.title } : undefined}
      linkProblem={
        taskId && !linked
          ? 'The linked task could not be loaded, so it was not attached.'
          : undefined
      }
    />
  )
}

function RaiseForm({
  sites,
  initialSiteId,
  linkedTask,
  linkProblem,
}: {
  sites: { id: string; name: string; timeZone?: string | null }[]
  initialSiteId: string
  linkedTask?: { id: string; title: string }
  linkProblem?: string
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const uploads = useUploadTracker()
  const [photoIds, setPhotoIds] = useState<string[]>([])
  const [formErrors, setFormErrors] = useState<string[]>(linkProblem ? [linkProblem] : [])
  const [taskLink, setTaskLink] = useState(linkedTask)

  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors },
  } = useForm<RaiseValues>({
    resolver: zodResolver(raiseSchema),
    defaultValues: {
      siteId: initialSiteId,
      title: '',
      description: '',
      priority: 'Medium',
      dueAt: '',
    },
  })
  const siteId = useWatch({ control, name: 'siteId' })
  const timeZone = sites.find((s) => s.id === siteId)?.timeZone ?? undefined

  const raise = useMutation({
    mutationFn: (values: RaiseValues) =>
      unwrap(
        client.POST('/api/v1/issues', {
          body: {
            title: values.title,
            description: values.description || null,
            siteId: values.siteId,
            departmentId: null,
            priority: values.priority,
            dueAt: values.dueAt ? fromLocalInput(values.dueAt, timeZone) : null,
            siteTaskId: taskLink?.id ?? null,
            assigneeId: null,
            photoIds,
          },
        }),
      ),
    onSuccess: (issue) => {
      applyIssueUpdate(queryClient, organizationId, issue)
      toast.success(`Issue #${issue.number} raised.`)
      void navigate({ to: '/issues/$issueId', params: { issueId: issue.id } })
    },
    onError: (error) => {
      const general: string[] = []
      if (error instanceof ApiError && error.code && CODE_MESSAGE[error.code]) {
        general.push(CODE_MESSAGE[error.code]!)
      } else if (error instanceof ApiError && error.code && CODE_FIELD[error.code]) {
        setError(CODE_FIELD[error.code]!, { type: 'server', message: errorMessage(error) })
      } else if (error instanceof ApiError && Object.keys(error.errors).length > 0) {
        for (const [key, messages] of Object.entries(error.errors)) {
          const field = FIELDS.find((f) => f.toLowerCase() === key.toLowerCase())
          if (field) setError(field, { type: 'server', message: messages.join(' ') })
          else general.push(messages.join(' '))
        }
      } else {
        general.push(errorMessage(error))
      }
      setFormErrors(general)
    },
  })

  const submit = handleSubmit((values) => {
    setFormErrors([])
    if (values.dueAt && isPastLocalInput(values.dueAt, timeZone)) {
      setError('dueAt', { type: 'validate', message: 'Pick a time in the future.' })
      return
    }
    raise.mutate(values)
  })

  return (
    <section className="flex flex-col gap-5">
      <header className="flex flex-col gap-2">
        <Link
          to="/issues"
          className="-ml-2 inline-flex h-11 w-fit items-center gap-1 rounded-md px-2 text-base font-medium text-muted-foreground"
        >
          <ArrowLeft className="size-5" aria-hidden />
          Issues
        </Link>
        <h1 className="text-2xl font-semibold tracking-tight">Raise an issue</h1>
      </header>

      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormAlert messages={formErrors} />

        {taskLink && (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border p-3">
            <p className="text-sm">
              Linked task: <span className="font-medium">{taskLink.title}</span>
              <span className="block text-muted-foreground">
                Site: {sites.find((s) => s.id === siteId)?.name}
              </span>
            </p>
            <Button type="button" variant="ghost" size="lg" onClick={() => setTaskLink(undefined)}>
              Remove link
            </Button>
          </div>
        )}

        {!taskLink && (
          <Field label="Site" htmlFor="issue-site" error={errors.siteId?.message}>
            <select
              id="issue-site"
              className={selectClass}
              aria-invalid={errors.siteId ? true : undefined}
              {...register('siteId', {
                // Photos belong to one site, so a different site starts with none.
                onChange: () => setPhotoIds([]),
              })}
            >
              <option value="">Choose a site…</option>
              {sites.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </Field>
        )}

        <Field label="Title" htmlFor="issue-title" error={errors.title?.message}>
          <Input
            id="issue-title"
            className="h-12"
            maxLength={200}
            aria-invalid={errors.title ? true : undefined}
            {...register('title')}
          />
        </Field>

        <Field
          label="Description (optional)"
          htmlFor="issue-description"
          error={errors.description?.message}
        >
          <Textarea
            id="issue-description"
            className="min-h-28"
            aria-invalid={errors.description ? true : undefined}
            {...register('description')}
          />
        </Field>

        <Field label="Priority" htmlFor="issue-priority" error={errors.priority?.message}>
          <select id="issue-priority" className={selectClass} {...register('priority')}>
            {PRIORITIES.map((p) => (
              <option key={p} value={p}>
                {PRIORITY_LABEL[p]}
              </option>
            ))}
          </select>
        </Field>

        <Field
          label={`Due (optional, ${timeZone ? 'site time' : 'your time'})`}
          htmlFor="issue-due"
          error={errors.dueAt?.message}
          hint="Leave blank to use the default response time for the priority."
        >
          <Input
            id="issue-due"
            type="datetime-local"
            className="h-12"
            aria-invalid={errors.dueAt ? true : undefined}
            {...register('dueAt')}
          />
        </Field>

        <div className="flex flex-col gap-2" data-section="photos">
          <Label>Photos (optional)</Label>
          {siteId ? (
            <PhotoPicker
              label="Issue photo"
              siteId={siteId}
              value={photoIds}
              onChange={setPhotoIds}
              trackUpload={uploads.track}
            />
          ) : (
            <p className="text-sm text-muted-foreground">Choose a site to add photos.</p>
          )}
        </div>

        <Button type="submit" size="lg" disabled={raise.isPending || uploads.pending > 0}>
          {raise.isPending && <Loader2 className="animate-spin" aria-hidden />}
          {uploads.pending > 0 ? 'Uploading photos…' : raise.isPending ? 'Raising…' : 'Raise issue'}
        </Button>
      </form>
    </section>
  )
}

function Field({
  label,
  htmlFor,
  error,
  hint,
  children,
}: {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : hint ? (
        <p className="text-sm text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  )
}
