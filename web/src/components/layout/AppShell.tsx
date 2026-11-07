import { Link, Outlet, useLocation } from '@tanstack/react-router'

import { hasPermission } from '@/auth/permissions'
import { useAuthState } from '@/auth/context'
import { IssuesNavBadge } from '@/features/issues/NavBadge'
import { cn } from '@/lib/utils'

import { EmailBanner } from './EmailBanner'
import { NAV_ITEMS } from './nav'
import { OrganizationSwitcher } from './OrganizationSwitcher'
import { UserMenu } from './UserMenu'

const linkBase = 'flex items-center gap-3 rounded-lg px-3 text-base font-medium transition-colors'

export function AppShell() {
  const state = useAuthState()
  // Settings has lists and forms that use the room a desktop gives them.
  const wide = useLocation({ select: (l) => l.pathname.startsWith('/settings') })
  // The session ends before the redirect to /login lands; render nothing in between.
  if (state.status !== 'authenticated') return null
  const items = NAV_ITEMS.filter(
    (i) => !i.permission || hasPermission(state.session.permissions, i.permission),
  )

  return (
    <div className="flex min-h-svh">
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col gap-4 border-r bg-card p-4 md:flex">
        <div className="flex items-center gap-2 px-2 text-xl font-semibold tracking-tight">
          <span className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground">
            T
          </span>
          Trail
        </div>
        <OrganizationSwitcher />
        <nav aria-label="Main" className="flex flex-1 flex-col gap-1">
          {items.map(({ to, label, icon: Icon }) => (
            <Link
              key={to}
              to={to}
              className={cn(linkBase, 'h-11 text-muted-foreground hover:bg-accent')}
              activeProps={{ className: 'bg-accent text-accent-foreground' }}
            >
              <Icon className="size-5" aria-hidden />
              {label}
              {to === '/issues' && <IssuesNavBadge className="ml-auto" />}
            </Link>
          ))}
        </nav>
        <UserMenu />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        {/* Mobile top bar */}
        <header className="sticky top-0 z-20 flex items-center justify-between gap-2 border-b bg-card px-3 py-2 pt-[max(0.5rem,env(safe-area-inset-top))] md:hidden">
          <OrganizationSwitcher compact />
          <UserMenu compact />
        </header>
        <EmailBanner />
        <main
          className={cn(
            'mx-auto w-full flex-1 px-4 pt-4 pb-28 md:pb-8',
            wide ? 'max-w-5xl' : 'max-w-3xl',
          )}
        >
          <Outlet />
        </main>
      </div>

      {/* Mobile bottom navigation */}
      <nav
        aria-label="Main"
        className="fixed inset-x-0 bottom-0 z-20 flex border-t bg-card pb-[env(safe-area-inset-bottom)] md:hidden"
      >
        {items.map(({ to, label, icon: Icon }) => (
          <Link
            key={to}
            to={to}
            className="flex min-h-16 flex-1 flex-col items-center justify-center gap-1 text-xs font-medium text-muted-foreground"
            activeProps={{ className: 'text-primary' }}
          >
            <span className="relative">
              <Icon className="size-6" aria-hidden />
              {to === '/issues' && (
                <IssuesNavBadge className="absolute -top-1.5 left-3.5 min-w-4 text-[10px] leading-4" />
              )}
            </span>
            {label}
          </Link>
        ))}
      </nav>
    </div>
  )
}
