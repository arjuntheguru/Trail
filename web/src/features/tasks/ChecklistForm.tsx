import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CircleCheck } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { useOutboxApi, usePendingForTask } from '@/offline/context'
import { SAVED_ON_DEVICE } from '@/offline/messages'
import { isOnline } from '@/offline/online'
import type { CompleteBody } from '@/offline/outbox-types'

import { toDraftAnswers, toFormAnswers, toServerAnswers } from './answers'
import { evaluateAnswers } from './evaluate'
import { isConflict, isOffline, mapServerErrors } from './errors'
import { FieldsEditor } from './FieldsEditor'
import { TaskFormProvider, useTaskForm } from './form-context'
import { focusFirstProblem, NO_PROBLEMS, validateForSubmit, type FormProblems } from './problems'
import { applyTaskUpdate, taskKey } from './queries'
import type { TaskDetail, TaskFormValues } from './types'
import { useAutosave, type SaveStatus } from './useAutosave'

const STATUS_TEXT: Record<SaveStatus, string> = {
  idle: '',
  unsaved: 'Unsaved changes',
  saving: 'Saving…',
  saved: 'Saved',
  queued: 'Saved on this device',
  offline: 'Offline – not saved',
  error: 'Could not save',
}

export function SaveIndicator({ status }: { status: SaveStatus }) {
  return (
    <p
      role="status"
      aria-live="polite"
      className={
        status === 'offline' || status === 'error'
          ? 'text-sm font-medium text-destructive'
          : 'text-sm text-muted-foreground'
      }
    >
      {STATUS_TEXT[status]}
    </p>
  )
}

interface ChecklistFormProps {
  task: TaskDetail
  /** Shown above the task by the screen; used for "someone else completed it" style conflicts. */
  onConflict: (message: string) => void
}

/** Checklists and forms: one draft that autosaves, completed in a single step. */
export function ChecklistForm(props: ChecklistFormProps) {
  return (
    <TaskFormProvider siteId={props.task.siteId} taskId={props.task.id} readOnly={false}>
      <ChecklistFormInner {...props} />
    </TaskFormProvider>
  )
}

