import { useSession } from '@/auth/context'

import type { Member, Role } from './queries'

/** Mirrors `MemberRules.CallerHolds`: a caller may only grant what they hold themselves. */
export function holdsAll(held: readonly string[], required: readonly string[]) {
  return required.every((p) => held.includes(p))
}

/** True when the member is the signed-in person (the server refuses every change to yourself). */
export function useIsSelf() {
  const session = useSession()
  return (member: Pick<Member, 'id' | 'userId'>) =>
    member.id === session.organization?.membershipId || member.userId === session.user.id
}

/**
 * Why the caller cannot manage this member, or undefined when they can. The server stays authoritative
 * (it also protects the last owner); this only avoids offering actions it is certain to refuse.
 */
export function cannotManageReason(
  member: Pick<Member, 'roleId'>,
  isSelf: boolean,
  roles: readonly Role[] | undefined,
  held: readonly string[],
): string | undefined {
  if (isSelf) return 'You cannot change your own membership or access.'
  const role = roles?.find((r) => r.id === member.roleId)
  if (role && !holdsAll(held, role.permissions)) {
    return 'This member has permissions you do not hold yourself, so only someone with at least those permissions can change them.'
  }
  return undefined
}
