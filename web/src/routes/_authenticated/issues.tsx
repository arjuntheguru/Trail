import { createFileRoute } from '@tanstack/react-router'

import { IssuesPage } from '@/features/issues/IssuesPage'
import { issuesSearchSchema } from '@/features/issues/search'

export const Route = createFileRoute('/_authenticated/issues')({
  validateSearch: issuesSearchSchema,
  component: IssuesPage,
})
