import {
  Bell,
  Building2,
  CalendarClock,
  ClipboardList,
  History,
  KeyRound,
  Layers,
  MapPin,
  Network,
  Plug,
  Tag,
  UsersRound,
} from 'lucide-react'

import { Permissions } from '@/auth/permissions'

export interface SettingsSection {
  to:
    | '/settings/organization'
    | '/settings/sites'
    | '/settings/regions'
    | '/settings/departments'
    | '/settings/tags'
    | '/settings/members'
    | '/settings/roles'
    | '/settings/templates'
    | '/settings/schedules'
    | '/settings/integrations'
    | '/settings/audit'
    | '/settings/notifications'
  label: string
  icon: typeof Building2
  /** Needed to open the section; every member may see the organization. */
  permission?: string
}

export const SETTINGS_SECTIONS: readonly SettingsSection[] = [
  { to: '/settings/organization', label: 'Organization', icon: Building2 },
  { to: '/settings/sites', label: 'Sites', icon: MapPin, permission: Permissions.Sites.View },
  { to: '/settings/regions', label: 'Regions', icon: Network, permission: Permissions.Sites.View },
  {
    to: '/settings/departments',
    label: 'Departments',
    icon: Layers,
    permission: Permissions.Sites.View,
  },
  { to: '/settings/tags', label: 'Tags', icon: Tag, permission: Permissions.Templates.View },
  {
    to: '/settings/members',
    label: 'Members',
    icon: UsersRound,
    permission: Permissions.Members.View,
  },
  { to: '/settings/roles', label: 'Roles', icon: KeyRound, permission: Permissions.Members.View },
  {
    to: '/settings/templates',
    label: 'Templates',
    icon: ClipboardList,
    permission: Permissions.Templates.View,
  },
  {
    to: '/settings/schedules',
    label: 'Schedules',
    icon: CalendarClock,
    permission: Permissions.Schedules.Manage,
  },
  {
    to: '/settings/integrations',
    label: 'Integrations',
    icon: Plug,
    permission: Permissions.Integrations.Manage,
  },
  { to: '/settings/audit', label: 'Audit log', icon: History, permission: Permissions.Audit.View },
  { to: '/settings/notifications', label: 'Notifications', icon: Bell },
]

/** The sections these permissions can open, in menu order. */
export function visibleSections(permissions: readonly string[] | undefined) {
  return SETTINGS_SECTIONS.filter((s) => !s.permission || permissions?.includes(s.permission))
}
