import { useQuery } from '@tanstack/react-query'
import { CircleCheck, Lock, Play, RotateCcw, UserCheck } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { PhotoPicker } from '@/components/Photos'
import { useUploadTracker } from '@/components/useUploadTracker'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { fromLocalInput, toLocalInput } from '@/lib/dates'

import { PRIORITY_LABEL } from './labels'
import { assigneeCandidatesQuery } from './queries'
import { useIssueAction, splitError } from './useIssueAction'
import { PRIORITIES, type IssueDetail, type IssuePriority } from './types'

const selectClass = 'h-12 w-full rounded-lg border border-input bg-card px-3 text-base'

type Panel = 'resolve' | 'close' | 'reopen'

/** Workflow buttons for the issue, filtered by what the caller may do in its current status. */
export function IssueActions({ issue }: { issue: IssueDetail }) {
  const canRaise = usePermission(Permissions.Issues.Raise)
  const canManage = usePermission(Permissions.Issues.Manage)
  const membershipId = useSession().organization?.membershipId
  const [panel, setPanel] = useState<Panel | null>(null)

  const active = issue.status === 'Open' || issue.status === 'InProgress'
  // Start and resolve: the assignee or a manager (the server decides; this only hides what would be refused).
  const isAssignee = !!membershipId && issue.assignee?.membershipId === membershipId
  const canWork = canRaise && (isAssignee || canManage)
  const canStart = canWork && issue.status === 'Open'
  const canResolve = canWork && active
  const canClose = canManage && issue.status !== 'Closed'
  const canReopen = canManage && (issue.status === 'Resolved' || issue.status === 'Closed')

  if (!canStart && !canResolve && !canClose && !canReopen) return null

  const toggle = (next: Panel) => setPanel((current) => (current === next ? null : next))
  return (
    <section aria-label="Actions" className="flex flex-col gap-3 border-t pt-4">
      <div className="flex flex-wrap gap-2">
        {canStart && <StartButton issue={issue} />}
        {canResolve && (
          <Button size="lg" variant="outline" onClick={() => toggle('resolve')}>
            <CircleCheck aria-hidden />
            Resolve
          </Button>
        )}
        {canClose && (
          <Button size="lg" variant="outline" onClick={() => toggle('close')}>
            <Lock aria-hidden />
            {issue.status === 'Resolved' ? 'Verify and close' : 'Close issue'}
          </Button>
        )}
        {canReopen && (
          <Button size="lg" variant="outline" onClick={() => toggle('reopen')}>
            <RotateCcw aria-hidden />
            Reopen
          </Button>
        )}
      </div>
      {panel === 'resolve' && canResolve && (
        <ResolvePanel issue={issue} onDone={() => setPanel(null)} />
      )}
      {panel === 'close' && canClose && <ClosePanel issue={issue} onDone={() => setPanel(null)} />}
      {panel === 'reopen' && canReopen && (
        <ReopenPanel issue={issue} onDone={() => setPanel(null)} />
      )}
    </section>
  )
}

function StartButton({ issue }: { issue: IssueDetail }) {
  const { client } = useAuthService()
  const { mutation, error } = useIssueAction(
    issue.id,
    () => unwrap(client.POST('/api/v1/issues/{id}/start', { params: { path: { id: issue.id } } })),
    () => toast.success('Work started.'),
  )
  return (
    <div className="flex flex-col gap-2">
      <Button size="lg" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
        <Play aria-hidden />
        Start work
      </Button>
      <FormAlert messages={splitError(error).general} />
    </div>
  )
}

function PanelShell({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-3 rounded-xl border p-4">
      <h2 className="font-medium">{title}</h2>
      {children}
    </div>
  )
}

