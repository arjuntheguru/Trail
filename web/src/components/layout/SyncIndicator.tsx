import { useQueryClient } from '@tanstack/react-query'
import { CloudOff, CloudUpload, LoaderCircle } from 'lucide-react'
import { useMemo } from 'react'

import { useSession } from '@/auth/context'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { TaskDetail } from '@/features/tasks/types'
import { taskKey } from '@/features/tasks/queries'
import { cn } from '@/lib/utils'
import { useOutbox } from '@/offline/context'
import type { OutboxItem } from '@/offline/outbox-types'

const DISCARD_CONFIRMATION =
  'Discard this change? It has not reached the server and cannot be recovered.'

function describe(item: OutboxItem): string {
  switch (item.type) {
    case 'complete':
      return 'Completion'
    case 'entry':
      return 'Reading'
    case 'progress':
      return 'Draft answers'
    case 'upload':
      return `Photo (${item.fileName})`
  }
}

/**
 * Shows that changes made on this device are waiting to reach the server, and lets people retry or discard
 * the ones that were refused. Hidden when there is nothing queued.
 */
export function SyncIndicator({ className }: { className?: string }) {
  const { items, pendingCount, failedCount, syncing, retry, discard, retryAll } = useOutbox()
  const queryClient = useQueryClient()
  const organizationId = useSession().organization?.id

  const groups = useMemo(() => {
    const byTask = new Map<string, OutboxItem[]>()
    for (const item of items) byTask.set(item.taskId, [...(byTask.get(item.taskId) ?? []), item])
    return [...byTask.entries()]
  }, [items])

  if (items.length === 0 && !syncing) return null
  const count = pendingCount + failedCount
  const failed = failedCount > 0
  const label = failed
    ? `${failedCount} ${failedCount === 1 ? 'change' : 'changes'} could not sync`
    : syncing
      ? 'Syncing changes'
      : `${count} ${count === 1 ? 'change' : 'changes'} waiting to sync`
  const Icon = failed ? CloudOff : syncing ? LoaderCircle : CloudUpload

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          aria-label={label}
          data-testid="sync-indicator"
          className={cn(
            'h-9 gap-1.5 px-3',
            failed && 'border-destructive/50 text-destructive',
            className,
          )}
        >
          <Icon className={cn('size-4', syncing && !failed && 'animate-spin')} aria-hidden />
          <span aria-hidden>{count}</span>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="flex max-h-[70vh] flex-col gap-3 overflow-y-auto p-4">
        <h2 className="text-sm font-semibold">Changes on this device</h2>
        {groups.length === 0 && <p className="text-sm text-muted-foreground">Syncing…</p>}
        <ul className="flex flex-col gap-3">
          {groups.map(([taskId, taskItems]) => {
            const title = queryClient.getQueryData<TaskDetail>(
              taskKey(organizationId, taskId),
            )?.title
            return (
              <li key={taskId} className="flex flex-col gap-1.5">
                <p className="text-sm font-medium">{title ?? 'Task'}</p>
                <ul className="flex flex-col gap-2">
                  {taskItems.map((item) => (
                    <li key={item.id} className="rounded-md border p-2 text-sm">
                      <p>
                        {describe(item)}
                        <span className="text-muted-foreground">
                          {item.status === 'failed' ? ' · Sync failed' : ' · Waiting to sync'}
                        </span>
                      </p>
                      {item.status === 'failed' && (
                        <>
                          <p role="alert" className="mt-1 text-destructive">
                            {item.error?.message ?? 'This change could not be synced.'}
                          </p>
                          <div className="mt-2 flex gap-2">
                            <Button size="sm" variant="outline" onClick={() => void retry(item.id)}>
                              Retry
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => {
                                if (window.confirm(DISCARD_CONFIRMATION)) void discard(item.id)
                              }}
                            >
                              Discard
                            </Button>
                          </div>
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              </li>
            )
          })}
        </ul>
        <Button size="sm" disabled={syncing} onClick={() => void retryAll()}>
          Sync now
        </Button>
      </PopoverContent>
    </Popover>
  )
}
