import { createFileRoute } from '@tanstack/react-router'

import { ScheduleFormPage } from '@/features/schedules/ScheduleFormPage'

export const Route = createFileRoute('/_authenticated/settings/schedules_/$scheduleId')({
  component: ScheduleRoute,
})

function ScheduleRoute() {
  const { scheduleId } = Route.useParams()
  return <ScheduleFormPage key={scheduleId} scheduleId={scheduleId} />
}
