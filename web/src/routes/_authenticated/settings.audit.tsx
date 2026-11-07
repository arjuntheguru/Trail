import { createFileRoute } from '@tanstack/react-router'

import { AuditPage } from '@/features/audit/AuditPage'
import { auditSearchSchema } from '@/features/audit/search'

export const Route = createFileRoute('/_authenticated/settings/audit')({
  validateSearch: auditSearchSchema,
  component: AuditPage,
})
