import { createFileRoute } from '@tanstack/react-router'

import { NotificationPreferencesPage } from '@/features/notifications/NotificationPreferencesPage'

export const Route = createFileRoute('/_authenticated/settings/notifications')({
  component: NotificationPreferencesPage,
})
