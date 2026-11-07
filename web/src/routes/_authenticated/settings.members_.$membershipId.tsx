import { createFileRoute } from '@tanstack/react-router'

import { MemberDetailPage } from '@/features/settings/MemberDetailPage'

export const Route = createFileRoute('/_authenticated/settings/members_/$membershipId')({
  component: MemberRoute,
})

function MemberRoute() {
  const { membershipId } = Route.useParams()
  return <MemberDetailPage key={membershipId} membershipId={membershipId} />
}
