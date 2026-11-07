import { useQuery } from '@tanstack/react-query'

import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { cn } from '@/lib/utils'

import { issuesSummaryQuery } from './queries'

/**
 * Count of issues needing attention, shown on the Issues nav item (red when any are overdue). It shares the
 * unfiltered summary query with the list, refetches when the window regains focus and renders nothing at zero
 * or while the count is unknown.
 */
export function IssuesNavBadge({ className }: { className?: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canView = usePermission(Permissions.Issues.View)
  const summary = useQuery({ ...issuesSummaryQuery(client, organizationId), enabled: canView })
  if (!summary.data) return null

  const open = (summary.data.byStatus.Open ?? 0) + (summary.data.byStatus.InProgress ?? 0)
  if (open === 0) return null
  const overdue = summary.data.overdue
  return (
    <span
      data-testid="issues-nav-badge"
      aria-label={`${open} open ${open === 1 ? 'issue' : 'issues'}${overdue > 0 ? `, ${overdue} overdue` : ''}`}
      className={cn(
        'inline-grid min-w-5 place-items-center rounded-full px-1.5 text-xs leading-5 font-semibold text-white',
        overdue > 0 ? 'bg-destructive' : 'bg-primary',
        className,
      )}
    >
      {open > 99 ? '99+' : open}
    </span>
  )
}
