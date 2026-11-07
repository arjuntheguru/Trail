import { Building2, KeyRound, MapPin, Network, Tag, UsersRound, Layers } from 'lucide-react'

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
]

/** The sections these permissions can open, in menu order. */
export function visibleSections(permissions: readonly string[] | undefined) {
  return SETTINGS_SECTIONS.filter((s) => !s.permission || permissions?.includes(s.permission))
}
