import { formatDateTime } from '@/lib/dates'

import { PRIORITY_LABEL, STATUS_LABEL } from './labels'
import type { IssueEvent, IssuePriority, IssueStatus } from './types'

const status = (value: string | null) => STATUS_LABEL[value as IssueStatus] ?? value ?? ''
const priority = (value: string | null) => PRIORITY_LABEL[value as IssuePriority] ?? value ?? ''

/** What happened, without the actor ("started work", "changed priority from Low to High"). */
export function describeEvent(event: IssueEvent, timeZone?: string): string {
  switch (event.kind) {
    case 'Raised':
      return event.to
        ? `raised this issue (${priority(event.to).toLowerCase()} priority)`
        : 'raised this issue'
    case 'Comment':
      return 'commented'
    case 'StatusChanged':
      switch (event.to) {
        case 'InProgress':
          return 'started work'
        case 'Resolved':
          return 'resolved this issue'
        case 'Closed':
          return 'closed this issue'
        case 'Open':
          return 'reopened this issue'
        default:
          return `changed the status from ${status(event.from)} to ${status(event.to)}`
      }
    case 'Assigned':
      if (!event.to) return 'unassigned this issue'
      return event.toName ? `assigned this to ${event.toName}` : 'assigned this issue'
    case 'PriorityChanged':
      return `changed the priority from ${priority(event.from)} to ${priority(event.to)}`
    case 'DueChanged':
      return event.to
        ? `set the due time to ${formatDateTime(event.to, timeZone)}`
        : 'removed the due time'
  }
}
