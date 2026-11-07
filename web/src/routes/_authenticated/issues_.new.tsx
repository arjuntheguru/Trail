import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'

import { RaiseIssuePage } from '@/features/issues/RaiseIssuePage'

// The site can be preselected from a link (e.g. from the list's site filter).
const searchSchema = z.object({
  siteId: z.string().optional().catch(undefined),
  // A task to link the issue to (the site is then the task's site).
  taskId: z.string().optional().catch(undefined),
})

export const Route = createFileRoute('/_authenticated/issues_/new')({
  validateSearch: searchSchema,
  component: RaiseIssueRoute,
})

function RaiseIssueRoute() {
  const { siteId, taskId } = Route.useSearch()
  return <RaiseIssuePage initialSiteId={siteId} taskId={taskId} />
}
