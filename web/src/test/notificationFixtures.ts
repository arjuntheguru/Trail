import { http, HttpResponse } from 'msw'

import type { components } from '@/api/schema'

import { API, server } from './server'

export type NotificationFixture = components['schemas']['NotificationResponse']
export type PreferencesFixture = components['schemas']['NotificationPreferencesResponse']

const url = (path: string) => `${API}/api/v1/notifications${path}`

export function notification(overrides: Partial<NotificationFixture> = {}): NotificationFixture {
  return {
    id: 'n0000000-0000-0000-0000-000000000001',
    kind: 'IssueAssigned',
    title: 'Issue assigned to you',
    body: 'Walk-in fridge is warm',
    subjectType: 'Issue',
    subjectId: 'i0000000-0000-0000-0000-000000000001',
    siteId: null,
    readAt: null,
    createdAt: new Date().toISOString(),
    ...overrides,
  }
}

export function pagedNotifications(
  items: NotificationFixture[],
  extra: Partial<{ page: number; pageSize: number; totalCount: number; hasNextPage: boolean }> = {},
) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    ...extra,
  }
}

/** Serves the unread count from a mutable box and records how often it was asked. */
export function serveUnreadCount(initial: number) {
  const state = { count: initial, requests: 0 }
  server.use(
    http.get(url('/unread-count'), () => {
      state.requests += 1
      return HttpResponse.json({ count: state.count })
    }),
  )
  return state
}

export const DEFAULT_PREFERENCES: PreferencesFixture = {
  emailKinds: ['IssueAssigned', 'IssueOverdue', 'IssueEscalated', 'TaskAwaitingApproval'],
  digestEnabled: true,
  digestHour: 7,
}

export const notificationUrl = url
