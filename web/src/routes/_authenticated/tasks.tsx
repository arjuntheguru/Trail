import { createFileRoute } from '@tanstack/react-router'

import { tasksSearchSchema } from '@/features/tasks/search'
import { TaskListPage } from '@/features/tasks/TaskListPage'

export const Route = createFileRoute('/_authenticated/tasks')({
  validateSearch: tasksSearchSchema,
  component: TaskListPage,
})
