import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { ListSkeleton, LoadError, SectionHeader } from '@/features/settings/shared'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'

import {
  notificationPreferencesQuery,
  type NotificationKind,
  type NotificationPreferences,
} from './queries'

/** Every kind, in the server's enum order (the order it sends them back in). */
const KINDS: readonly { kind: NotificationKind; label: string; hint: string }[] = [
  { kind: 'IssueAssigned', label: 'Issue assigned to me', hint: 'Someone gives you an issue.' },
  {
    kind: 'IssueRaised',
    label: 'Issue raised',
    hint: 'A new issue appears at one of your sites.',
  },
  {
    kind: 'IssueStatusChanged',
    label: 'Issue status changed',
    hint: 'An issue you raised or own moves on.',
  },
  { kind: 'IssueCommented', label: 'Issue commented', hint: 'Someone comments on your issue.' },
  { kind: 'IssueOverdue', label: 'Issue overdue', hint: 'An issue passes its due date.' },
  { kind: 'IssueEscalated', label: 'Issue escalated', hint: 'An issue is escalated to you.' },
  {
    kind: 'TaskAwaitingApproval',
    label: 'Task awaiting approval',
    hint: 'A completed task needs your sign-off.',
  },
]

const HOURS = Array.from({ length: 24 }, (_, hour) => hour)
const hourLabel = (hour: number) => `${String(hour).padStart(2, '0')}:00`

export function NotificationPreferencesPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const preferences = useQuery(notificationPreferencesQuery(client, organizationId))
  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Notifications"
        description="Every notification appears in the app. Choose which ones are also emailed to you."
      />
      {preferences.isPending ? (
        <ListSkeleton rows={3} />
      ) : preferences.isError ? (
        <LoadError error={preferences.error} retry={() => void preferences.refetch()} />
      ) : (
        <PreferencesForm saved={preferences.data} />
      )}
    </section>
  )
}

function PreferencesForm({ saved }: { saved: NotificationPreferences }) {
  const { client } = useAuthService()
  const [emailKinds, setEmailKinds] = useState<NotificationKind[]>(saved.emailKinds)
  const [digestEnabled, setDigestEnabled] = useState(saved.digestEnabled)
  const [digestHour, setDigestHour] = useState(saved.digestHour)
  const [base, setBase] = useState(saved)
  const [error, setError] = useState<string[]>([])

  const mutation = useSettingsMutation({
    run: (body: NotificationPreferences) =>
      unwrap(client.PUT('/api/v1/notifications/preferences', { body })),
    areas: [['notifications', 'preferences']],
    onSuccess: (result) => {
      setBase(result)
      setEmailKinds(result.emailKinds)
      setDigestEnabled(result.digestEnabled)
      setDigestHour(result.digestHour)
      toast.success('Notification settings saved.')
    },
  })

  const dirty =
    digestEnabled !== base.digestEnabled ||
    digestHour !== base.digestHour ||
    emailKinds.length !== base.emailKinds.length ||
    emailKinds.some((k) => !base.emailKinds.includes(k))

  const toggle = (kind: NotificationKind, on: boolean) =>
    setEmailKinds((current) =>
      KINDS.map((k) => k.kind).filter((k) => (k === kind ? on : current.includes(k))),
    )

  return (
    <form
      noValidate
      className="flex flex-col gap-6"
      onSubmit={(e) => {
        e.preventDefault()
        setError([])
        mutation.mutate(
          { emailKinds, digestEnabled, digestHour },
          { onError: (failure) => setError([errorMessage(failure)]) },
        )
      }}
    >
      <fieldset className="flex flex-col gap-3">
        <legend className="mb-1 text-base font-medium">Email me when</legend>
        {KINDS.map(({ kind, label, hint }) => {
          const id = `email-${kind}`
          return (
            <div key={kind} className="flex items-start gap-3">
              <Checkbox
                id={id}
                className="mt-0.5"
                checked={emailKinds.includes(kind)}
                onCheckedChange={(v) => toggle(kind, v === true)}
              />
              <div className="flex flex-col">
                <Label htmlFor={id}>{label}</Label>
                <p className="text-sm text-muted-foreground">{hint}</p>
              </div>
            </div>
          )
        })}
      </fieldset>

      <fieldset className="flex flex-col gap-3">
        <legend className="mb-1 text-base font-medium">Daily digest</legend>
        <div className="flex items-start gap-3">
          <Checkbox
            id="digest-enabled"
            className="mt-0.5"
            checked={digestEnabled}
            onCheckedChange={(v) => setDigestEnabled(v === true)}
          />
          <div className="flex flex-col">
            <Label htmlFor="digest-enabled">Send me a daily digest</Label>
            <p className="text-sm text-muted-foreground">
              One email summarising what needs your attention.
            </p>
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="digest-hour">Send from</Label>
          <select
            id="digest-hour"
            value={digestHour}
            disabled={!digestEnabled}
            onChange={(e) => setDigestHour(Number(e.target.value))}
            className="h-12 w-40 rounded-lg border border-input bg-card px-3 text-base disabled:opacity-50"
          >
            {HOURS.map((h) => (
              <option key={h} value={h}>
                {hourLabel(h)}
              </option>
            ))}
          </select>
          <p className="text-sm text-muted-foreground">
            Local time, in the time zone of most of your sites.
          </p>
        </div>
      </fieldset>

      <FormAlert messages={error} />
      <div>
        <Button type="submit" disabled={mutation.isPending || !dirty}>
          {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
          Save changes
        </Button>
      </div>
    </form>
  )
}