function ChecklistFormInner({ task, onConflict }: ChecklistFormProps) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { pendingUploads } = useTaskForm()
  const outbox = useOutboxApi()
  const queuedProgress = usePendingForTask(task.id).find((i) => i.type === 'progress')
  const root = useRef<HTMLFormElement>(null)
  const [problems, setProblems] = useState<FormProblems>(NO_PROBLEMS)
  const [stopped, setStopped] = useState(false)

  // The form is created once from the saved draft; later responses (autosaves) must not reset what is typed.
  const [defaults] = useState<TaskFormValues>(() => {
    // A draft kept on this device is newer than the one the server (or the cache) holds.
    const draft =
      queuedProgress?.type === 'progress'
        ? queuedProgress.body
        : task.entries.find((e) => !e.submittedAt)
    return {
      answers: toFormAnswers(task.fields, draft?.answers),
      note: draft?.note ?? '',
      photoIds: [...(draft?.photoIds ?? [])],
    }
  })
  const form = useForm<TaskFormValues>({ defaultValues: defaults })
  const values = useWatch({ control: form.control }) as TaskFormValues

  const draftBody = (v: TaskFormValues) => ({
    answers: toDraftAnswers(task.fields, v.answers),
    note: v.note ?? '',
    photoIds: v.photoIds ?? [],
  })
  const snapshot = JSON.stringify(draftBody(values))
  const [initialSnapshot] = useState(() => JSON.stringify(draftBody(defaults)))

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

  function handleServerError(error: unknown) {
    if (isConflict(error)) {
      setStopped(true)
      onConflict(errorMessage(error))
      void queryClient.invalidateQueries({ queryKey: taskKey(organizationId, task.id) })
      return
    }
    const mapped = mapServerErrors(error)
    setProblems(mapped)
    requestAnimationFrame(() => focusFirstProblem(root.current, task.fields, mapped))
  }

  const autosave = useAutosave({
    enabled: !stopped,
    snapshot,
    initialSnapshot,
    save: async (body) => {
      try {
        // Earlier drafts still queued must go first, so this one joins the queue behind them.
        if (outbox.hasQueued(task.id)) {
          await outbox.enqueueProgress(task.id, JSON.parse(body) as ReturnType<typeof draftBody>)
          return
        }
        const updated = await unwrap(
          client.PUT('/api/v1/tasks/{id}/progress', {
            params: { path: { id: task.id } },
            body: JSON.parse(body) as ReturnType<typeof draftBody>,
          }),
        )
        queryClient.setQueryData(taskKey(organizationId, task.id), updated)
      } catch (error) {
        if (isConflict(error)) handleServerError(error)
        throw error
      }
    },
    saveOffline: (body) =>
      outbox.enqueueProgress(task.id, JSON.parse(body) as ReturnType<typeof draftBody>),
  })

  const complete = useMutation({
    // Offline is handled here (by queueing), so the mutation must not pause until the network returns.
    networkMode: 'always',
    mutationFn: async (input: CompleteBody): Promise<{ queued: true } | TaskDetail> => {
      // One id per press of the button, kept when the request is retried from the queue.
      const requestId = crypto.randomUUID()
      const completedAt = new Date().toISOString()
      const planned = await outbox.plan(task.id, input)
      const queue = async () => {
        await outbox.enqueueComplete({
          taskId: task.id,
          body: planned.body,
          requestId,
          completedAt,
        })
        return { queued: true } as const
      }
      if (planned.queue || !isOnline()) return queue()
      try {
        const updated = await unwrap(
          client.POST('/api/v1/tasks/{id}/complete', {
            params: { path: { id: task.id } },
            body: { ...planned.body, requestId },
          }),
        )
        void outbox.dropProgress(task.id).catch(() => {})
        return updated
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
        toast.success(result.hasFailures ? 'Task completed with failed checks.' : 'Task completed.')
      }
      void navigate({ to: '/today' })
    },
    onError: (error) => {
      autosave.resume()
      handleServerError(error)
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
    autosave.cancel()
    complete.mutate({ answers, note: v.note.trim() || null, photoIds: v.photoIds })
  }

  const clearField = (key: string) =>
    setProblems((p) => {
      if (!(key in p.fields)) return p
      const fields = { ...p.fields }
      delete fields[key]
      return { ...p, fields }
    })

  return (
    <form
      ref={root}
      noValidate
      className="flex flex-col gap-6"
      onSubmit={(e) => {
        e.preventDefault()
        submit()
      }}
      onBlur={() => void autosave.flush()}
    >
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Your answers</h2>
        <SaveIndicator status={autosave.status} />
      </div>
      {problems.general.length > 0 && <FormAlert messages={problems.general} />}

      <FieldsEditor
        fields={task.fields}
        control={form.control}
        problems={problems}
        failures={failures}
        requiresPhoto={task.requiresPhoto}
        noteLabel="Note (optional)"
        onEdit={clearField}
        onClearPhotoProblem={() => setProblems((p) => ({ ...p, photos: undefined }))}
      />

      <div className="sticky bottom-[calc(4rem+env(safe-area-inset-bottom))] -mx-4 border-t bg-background/95 p-4 backdrop-blur md:bottom-0">
        <Button
          type="submit"
          size="lg"
          className="h-14 w-full text-lg"
          disabled={complete.isPending || pendingUploads > 0 || stopped}
        >
          <CircleCheck className="size-6" aria-hidden />
          {pendingUploads > 0
            ? 'Uploading photos…'
            : complete.isPending
              ? 'Completing…'
              : 'Complete task'}
        </Button>
      </div>
    </form>
  )
}
