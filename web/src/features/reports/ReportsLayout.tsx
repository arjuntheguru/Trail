import { getRouteApi, Link, Outlet } from '@tanstack/react-router'

import { usePermission } from '@/auth/context'
import { Permissions } from '@/auth/permissions'

const route = getRouteApi('/_authenticated/reports')

const TABS = [
  { to: '/reports', label: 'Overview', exact: true },
  { to: '/reports/compliance', label: 'Compliance', exact: false },
  { to: '/reports/activity', label: 'Activity', exact: false },
] as const

/** The Reports header and tab bar; the filters in the URL carry across the tabs. */
export function ReportsLayout() {
  const canView = usePermission(Permissions.Reports.View)
  const search = route.useSearch()

  if (!canView) {
    return (
      <section className="flex flex-col gap-2 py-12 text-center">
        <h1 className="text-2xl font-semibold">Reports</h1>
        <p className="text-muted-foreground">
          You do not have access to reports in this organization.
        </p>
      </section>
    )
  }

  return (
    <section className="flex flex-col gap-5">
      <header className="flex flex-col gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Reports</h1>
        <nav aria-label="Reports sections" className="flex gap-1 border-b">
          {TABS.map((tab) => (
            <Link
              key={tab.to}
              to={tab.to}
              search={search}
              activeOptions={{ exact: tab.exact }}
              className="-mb-px border-b-2 border-transparent px-4 py-3 text-base font-medium text-muted-foreground outline-none hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50"
              activeProps={{ className: 'border-primary text-foreground' }}
            >
              {tab.label}
            </Link>
          ))}
        </nav>
      </header>
      <Outlet />
    </section>
  )
}
