import { WifiOff } from 'lucide-react'

import { useOnlineStatus } from '@/lib/useOnlineStatus'

export function OfflineBanner() {
  const online = useOnlineStatus()
  if (online) return null
  return (
    <div
      role="status"
      className="flex items-center gap-2 border-b bg-muted px-4 py-1.5 text-sm text-muted-foreground"
    >
      <WifiOff className="size-4 shrink-0" aria-hidden />
      You&apos;re offline. Showing saved data; changes you make will sync when you reconnect.
    </div>
  )
}
