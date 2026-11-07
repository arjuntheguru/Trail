import type { TaskDetail, TaskListItem } from './types'

export function statusLabel(task: TaskDetail): string {
  switch (task.status) {
    case 'Completed':
      if (task.approvedAt) return 'Approved'
      return task.isAwaitingApproval ? 'Awaiting approval' : 'Completed'
    case 'Missed':
      return 'Missed'
    case 'Cancelled':
      return 'Cancelled'
    default:
      return task.isOverdue ? 'Overdue' : 'Open'
  }
}

/** The status text for a list row, which carries no approval timestamp: a finished task that needed approval and is no longer waiting has been approved. */
export function listStatusLabel(task: TaskListItem): string {
  switch (task.status) {
    case 'Completed':
      if (task.isAwaitingApproval) return 'Awaiting approval'
      return task.requiresApproval ? 'Approved' : 'Completed'
    case 'Missed':
      return 'Missed'
    case 'Cancelled':
      return 'Cancelled'
    default:
      return task.isOverdue ? 'Overdue' : 'Open'
  }
}
