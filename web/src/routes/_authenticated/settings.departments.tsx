import { createFileRoute } from '@tanstack/react-router'

import { DepartmentsPage } from '@/features/settings/DepartmentsPage'

export const Route = createFileRoute('/_authenticated/settings/departments')({
  component: DepartmentsPage,
})
