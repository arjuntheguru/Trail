import { onlineManager } from '@tanstack/react-query'
import { vi } from 'vitest'

/** Simulates the device losing or regaining its network, as the browser reports it. */
export function setOnline(online: boolean) {
  vi.spyOn(window.navigator, 'onLine', 'get').mockReturnValue(online)
  window.dispatchEvent(new Event(online ? 'online' : 'offline'))
}

/** Call from `afterEach` so one test's connectivity never leaks into the next. */
export function resetNetwork() {
  vi.restoreAllMocks()
  onlineManager.setOnline(true)
}
