import { useCallback, useReducer } from 'react'

const key = (organizationId: string) => `trail.lastSite.${organizationId}`

function read(organizationId: string): string | null {
  try {
    return localStorage.getItem(key(organizationId))
  } catch {
    return null
  }
}

/**
 * The site shown on Today. The last choice is remembered per organization in localStorage (a site id is not a
 * secret) and falls back to the first site when it is gone or was never chosen.
 */
export function useSelectedSite(organizationId: string, siteIds: readonly string[]) {
  const [, rerender] = useReducer((n: number) => n + 1, 0)
  const stored = read(organizationId)
  const selected = stored && siteIds.includes(stored) ? stored : (siteIds[0] ?? null)

  const select = useCallback(
    (siteId: string) => {
      try {
        localStorage.setItem(key(organizationId), siteId)
      } catch {
        // Storage can be unavailable (private mode); the choice is then simply not remembered.
      }
      rerender()
    },
    [organizationId],
  )

  return [selected, select] as const
}
