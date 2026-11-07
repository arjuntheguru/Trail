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
