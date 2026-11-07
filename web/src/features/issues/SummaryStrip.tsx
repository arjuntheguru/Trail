import { useQuery } from '@tanstack/react-query'

import { useAuthService, useSession } from '@/auth/context'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

import { issuesSummaryQuery } from './queries'
import type { IssuesSearch } from './search'

interface Chip {
  key: string
  label: string
  count: number
  pressed: boolean
  tone?: 'bad'
  apply: Partial<IssuesSearch>
}

/** Counts at the top of the list. Each one is also a shortcut that sets the matching filter. */
export function SummaryStrip({
  search,
  onChange,
}: {
  search: IssuesSearch
  onChange: (next: Partial<IssuesSearch>) => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const summary = useQuery(issuesSummaryQuery(client, organizationId, search.siteId))

  if (summary.isPending) {
    return (
      <div className="flex gap-2" role="status" aria-label="Loading summary">
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-16 w-24 rounded-xl" />
        ))}
      </div>
    )
  }
  // The counts are a convenience; the list itself reports its own errors.
  if (!summary.data) return null

  const { byStatus, overdue, assignedToMe } = summary.data
  const status = search.status ?? 'active'
  const alone = !search.mine && !search.overdue
  const chips: Chip[] = [
    {
      key: 'open',
      label: 'Open',
      count: byStatus.Open ?? 0,
      pressed: status === 'open' && alone,
      apply: { status: 'open', mine: undefined, overdue: undefined },
    },
    {
      key: 'inProgress',
      label: 'In progress',
      count: byStatus.InProgress ?? 0,
      pressed: status === 'inProgress' && alone,
      apply: { status: 'inProgress', mine: undefined, overdue: undefined },
    },
    {
      key: 'resolved',
      label: 'Resolved',
      count: byStatus.Resolved ?? 0,
      pressed: status === 'resolved' && alone,
      apply: { status: 'resolved', mine: undefined, overdue: undefined },
    },
    {
      key: 'overdue',
      label: 'Overdue',
      count: overdue,
      pressed: !!search.overdue,
      tone: 'bad',
      apply: { status: undefined, overdue: true, mine: undefined },
    },
    {
      key: 'mine',
      label: 'Mine',
      count: assignedToMe,
      pressed: !!search.mine,
      apply: { status: undefined, mine: true, overdue: undefined },
    },
  ]

  return (
    <ul className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1" aria-label="Issue summary">
      {chips.map((chip) => (
        <li key={chip.key} className="shrink-0">
          <button
            type="button"
            aria-pressed={chip.pressed}
            // A pressed chip toggles back to the default view.
            onClick={() =>
              onChange(
                chip.pressed
                  ? { status: undefined, mine: undefined, overdue: undefined }
                  : chip.apply,
              )
            }
            className={cn(
              'flex min-h-16 min-w-24 flex-col items-start justify-center rounded-xl border bg-card px-3 py-2 text-left',
              chip.pressed && 'border-primary bg-accent',
            )}
          >
            <span
              className={cn(
                'text-2xl leading-none font-semibold',
                chip.tone === 'bad' && chip.count > 0 && 'text-destructive',
              )}
            >
              {chip.count}
            </span>
            <span className="text-sm text-muted-foreground">{chip.label}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}
