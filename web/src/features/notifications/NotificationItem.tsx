import { useNavigate } from '@tanstack/react-router'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { cn } from '@/lib/utils'

import { instantOf } from './days'
import { useMarkRead, type Notification } from './queries'

const time = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' })

/** One notification. Opening it marks it read (when it is not) and goes to the issue or task it is about. */
export function NotificationItem({
  notification,
  onOpen,
}: {
  notification: Notification
  /** Called once the user has chosen it, e.g. to close the popover. */
  onOpen?: () => void
}) {
  const navigate = useNavigate()
  const markRead = useMarkRead()
  const unread = notification.readAt === null

  const open = () => {
    if (unread) {
      markRead.mutate(notification.id, { onError: (error) => toast.error(errorMessage(error)) })
    }
    onOpen?.()
    if (notification.subjectType === 'Task') {
      void navigate({ to: '/tasks/$taskId', params: { taskId: notification.subjectId } })
    } else {
      void navigate({ to: '/issues/$issueId', params: { issueId: notification.subjectId } })
    }
  }

  return (
    <button
      type="button"
      onClick={open}
      data-unread={unread}
      className={cn(
        'flex w-full items-start gap-3 px-4 py-3 text-left transition-colors hover:bg-accent',
        unread && 'bg-accent/40',
      )}
    >
      <span
        aria-hidden
        className={cn(
          'mt-2 size-2 shrink-0 rounded-full',
          unread ? 'bg-primary' : 'bg-transparent',
        )}
      />
      <span className="min-w-0 flex-1">
        <span className={cn('block text-base', unread ? 'font-semibold' : 'font-medium')}>
          {notification.title}
        </span>
        {notification.body && (
          <span className="block text-sm text-muted-foreground">{notification.body}</span>
        )}
        <span className="block text-xs text-muted-foreground">
          {time.format(instantOf(notification.createdAt))}
          <span className="sr-only">{unread ? ', unread' : ', read'}</span>
        </span>
      </span>
    </button>
  )
}
