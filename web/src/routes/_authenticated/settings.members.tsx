import { createFileRoute } from '@tanstack/react-router'

import { MembersPage } from '@/features/settings/MembersPage'

export const Route = createFileRoute('/_authenticated/settings/members')({
  component: MembersPage,
})
