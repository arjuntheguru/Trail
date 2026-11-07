import { describe, expect, it } from 'vitest'

import { groupKeyOf, groupTasks, type TaskItem } from './groups'

const NOW = Date.parse('2026-11-07T12:00:00Z')

function task(overrides: Partial<TaskItem>): TaskItem {
  return {
    id: 'id',
    siteId: 's',
    siteName: 'Site',
    title: 'T',
    kind: 'Checklist',
    status: 'Open',
    dueDate: '2026-11-07',
    dueAt: '2026-11-07T15:00:00Z',
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

describe('groupKeyOf', () => {
  it.each([
    [{ status: 'Open', isOverdue: true }, 'overdue'],
    [{ status: 'Open' }, 'due'],
    [{ status: 'Open', availableAt: '2026-11-07T14:00:00Z' }, 'upcoming'],
    [{ status: 'Open', availableAt: '2026-11-07T08:00:00Z' }, 'due'],
    [{ status: 'Completed' }, 'completed'],
    [{ status: 'Completed', isAwaitingApproval: true }, 'awaiting-approval'],
    [{ status: 'Missed' }, 'missed'],
    [{ status: 'Cancelled' }, 'cancelled'],
  ] as const)('%j -> %s', (overrides, expected) => {
    expect(groupKeyOf(task(overrides), NOW)).toBe(expected)
  })
})

describe('groupTasks', () => {
  it('orders groups by urgency, keeps task order within a group and drops empty groups', () => {
    const groups = groupTasks(
      [
        task({ id: '1', status: 'Completed' }),
        task({ id: '2', isOverdue: true }),
        task({ id: '3' }),
        task({ id: '4', isOverdue: true }),
      ],
      NOW,
    )
    expect(groups.map((g) => g.key)).toEqual(['overdue', 'due', 'completed'])
    expect(groups[0]?.items.map((t) => t.id)).toEqual(['2', '4'])
  })

  it('returns no groups for an empty list', () => {
    expect(groupTasks([], NOW)).toEqual([])
  })
})
