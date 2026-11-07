import type { components } from '@/api/schema'

type AuthSession = components['schemas']['AuthSessionResponse']
type Organization = components['schemas']['OrganizationSummary']

export const ORG_A: Organization = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Acme Kitchens',
  slug: 'acme',
  membershipId: 'aaaaaaaa-0000-0000-0000-000000000001',
  role: 'Owner',
}

export const ORG_B: Organization = {
  id: '22222222-2222-2222-2222-222222222222',
  name: 'Beta Bistro',
  slug: 'beta',
  membershipId: 'bbbbbbbb-0000-0000-0000-000000000001',
  role: 'Admin',
}

/** A session response as the API sends it for cookie transport (empty refresh token in the body). */
export function sessionResponse(
  overrides: Partial<AuthSession> & { expiresInMs?: number } = {},
): AuthSession {
  const { expiresInMs = 15 * 60_000, ...rest } = overrides
  return {
    accessToken: 'access-1',
    accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    refreshToken: '',
    refreshTokenExpiresAt: new Date(Date.now() + 30 * 24 * 3600_000).toISOString(),
    user: {
      id: 'cccccccc-0000-0000-0000-000000000001',
      email: 'sam@example.com',
      firstName: 'Sam',
      lastName: 'Chef',
      emailConfirmed: true,
      isPlatformAdmin: false,
    },
    organization: ORG_A,
    organizations: [ORG_A],
    permissions: ['tasks.view', 'sites.view', 'issues.view'],
    ...rest,
  }
}

export function problem(status: number, code: string, detail: string, extra: object = {}) {
  return { type: 'about:blank', title: code, status, detail, code, ...extra }
}

type TaskDetail = components['schemas']['TaskDetailResponse']
type TemplateField = components['schemas']['TemplateField']
type TaskEntry = components['schemas']['TaskEntryResponse']

export const SESSION_USER_ID = 'cccccccc-0000-0000-0000-000000000001'

export function field(
  overrides: Partial<TemplateField> & Pick<TemplateField, 'key' | 'type'>,
): TemplateField {
  return {
    label: overrides.key,
    required: true,
    options: [],
    photoRequiredOnFailure: false,
    commentRequiredOnFailure: true,
    raiseIssueOnFailure: true,
    isAnswerable: overrides.type !== 'Instruction',
    ...overrides,
  }
}

export function entry(overrides: Partial<TaskEntry> = {}): TaskEntry {
  return {
    id: crypto.randomUUID(),
    answers: {},
    photoIds: [],
    note: null,
    failures: [],
    hasFailures: false,
    submittedAt: null,
    submittedById: null,
    submittedBy: null,
    ...overrides,
  }
}

/** A task as GET /tasks/{id} returns it: an open checklist due today with no fields unless given. */
export function taskDetail(overrides: Partial<TaskDetail> = {}): TaskDetail {
  return {
    id: '99999999-0000-0000-0000-000000000001',
    siteId: '55555555-0000-0000-0000-000000000001',
    siteName: 'Harbour Cafe',
    timeZone: 'Europe/London',
    title: 'Opening checks',
    description: null,
    kind: 'Checklist',
    status: 'Open',
    source: 'Scheduled',
    fields: [],
    requiresApproval: false,
    requiresPhoto: false,
    departmentId: null,
    dueDate: '2026-11-07',
    dueAt: '2026-11-07T12:00:00Z',
    availableAt: null,
    isOverdue: false,
    isAwaitingApproval: false,
    canCompleteLate: false,
    snoozeCount: 0,
    completedAt: null,
    completedById: null,
    completedBy: null,
    completedLate: false,
    hasFailures: false,
    approvedAt: null,
    approvedById: null,
    approvedBy: null,
    approvalNote: null,
    entries: [],
    ...overrides,
  }
}

type IssueDetail = components['schemas']['IssueDetailResponse']
type IssueListItem = components['schemas']['IssueListItemResponse']
type IssueEvent = components['schemas']['IssueEventResponse']

export const SESSION_MEMBERSHIP_ID = ORG_A.membershipId
export const ISSUE_SITE_ID = '55555555-0000-0000-0000-000000000001'

export function issueListItem(overrides: Partial<IssueListItem> = {}): IssueListItem {
  return {
    id: crypto.randomUUID(),
    number: 12,
    title: 'Fridge door will not seal',
    siteId: ISSUE_SITE_ID,
    siteName: 'Harbour Cafe',
    priority: 'Medium',
    status: 'Open',
    source: 'Manual',
    assignee: null,
    dueAt: null,
    isOverdue: false,
    createdAt: '2026-11-06T09:00:00Z',
    photoCount: 0,
    timeZone: 'Europe/London',
    isEscalated: false,
    ...overrides,
  }
}

export function issueEvent(overrides: Partial<IssueEvent> = {}): IssueEvent {
  return {
    id: crypto.randomUUID(),
    kind: 'Comment',
    from: null,
    to: null,
    fromName: null,
    toName: null,
    note: null,
    photoIds: [],
    occurredAt: '2026-11-06T10:00:00Z',
    actorId: null,
    actorName: 'Pat Manager',
    ...overrides,
  }
}

/** An open, unassigned manual issue with a single "raised" event. */
export function issueDetail(overrides: Partial<IssueDetail> = {}): IssueDetail {
  return {
    id: '88888888-0000-0000-0000-000000000001',
    number: 12,
    title: 'Fridge door will not seal',
    description: null,
    siteId: ISSUE_SITE_ID,
    siteName: 'Harbour Cafe',
    departmentId: null,
    departmentName: null,
    priority: 'Medium',
    status: 'Open',
    source: 'Manual',
    assignee: null,
    dueAt: null,
    isOverdue: false,
    siteTaskId: null,
    taskTitle: null,
    taskDueDate: null,
    fieldKey: null,
    resolution: null,
    resolvedAt: null,
    resolvedBy: null,
    closedAt: null,
    closedBy: null,
    photoIds: [],
    createdAt: '2026-11-06T09:00:00Z',
    timeZone: 'Europe/London',
    overdueNotifiedAt: null,
    escalatedAt: null,
    timeline: [
      issueEvent({
        kind: 'Raised',
        to: 'Medium',
        occurredAt: '2026-11-06T09:00:00Z',
        actorName: 'Sam Chef',
      }),
    ],
    ...overrides,
  }
}
