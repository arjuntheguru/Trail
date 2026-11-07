import { createFileRoute } from '@tanstack/react-router'

import { requireAuthenticated } from '@/auth/guard'
import { AppShell } from '@/components/layout/AppShell'

export const Route = createFileRoute('/_authenticated')({
  beforeLoad: ({ context, location }) => requireAuthenticated(context.auth, location.href),
  component: AppShell,
})
