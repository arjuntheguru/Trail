import { createFileRoute } from '@tanstack/react-router'

import { TaskPage } from '@/features/tasks/TaskPage'

export const Route = createFileRoute('/_authenticated/tasks_/$taskId')({
  component: TaskRoute,
})

function TaskRoute() {
  const { taskId } = Route.useParams()
  return <TaskPage taskId={taskId} />
}
