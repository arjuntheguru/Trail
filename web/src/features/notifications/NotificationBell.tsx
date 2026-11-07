import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Bell, CheckCheck } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { useAuthService, useSession } from '@/auth/context'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

import { NotificationItem } from './NotificationItem'
import { recentNotificationsQuery, unreadCountQuery, useMarkAllRead } from './queries'

/** The top bar bell: unread count, and the latest notifications in a popover. */
export function NotificationBell({ className }: { className?: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const [open, setOpen] = useState(false)
  const count = useQuery(unreadCountQuery(client, organizationId))
  const unread = count.data?.count ?? 0

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className={cn('relative', className)}
          aria-label={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'}
        >
          <Bell className="size-5" aria-hidden />
          {unread > 0 && (
            <span
              data-testid="notification-count"
              aria-hidden
              className="absolute top-1 right-1 inline-grid min-w-4 place-items-center rounded-full bg-destructive px-1 text-[10px] leading-4 font-semibold text-white"
            >
              {unread > 99 ? '99+' : unread}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="p-0">
        <BellList unread={unread} onClose={() => setOpen(false)} />
      </PopoverContent>
    </Popover>
  )
}

function BellList({ unread, onClose }: { unread: number; onClose: () => void }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const recent = useQuery(recentNotificationsQuery(client, organizationId))
  const markAll = useMarkAllRead()

  return (
    <div className="flex flex-col">
      <div className="flex items-center justify-between gap-2 border-b px-4 py-2">
        <h2 className="text-base font-semibold">Notifications</h2>
        <Button
          variant="ghost"
          size="sm"
          disabled={unread === 0 || markAll.isPending}
          onClick={() =>
            markAll.mutate(undefined, { onError: (error) => toast.error(errorMessage(error)) })
          }
        >
          <CheckCheck aria-hidden />
          Mark all read
        </Button>
      </div>
      <div className="max-h-[min(24rem,60svh)] overflow-y-auto">
        {recent.isPending ? (
          <div className="flex flex-col gap-2 p-4" role="status" aria-label="Loading notifications">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : recent.isError ? (
          <div role="alert" className="flex flex-col items-center gap-2 p-6 text-center">
            <p>{errorMessage(recent.error)}</p>
            <Button variant="outline" onClick={() => void recent.refetch()}>
              Try again
            </Button>
          </div>
        ) : recent.data.items.length === 0 ? (
          <p className="p-6 text-center text-muted-foreground">You are all caught up.</p>
        ) : (
          <ul aria-label="Recent notifications" className="divide-y">
            {recent.data.items.map((n) => (
              <li key={n.id}>
                <NotificationItem notification={n} onOpen={onClose} />
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="border-t p-1 text-center">
        <Button asChild variant="ghost" className="w-full">
          <Link to="/notifications" onClick={onClose}>
            See all
          </Link>
        </Button>
      </div>
    </div>
  )
}