function TextBox({
  id,
  label,
  value,
  onChange,
  error,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id} className="text-sm">
        {label}
      </Label>
      <Textarea
        id={id}
        value={value}
        maxLength={2000}
        aria-invalid={error ? true : undefined}
        onChange={(e) => onChange(e.target.value)}
        className="min-h-24"
      />
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

function ResolvePanel({ issue, onDone }: { issue: IssueDetail; onDone: () => void }) {
  const { client } = useAuthService()
  const uploads = useUploadTracker()
  const [resolution, setResolution] = useState('')
  const [photoIds, setPhotoIds] = useState<string[]>([])
  const [missing, setMissing] = useState(false)
  const { mutation, error } = useIssueAction(
    issue.id,
    () =>
      unwrap(
        client.POST('/api/v1/issues/{id}/resolve', {
          params: { path: { id: issue.id } },
          body: { resolution: resolution.trim(), photoIds },
        }),
      ),
    () => {
      toast.success('Issue resolved.')
      onDone()
    },
  )
  const problems = splitError(error, 'resolution')
  const fieldError = missing ? 'Describe the corrective action taken.' : problems.field
  return (
    <PanelShell title="Resolve">
      <TextBox
        id="resolve-text"
        label="Corrective action taken"
        value={resolution}
        onChange={(v) => {
          setResolution(v)
          setMissing(false)
        }}
        error={fieldError}
      />
      <PhotoPicker
        label="Resolution photo"
        siteId={issue.siteId}
        value={photoIds}
        onChange={setPhotoIds}
        trackUpload={uploads.track}
      />
      <FormAlert messages={problems.general} />
      <Button
        size="lg"
        disabled={mutation.isPending || uploads.pending > 0}
        onClick={() => (resolution.trim() ? mutation.mutate() : setMissing(true))}
      >
        Mark as resolved
      </Button>
    </PanelShell>
  )
}

function ClosePanel({ issue, onDone }: { issue: IssueDetail; onDone: () => void }) {
  const { client } = useAuthService()
  const [note, setNote] = useState('')
  const { mutation, error } = useIssueAction(
    issue.id,
    () =>
      unwrap(
        client.POST('/api/v1/issues/{id}/close', {
          params: { path: { id: issue.id } },
          body: { note: note.trim() || null },
        }),
      ),
    () => {
      toast.success('Issue closed.')
      onDone()
    },
  )
  const problems = splitError(error, 'note')
  return (
    <PanelShell title={issue.status === 'Resolved' ? 'Verify and close' : 'Close issue'}>
      <TextBox
        id="close-note"
        label="Note (optional)"
        value={note}
        onChange={setNote}
        error={problems.field}
      />
      <FormAlert messages={problems.general} />
      <Button size="lg" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
        Close issue
      </Button>
    </PanelShell>
  )
}

function ReopenPanel({ issue, onDone }: { issue: IssueDetail; onDone: () => void }) {
  const { client } = useAuthService()
  const [reason, setReason] = useState('')
  const [missing, setMissing] = useState(false)
  const { mutation, error } = useIssueAction(
    issue.id,
    () =>
      unwrap(
        client.POST('/api/v1/issues/{id}/reopen', {
          params: { path: { id: issue.id } },
          body: { reason: reason.trim() },
        }),
      ),
    () => {
      toast.success('Issue reopened.')
      onDone()
    },
  )
  const problems = splitError(error, 'reason')
  return (
    <PanelShell title="Reopen">
      <TextBox
        id="reopen-reason"
        label="Why is it being reopened?"
        value={reason}
        onChange={(v) => {
          setReason(v)
          setMissing(false)
        }}
        error={missing ? 'Give a reason so the team knows what is still wrong.' : problems.field}
      />
      <FormAlert messages={problems.general} />
      <Button
        size="lg"
        disabled={mutation.isPending}
        onClick={() => (reason.trim() ? mutation.mutate() : setMissing(true))}
      >
        Reopen issue
      </Button>
    </PanelShell>
  )
}

/** Assignee, priority and due time. Only managers change these, and only while the issue is active. */
export function ManageIssue({ issue }: { issue: IssueDetail }) {
  const canManage = usePermission(Permissions.Issues.Manage)
  if (!canManage || (issue.status !== 'Open' && issue.status !== 'InProgress')) return null
  return (
    <section aria-label="Manage" className="flex flex-col gap-4 rounded-xl border p-4">
      <h2 className="font-medium">Manage</h2>
      <AssigneeEditor issue={issue} />
      <PriorityEditor issue={issue} />
      <DueEditor key={issue.dueAt ?? 'none'} issue={issue} />
    </section>
  )
}

function AssigneeEditor({ issue }: { issue: IssueDetail }) {
  const { client } = useAuthService()
  const session = useSession()
  const organizationId = session.organization?.id
  const myMembershipId = session.organization?.membershipId
  const canListMembers = usePermission(Permissions.Members.View)
  const members = useQuery({
    ...assigneeCandidatesQuery(client, organizationId),
    enabled: canListMembers,
  })
  const { mutation, error } = useIssueAction(
    issue.id,
    (assigneeId: string | null) =>
      unwrap(
        client.PUT('/api/v1/issues/{id}/assignee', {
          params: { path: { id: issue.id } },
          body: { assigneeId },
        }),
      ),
    (updated) =>
      toast.success(
        updated.assignee ? `Assigned to ${updated.assignee.name ?? 'member'}.` : 'Unassigned.',
      ),
  )
  const current = issue.assignee
  const messages = splitError(error).general
  // Without members.view (or if the list fails) the picker cannot be offered: show who has it and let the
  // caller take it or drop it.
  const candidates = members.data?.items

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="assignee" className="text-sm">
        Assignee
      </Label>
      {candidates ? (
        <select
          id="assignee"
          className={selectClass}
          value={current?.membershipId ?? ''}
          disabled={mutation.isPending}
          onChange={(e) => mutation.mutate(e.target.value || null)}
        >
          <option value="">Unassigned</option>
          {current && !candidates.some((m) => m.id === current.membershipId) && (
            <option value={current.membershipId}>{current.name ?? 'Former member'}</option>
          )}
          {candidates.map((m) => (
            <option key={m.id} value={m.id}>
              {m.firstName} {m.lastName}
            </option>
          ))}
        </select>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-base">{current ? (current.name ?? 'Assigned') : 'Unassigned'}</span>
          {myMembershipId && current?.membershipId !== myMembershipId && (
            <Button
              variant="outline"
              size="lg"
              disabled={mutation.isPending}
              onClick={() => mutation.mutate(myMembershipId)}
            >
              <UserCheck aria-hidden />
              Assign to me
            </Button>
          )}
          {current && (
            <Button
              variant="ghost"
              size="lg"
              disabled={mutation.isPending}
              onClick={() => mutation.mutate(null)}
            >
              Unassign
            </Button>
          )}
        </div>
      )}
      <FormAlert messages={messages} />
    </div>
  )
}

