import { createFileRoute } from '@tanstack/react-router'

import { reportsSearchSchema } from '@/features/reports/search'
import { ReportsLayout } from '@/features/reports/ReportsLayout'

export const Route = createFileRoute('/_authenticated/reports')({
  validateSearch: reportsSearchSchema,
  component: ReportsLayout,
})
