import { useInfiniteQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { BellOff, CheckCheck } from 'lucide-react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { useAuthService, useSession } from '@/auth/context'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

import { groupByDay } from './days'
import { NotificationItem } from './NotificationItem'
import { notificationsListQuery, useMarkAllRead } from './queries'

const route = getRouteApi('/_authenticated/notifications')

export function NotificationsPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const { unread } = route.useSearch()
  const navigate = route.useNavigate()
  const unreadOnly = !!unread
  const list = useInfiniteQuery(notificationsListQuery(client, organizationId, unreadOnly))
  const markAll = useMarkAllRead()

  const items = list.data?.pages.flatMap((p) => p.items) ?? []
  const total = list.data?.pages.at(-1)?.totalCount ?? 0
  const groups = groupByDay(items)
  const setUnread = (next: boolean) =>
    void navigate({ search: { unread: next || undefined }, replace: true })

  return (
    <section className="flex flex-col gap-5">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Notifications</h1>
        <Button
          variant="outline"
          size="lg"
          disabled={markAll.isPending}
          onClick={() =>
            markAll.mutate(undefined, { onError: (error) => toast.error(errorMessage(error)) })
          }
        >
          <CheckCheck aria-hidden />
          Mark all read
        </Button>
      </header>

      <div className="flex gap-2">
        <FilterButton pressed={!unreadOnly} onClick={() => setUnread(false)}>
          All
        </FilterButton>
        <FilterButton pressed={unreadOnly} onClick={() => setUnread(true)}>
          Unread
        </FilterButton>
      </div>

      {list.isError ? (
        <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
          <p>{errorMessage(list.error)}</p>
          <Button variant="outline" onClick={() => void list.refetch()}>
            Try again
          </Button>
        </div>
      ) : list.isPending ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Loading notifications">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-16 w-full rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center gap-2 py-12 text-center">
          <BellOff className="size-10 text-muted-foreground" aria-hidden />
          <h2 className="text-lg font-medium">
            {unreadOnly ? 'No unread notifications' : 'No notifications yet'}
          </h2>
          <p className="text-muted-foreground">You are all caught up.</p>
        </div>
      ) : (
        <>
          {groups.map((group) => (
            <section key={group.day} aria-label={group.label} className="flex flex-col gap-2">
              <h2 className="text-sm font-semibold text-muted-foreground">{group.label}</h2>
              <ul className="divide-y overflow-hidden rounded-xl border bg-card">
                {group.items.map((n) => (
                  <li key={n.id}>
                    <NotificationItem notification={n} />
                  </li>
                ))}
              </ul>
            </section>
          ))}
          <div className="flex flex-col items-center gap-2">
            <p className="text-sm text-muted-foreground">
              Showing {items.length} of {total}
            </p>
            {list.hasNextPage && (
              <Button
                variant="outline"
                size="lg"
                disabled={list.isFetchingNextPage}
                onClick={() => void list.fetchNextPage()}
              >
                {list.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </Button>
            )}
          </div>
        </>
      )}
    </section>
  )
}

function FilterButton({
  pressed,
  onClick,
  children,
}: {
  pressed: boolean
  onClick: () => void
  children: string
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      className={cn(
        'h-11 rounded-full border bg-card px-4 text-base font-medium',
        pressed && 'border-primary bg-accent',
      )}
    >
      {children}
    </button>
  )
}