function PriorityEditor({ issue }: { issue: IssueDetail }) {
  const { client } = useAuthService()
  const { mutation, error } = useIssueAction(
    issue.id,
    (priority: IssuePriority) =>
      unwrap(
        client.PUT('/api/v1/issues/{id}/priority', {
          params: { path: { id: issue.id } },
          body: { priority },
        }),
      ),
    () => toast.success('Priority changed.'),
  )
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="priority" className="text-sm">
        Priority
      </Label>
      <select
        id="priority"
        className={selectClass}
        value={issue.priority}
        disabled={mutation.isPending}
        onChange={(e) => mutation.mutate(e.target.value as IssuePriority)}
      >
        {PRIORITIES.map((p) => (
          <option key={p} value={p}>
            {PRIORITY_LABEL[p]}
          </option>
        ))}
      </select>
      <FormAlert messages={splitError(error).general} />
    </div>
  )
}

function DueEditor({ issue }: { issue: IssueDetail }) {
  const { client } = useAuthService()
  const [value, setValue] = useState(issue.dueAt ? toLocalInput(issue.dueAt) : '')
  const [past, setPast] = useState(false)
  const { mutation, error } = useIssueAction(
    issue.id,
    (dueAt: string | null) =>
      unwrap(
        client.PUT('/api/v1/issues/{id}/due', {
          params: { path: { id: issue.id } },
          body: { dueAt },
        }),
      ),
    () => toast.success('Due time changed.'),
  )
  const problems = splitError(error, 'dueAt')
  const message = problems.field ?? (past ? 'Pick a time in the future.' : undefined)
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="due" className="text-sm">
        Due
      </Label>
      <div className="flex flex-wrap items-center gap-2">
        <Input
          id="due"
          type="datetime-local"
          className="h-12 w-auto"
          value={value}
          aria-invalid={message ? true : undefined}
          onChange={(e) => {
            setValue(e.target.value)
            setPast(false)
          }}
        />
        <Button
          variant="outline"
          size="lg"
          disabled={!value || mutation.isPending}
          onClick={() =>
            new Date(value).getTime() <= Date.now()
              ? setPast(true)
              : mutation.mutate(fromLocalInput(value))
          }
        >
          Save due time
        </Button>
        {issue.dueAt && (
          <Button
            variant="ghost"
            size="lg"
            disabled={mutation.isPending}
            onClick={() => mutation.mutate(null)}
          >
            Clear
          </Button>
        )}
      </div>
      {message && (
        <p role="alert" className="text-sm text-destructive">
          {message}
        </p>
      )}
      <FormAlert messages={problems.general} />
    </div>
  )
}
