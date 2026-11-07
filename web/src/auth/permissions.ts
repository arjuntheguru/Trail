/** Permission strings issued by the API (see Trail.Application/Common/Security/Permissions.cs). */
export const Permissions = {
  Organization: { Manage: 'organization.manage' },
  Members: { View: 'members.view', Manage: 'members.manage' },
  Sites: { View: 'sites.view', Manage: 'sites.manage' },
  Templates: { View: 'templates.view', Manage: 'templates.manage' },
  Tasks: {
    View: 'tasks.view',
    Complete: 'tasks.complete',
    CreateAdHoc: 'tasks.create_ad_hoc',
    Approve: 'tasks.approve',
  },
  Issues: { View: 'issues.view', Raise: 'issues.raise', Manage: 'issues.manage' },
  Reports: { View: 'reports.view', Export: 'reports.export' },
  Audit: { View: 'audit.view' },
} as const

/** Pure check used by the hook and by route guards. */
export function hasPermission(permissions: readonly string[] | undefined, permission: string) {
  return permissions?.includes(permission) ?? false
}
