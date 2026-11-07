import type { TaskDetail } from './types'

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
