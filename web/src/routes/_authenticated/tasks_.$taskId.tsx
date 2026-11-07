import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'

import { safeBackTarget } from '@/features/tasks/search'
import { TaskPage } from '@/features/tasks/TaskPage'

export const Route = createFileRoute('/_authenticated/tasks_/$taskId')({
  // Where the back link returns to: Today or the Tasks list with its filters.
  validateSearch: z.object({
    from: z.string().optional().transform(safeBackTarget).catch(undefined),
  }),
  component: TaskRoute,
})

function TaskRoute() {
  const { taskId } = Route.useParams()
  return <TaskPage taskId={taskId} />
}
