import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CircleCheck, Plus } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useOutboxApi, usePendingForTask } from '@/offline/context'
import { SAVED_ON_DEVICE } from '@/offline/messages'
import { isOnline } from '@/offline/online'
import type { EntryBody } from '@/offline/outbox-types'

import { emptyFormValues, toServerAnswers } from './answers'
import { evaluateAnswers } from './evaluate'
import { isConflict, isOffline, mapServerErrors } from './errors'
import { FieldsEditor } from './FieldsEditor'
import { TaskFormProvider, useTaskForm } from './form-context'
import { focusFirstProblem, NO_PROBLEMS, validateForSubmit, type FormProblems } from './problems'
import { applyTaskUpdate, taskKey } from './queries'
import { ReadingCard } from './ReadOnly'
import type { TaskDetail, TaskEntry, TaskFormValues } from './types'

interface RecordLogProps {
  task: TaskDetail
  /** False when the log can no longer be added to (finished, not open yet, or no permission). */
  editable: boolean
  onConflict: (message: string) => void
}

/** Submitted readings, a form to add the next one and a button to finish the log. */
export function RecordLogView({ task, editable, onConflict }: RecordLogProps) {
  const user = useSession().user
  const readings = useMemo(
    () =>
      task.entries
        .filter((e) => e.submittedAt)
        .sort((a, b) => (a.submittedAt! < b.submittedAt! ? 1 : -1)),
    [task.entries],
  )
  // Readings taken on this device that the server has not received yet, newest first.
  const waiting = usePendingForTask(task.id)
    .filter((i) => i.type === 'entry')
    .map((item) => ({
      item,
      entry: {
        id: item.id,
        answers: item.body.answers,
        note: item.body.note ?? null,
        photoIds: item.body.photoIds ?? [],
        failures: [],
        hasFailures: false,
        submittedAt: item.recordedAt,
        submittedById: null,
        submittedBy: `${user.firstName} ${user.lastName}`.trim() || null,
      } satisfies TaskEntry,
    }))
    .reverse()
  const total = readings.length + waiting.length

  return (
    <TaskFormProvider siteId={task.siteId} taskId={task.id} readOnly={!editable}>
      <div className="flex flex-col gap-8">
        {editable && <AddReading task={task} onConflict={onConflict} />}

        <section aria-label="Readings" className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Readings ({total})</h2>
          {total === 0 ? (
            <p className="text-muted-foreground">No readings yet.</p>
          ) : (
            <>
              {waiting.map(({ item, entry }) => (
                <ReadingCard
                  key={item.id}
                  fields={task.fields}
                  entry={entry}
                  timeZone={task.timeZone}
                  sync={item.status === 'failed' ? 'failed' : 'waiting'}
                />
              ))}
              {readings.map((entry) => (
                <ReadingCard
                  key={entry.id}
                  fields={task.fields}
                  entry={entry}
                  timeZone={task.timeZone}
                />
              ))}
            </>
          )}
        </section>

        {editable && <FinishLog task={task} count={total} onConflict={onConflict} />}
      </div>
    </TaskFormProvider>
  )
}

