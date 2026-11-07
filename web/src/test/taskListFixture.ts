import type { components } from '@/api/schema'

import { ISSUE_SITE_ID } from './fixtures'

type TaskListItem = components['schemas']['TaskListItemResponse']

/** A task as GET /tasks returns it: an open checklist due 2026-11-07 at Harbour Cafe. */
export function taskListItem(overrides: Partial<TaskListItem> = {}): TaskListItem {
  return {
    id: '99999999-0000-0000-0000-000000000001',
    siteId: ISSUE_SITE_ID,
    siteName: 'Harbour Cafe',
    title: 'Opening checks',
    kind: 'Checklist',
    status: 'Open',
    dueDate: '2026-11-07',
    dueAt: '2026-11-07T12:00:00Z',
    availableAt: null,
    isOverdue: false,
    isAwaitingApproval: false,
    requiresApproval: false,
    requiresPhoto: false,
    departmentId: null,
    source: 'Scheduled',
    entryCount: 0,
    completedAt: null,
    completedBy: null,
    completedLate: false,
    hasFailures: false,
    canCompleteLate: false,
    ...overrides,
  }
}
