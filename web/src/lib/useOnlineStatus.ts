import { onlineManager } from '@tanstack/react-query'
import { useSyncExternalStore } from 'react'

/** Whether the browser believes it has a network; follows TanStack Query's online manager. */
export function useOnlineStatus(): boolean {
  return useSyncExternalStore(
    (listener) => onlineManager.subscribe(listener),
    () => onlineManager.isOnline(),
    () => true,
  )
}
