import { parseInstant } from '@/auth/session-store'
import { formatTime } from '@/lib/dates'

import type { TaskDetail } from './types'

export interface Workability {
  /** The person can fill the task in right now. */
  workable: boolean
  /** Why not, or a note that applies while it is workable (late completion). */
  message?: string
}

/**
 * Mirrors the server's TaskRules.CheckWorkable plus the permission: open tasks that have become available, and
 * missed tasks still inside the late window (the API reports that as `canCompleteLate`).
 */
export function workability(task: TaskDetail, now: number, canComplete: boolean): Workability {
  if (task.status === 'Completed' || task.status === 'Cancelled') return { workable: false }
  if (!canComplete) {
    return { workable: false, message: 'You do not have permission to complete tasks.' }
  }
  if (task.status === 'Missed') {
    return task.canCompleteLate
      ? { workable: true, message: 'This task was missed. Completing it now marks it as late.' }
      : { workable: false, message: 'This missed task can no longer be completed.' }
  }
  if (task.availableAt && parseInstant(task.availableAt) > now) {
    return {
      workable: false,
      message: `Not open yet. It opens at ${formatTime(task.availableAt, task.timeZone)}.`,
    }
  }
  return { workable: true }
}

/** yyyy-MM-dd plus whole days, without time zone shifts. */
export function addDays(day: string, days: number): string {
  const [y, m, d] = day.split('-').map(Number)
  const date = new Date(Date.UTC(y!, m! - 1, d! + days))
  return date.toISOString().slice(0, 10)
}
