import { createFileRoute } from '@tanstack/react-router'

import { ScheduleFormPage } from '@/features/schedules/ScheduleFormPage'

export const Route = createFileRoute('/_authenticated/settings/schedules_/new')({
  component: () => <ScheduleFormPage />,
})
