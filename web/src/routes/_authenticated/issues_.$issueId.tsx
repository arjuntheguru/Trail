import { createFileRoute } from '@tanstack/react-router'

import { IssueDetailPage } from '@/features/issues/IssueDetailPage'

export const Route = createFileRoute('/_authenticated/issues_/$issueId')({
  component: IssueRoute,
})

function IssueRoute() {
  const { issueId } = Route.useParams()
  return <IssueDetailPage issueId={issueId} />
}
