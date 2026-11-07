import type { components } from '@/api/schema'
import { parseInstant } from '@/auth/session-store'

export type TaskItem = components['schemas']['TaskListItemResponse']

export type TaskGroupKey =
  'overdue' | 'due' | 'upcoming' | 'awaiting-approval' | 'missed' | 'completed' | 'cancelled'

export interface TaskGroup {
  key: TaskGroupKey
  label: string
  items: TaskItem[]
}

const ORDER: { key: TaskGroupKey; label: string }[] = [
  { key: 'overdue', label: 'Overdue' },
  { key: 'due', label: 'Due now' },
  { key: 'upcoming', label: 'Later today' },
  { key: 'awaiting-approval', label: 'Awaiting approval' },
  { key: 'missed', label: 'Missed' },
  { key: 'completed', label: 'Completed' },
  { key: 'cancelled', label: 'Cancelled' },
]

export function groupKeyOf(task: TaskItem, now: number): TaskGroupKey {
  switch (task.status) {
    case 'Completed':
      return task.isAwaitingApproval ? 'awaiting-approval' : 'completed'
    case 'Missed':
      return 'missed'
    case 'Cancelled':
      return 'cancelled'
    default:
      if (task.isOverdue) return 'overdue'
      return task.availableAt && parseInstant(task.availableAt) > now ? 'upcoming' : 'due'
  }
}

/** Groups tasks by what the person needs to do about them, most urgent first. Empty groups are dropped. */
export function groupTasks(items: readonly TaskItem[], now: number): TaskGroup[] {
  const buckets = new Map<TaskGroupKey, TaskItem[]>()
  for (const item of items) {
    const key = groupKeyOf(item, now)
    buckets.set(key, [...(buckets.get(key) ?? []), item])
  }
  return ORDER.flatMap(({ key, label }) => {
    const group = buckets.get(key)
    return group ? [{ key, label, items: group }] : []
  })
}
