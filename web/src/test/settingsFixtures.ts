import { http, HttpResponse } from 'msw'

import type { components } from '@/api/schema'

import { sessionResponse } from './fixtures'
import { API, server } from './server'

type Site = components['schemas']['SiteResponse']
type Member = components['schemas']['MemberResponse']
type Role = components['schemas']['RoleResponse']
type Region = components['schemas']['RegionResponse']
type Department = components['schemas']['DepartmentResponse']

export const REFRESH = `${API}/api/v1/auth/refresh`
export const url = (path: string) => `${API}/api/v1${path}`

/** The signed-in user's ids, as `sessionResponse` issues them. */
export const SELF_USER_ID = 'cccccccc-0000-0000-0000-000000000001'
export const SELF_MEMBERSHIP_ID = 'aaaaaaaa-0000-0000-0000-000000000001'

export const ALL_PERMISSIONS = [
  'organization.manage',
  'members.view',
  'members.manage',
  'roles.manage',
  'sites.view',
  'sites.manage',
  'tags.manage',
  'templates.view',
  'templates.manage',
  'schedules.manage',
  'tasks.view',
  'tasks.complete',
  'tasks.create_ad_hoc',
  'tasks.approve',
  'issues.view',
  'issues.raise',
  'issues.manage',
  'reports.view',
  'reports.export',
  'audit.view',
  'integrations.manage',
]

export const CATALOG = ALL_PERMISSIONS.map((key) => ({ key, group: key.split('.')[0] }))

/** Signs the test user in with the given permissions. */
export function signIn(permissions: string[] = ALL_PERMISSIONS) {
  server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))))
}

export function paged<T>(items: T[], extra: object = {}) {
  return {
    items,
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    ...extra,
  }
}

export function site(overrides: Partial<Site> = {}): Site {
  return {
    id: crypto.randomUUID(),
    name: 'Harbour Cafe',
    code: null,
    regionId: null,
    regionName: null,
    timeZone: 'Europe/London',
    address: null,
    phone: null,
    email: null,
    isActive: true,
    openingHours: [],
    ...overrides,
  }
}

export function role(overrides: Partial<Role> = {}): Role {
  return {
    id: crypto.randomUUID(),
    name: 'Supervisor',
    description: null,
    isSystem: false,
    permissions: ['tasks.view'],
    memberCount: 0,
    ...overrides,
  }
}

export function member(overrides: Partial<Member> = {}): Member {
  return {
    id: crypto.randomUUID(),
    userId: crypto.randomUUID(),
    email: 'pat@example.com',
    firstName: 'Pat',
    lastName: 'Manager',
    roleId: 'role-team',
    roleName: 'Team Member',
    status: 'Active',
    allSites: true,
    joinedAt: '2026-01-01T00:00:00Z',
    lastLoginAt: null,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

export const region = (overrides: Partial<Region> = {}): Region => ({
  id: crypto.randomUUID(),
  name: 'North',
  description: null,
  ...overrides,
})

export const department = (overrides: Partial<Department> = {}): Department => ({
  id: crypto.randomUUID(),
  name: 'Kitchen',
  ...overrides,
})

/** Registers a handler that records the JSON body of every request and answers with `respond`. */
export function recordBodies<T = unknown>(
  method: 'post' | 'put' | 'delete',
  path: string,
  respond: (body: T) => Response = () => new HttpResponse(null, { status: 204 }),
) {
  const bodies: T[] = []
  server.use(
    http[method](url(path), async ({ request }) => {
      const body = (
        request.headers.get('content-type')?.includes('json') ? await request.json() : undefined
      ) as T
      bodies.push(body)
      return respond(body)
    }),
  )
  return bodies
}
