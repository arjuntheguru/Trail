import { CalendarCheck, ClipboardList, FileBarChart, Settings, TriangleAlert } from 'lucide-react'

import { Permissions } from '@/auth/permissions'

export interface NavItem {
  to: '/today' | '/tasks' | '/issues' | '/reports' | '/settings'
  label: string
  icon: typeof CalendarCheck
  /** Hidden from users who lack it. */
  permission?: string
}

export const NAV_ITEMS: readonly NavItem[] = [
  { to: '/today', label: 'Today', icon: CalendarCheck, permission: Permissions.Tasks.View },
  { to: '/tasks', label: 'Tasks', icon: ClipboardList, permission: Permissions.Tasks.View },
  { to: '/issues', label: 'Issues', icon: TriangleAlert, permission: Permissions.Issues.View },
  { to: '/reports', label: 'Reports', icon: FileBarChart, permission: Permissions.Reports.View },
  { to: '/settings', label: 'Settings', icon: Settings },
]
