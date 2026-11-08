import { onlineManager } from '@tanstack/react-query'

/** Whether the device believes it has a network (the browser flag and TanStack Query's manager agree). */
export function isOnline(): boolean {
  if (typeof navigator !== 'undefined' && navigator.onLine === false) return false
  return onlineManager.isOnline()
}
