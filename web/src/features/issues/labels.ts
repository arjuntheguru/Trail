import type { IssuePriority, IssueStatus } from './types'

export const STATUS_LABEL: Record<IssueStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  Resolved: 'Resolved',
  Closed: 'Closed',
}

export const PRIORITY_LABEL: Record<IssuePriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
}

export const STATUS_CLASS: Record<IssueStatus, string> = {
  Open: 'border-primary/40 text-primary',
  InProgress: 'border-warning/50 text-warning',
  Resolved: 'border-success/50 text-success',
  Closed: 'text-muted-foreground',
}

export const PRIORITY_CLASS: Record<IssuePriority, string> = {
  Low: 'bg-muted text-muted-foreground',
  Medium: 'bg-secondary text-secondary-foreground',
  High: 'bg-warning/20 text-warning',
  Critical: 'bg-destructive text-white',
}
