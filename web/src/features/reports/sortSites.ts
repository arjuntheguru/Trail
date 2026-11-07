import type { components } from '@/api/schema'

type SiteRow = components['schemas']['DashboardSiteResponse']

export type SortKey =
  | 'siteName'
  | 'score'
  | 'completed'
  | 'overdue'
  | 'missedYesterday'
  | 'openIssues'
  | 'overdueIssues'

function sortValue(row: SiteRow, key: SortKey): string | number | null {
  if (key === 'completed') return row.dueToday === 0 ? null : row.completed / row.dueToday
  return row[key]
}

/** Rows ordered by the column; sites with no value (nothing due) always go last. */
export function sortSites(rows: SiteRow[], key: SortKey, dir: 'asc' | 'desc'): SiteRow[] {
  const sign = dir === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    const x = sortValue(a, key)
    const y = sortValue(b, key)
    if (x == null && y == null) return 0
    if (x == null) return 1
    if (y == null) return -1
    const order =
      typeof x === 'string' && typeof y === 'string'
        ? x.localeCompare(y, undefined, { sensitivity: 'base' })
        : Number(x) - Number(y)
    return order * sign
  })
}
