import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'

import { NotificationsPage } from '@/features/notifications/NotificationsPage'

export const Route = createFileRoute('/_authenticated/notifications')({
  validateSearch: z.object({ unread: z.boolean().optional().catch(undefined) }),
  component: NotificationsPage,
})