function AddReading({ task, onConflict }: { task: TaskDetail; onConflict: (m: string) => void }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  const { pendingUploads } = useTaskForm()
  const outbox = useOutboxApi()
  const root = useRef<HTMLFormElement>(null)
  const [problems, setProblems] = useState<FormProblems>(NO_PROBLEMS)
  const form = useForm<TaskFormValues>({ defaultValues: emptyFormValues(task.fields) })
  const values = useWatch({ control: form.control }) as TaskFormValues
  const failures = useMemo(
    () =>
      new Map(
        evaluateAnswers(
          task.fields,
          toServerAnswers(task.fields, values.answers),
          false,
        ).failures.map((f) => [f.fieldKey, f.reason]),
      ),
    [task.fields, values.answers],
  )

  const add = useMutation({
    networkMode: 'always', // offline readings are queued rather than paused
    mutationFn: async (input: EntryBody): Promise<{ queued: true } | TaskDetail> => {
      const requestId = crypto.randomUUID()
      const recordedAt = new Date().toISOString()
      const planned = await outbox.plan(task.id, input)
      const queue = async () => {
        await outbox.enqueueEntry({ taskId: task.id, body: planned.body, requestId, recordedAt })
        return { queued: true } as const
      }
      if (planned.queue || !isOnline()) return queue()
      try {
        return await unwrap(
          client.POST('/api/v1/tasks/{id}/entries', {
            params: { path: { id: task.id } },
            body: { ...planned.body, requestId },
          }),
        )
      } catch (error) {
        // The request may or may not have arrived; the queued copy carries the same requestId.
        if (isOffline(error)) return queue()
        throw error
      }
    },
    onSuccess: (result) => {
      if ('queued' in result) {
        toast.success(SAVED_ON_DEVICE)
      } else {
        applyTaskUpdate(queryClient, organizationId, result)
        const submitted = result.entries
          .filter((e) => e.submittedAt)
          .sort((x, y) => (x.submittedAt! < y.submittedAt! ? 1 : -1))
        toast.success(
          submitted[0]?.hasFailures
            ? `Reading ${submitted.length} added. It failed a check.`
            : `Reading ${submitted.length} added.`,
        )
      }
      form.reset(emptyFormValues(task.fields))
      setProblems(NO_PROBLEMS)
    },
    onError: (error) => {
      if (isConflict(error)) {
        onConflict(errorMessage(error))
        void queryClient.invalidateQueries({ queryKey: taskKey(organizationId, task.id) })
        return
      }
      const mapped = mapServerErrors(error)
      setProblems(mapped)
      requestAnimationFrame(() => focusFirstProblem(root.current, task.fields, mapped))
    },
  })

  function submit() {
    const v = form.getValues()
    const answers = toServerAnswers(task.fields, v.answers)
    const found = validateForSubmit(task.fields, answers, {
      required: task.requiresPhoto,
      count: v.photoIds.length,
    })
    setProblems(found ?? NO_PROBLEMS)
    if (found) {
      focusFirstProblem(root.current, task.fields, found)
      return
    }
    add.mutate({ answers, note: v.note.trim() || null, photoIds: v.photoIds })
  }

  return (
    <form
      ref={root}
      noValidate
      aria-label="Add reading"
      className="flex flex-col gap-6"
      onSubmit={(e) => {
        e.preventDefault()
        submit()
      }}
    >
      <h2 className="text-lg font-semibold">Add reading</h2>
      {problems.general.length > 0 && <FormAlert messages={problems.general} />}
      <FieldsEditor
        fields={task.fields}
        control={form.control}
        problems={problems}
        failures={failures}
        requiresPhoto={task.requiresPhoto}
        noteLabel="Note (optional)"
        onEdit={(key) =>
          setProblems((p) => {
            if (!(key in p.fields)) return p
            const fields = { ...p.fields }
            delete fields[key]
            return { ...p, fields }
          })
        }
        onClearPhotoProblem={() => setProblems((p) => ({ ...p, photos: undefined }))}
      />
      <Button
        type="submit"
        size="lg"
        className="h-14 w-full text-lg"
        disabled={add.isPending || pendingUploads > 0}
      >
        <Plus className="size-6" aria-hidden />
        {pendingUploads > 0 ? 'Uploading photos…' : add.isPending ? 'Adding…' : 'Add reading'}
      </Button>
      <p className="-mt-3 text-center text-sm text-muted-foreground">
        A reading cannot be changed once it is added.
      </p>
    </form>
  )
}

function FinishLog({
  task,
  count,
  onConflict,
}: {
  task: TaskDetail
  count: number
  onConflict: (m: string) => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [note, setNote] = useState('')
  const [error, setError] = useState<string>()
  const outbox = useOutboxApi()

  const finish = useMutation({
    networkMode: 'always', // offline completions are queued rather than paused
    mutationFn: async (): Promise<{ queued: true } | TaskDetail> => {
      const body = { note: note.trim() || null }
      const requestId = crypto.randomUUID()
      const completedAt = new Date().toISOString()
      const queue = async () => {
        await outbox.enqueueComplete({ taskId: task.id, body, requestId, completedAt })
        return { queued: true } as const
      }
      // Readings still queued must reach the server before the log is finished.
      const planned = await outbox.plan(task.id, body)
      if (planned.queue || !isOnline()) return queue()
      try {
        return await unwrap(
          client.POST('/api/v1/tasks/{id}/complete', {
            params: { path: { id: task.id } },
            body: { ...body, requestId },
          }),
        )
      } catch (e) {
        if (isOffline(e)) return queue()
        throw e
      }
    },
    onMutate: () => setError(undefined),
    onSuccess: (result) => {
      if ('queued' in result) {
        toast.success(SAVED_ON_DEVICE)
      } else {
        applyTaskUpdate(queryClient, organizationId, result)
        toast.success(
          result.hasFailures
            ? 'Record log finished. Some readings failed their checks.'
            : 'Record log finished.',
        )
      }
      void navigate({ to: '/today' })
    },
    onError: (e) => {
      if (isConflict(e)) {
        onConflict(errorMessage(e))
        void queryClient.invalidateQueries({ queryKey: taskKey(organizationId, task.id) })
        return
      }
      setError(mapServerErrors(e).note ?? errorMessage(e))
    },
  })

  return (
    <section aria-label="Finish" className="flex flex-col gap-3 border-t pt-6">
      <h2 className="text-lg font-semibold">Finish this log</h2>
      <Label htmlFor="finish-note" className="text-base">
        Closing note (optional)
      </Label>
      <Textarea id="finish-note" value={note} onChange={(e) => setNote(e.target.value)} />
      {error && <FormAlert messages={[error]} />}
      <Button
        size="lg"
        className="h-14 text-lg"
        disabled={count === 0 || finish.isPending}
        onClick={() => finish.mutate()}
      >
        <CircleCheck className="size-6" aria-hidden />
        {finish.isPending ? 'Finishing…' : 'Finish'}
      </Button>
      {count === 0 && (
        <p className="text-center text-sm text-muted-foreground">
          Add at least one reading before finishing.
        </p>
      )}
    </section>
  )
}
