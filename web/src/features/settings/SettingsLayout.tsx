import { Link, Outlet } from '@tanstack/react-router'

import { useSession } from '@/auth/context'
import { cn } from '@/lib/utils'

import { visibleSections } from './sections'

/** The settings frame: a section menu (a scrolling row on phones, a column from tablet up) beside the page. */
export function SettingsLayout() {
  const sections = visibleSections(useSession().permissions)
  return (
    <div className="flex flex-col gap-4 md:flex-row md:gap-8">
      <div className="flex min-w-0 flex-col gap-3 md:w-52 md:shrink-0">
        <h1 className="text-2xl font-semibold">Settings</h1>
        <nav
          aria-label="Settings"
          className="-mx-4 overflow-x-auto px-4 md:mx-0 md:overflow-visible md:px-0"
        >
          <ul className="flex gap-1 md:flex-col">
            {sections.map(({ to, label, icon: Icon }) => (
              <li key={to} className="shrink-0">
                <Link
                  to={to}
                  className={cn(
                    'flex h-11 items-center gap-2 rounded-lg px-3 text-base font-medium whitespace-nowrap text-muted-foreground transition-colors hover:bg-accent md:h-10 md:text-sm',
                  )}
                  activeProps={{ className: 'bg-accent text-accent-foreground' }}
                >
                  <Icon className="size-4" aria-hidden />
                  {label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </div>
      <div className="min-w-0 flex-1">
        <Outlet />
      </div>
    </div>
  )
}
