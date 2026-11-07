import { createFileRoute } from '@tanstack/react-router'

import { CompliancePage } from '@/features/reports/CompliancePage'

export const Route = createFileRoute('/_authenticated/reports/compliance')({
  component: CompliancePage,
})
