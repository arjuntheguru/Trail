import { formatDay, localDay } from '@/lib/dates'

import type { Notification } from './queries'

/** An API instant (UTC when it carries no offset) as a Date. */
export function instantOf(iso: string): Date {
  return new Date(/(?:Z|[+-]\d{2}:?\d{2})$/i.test(iso) ? iso : `${iso}Z`)
}

export interface DayGroup {
  /** yyyy-mm-dd in the viewer's zone. */
  day: string
  label: string
  items: Notification[]
}

/** Groups newest-first notifications by the viewer's calendar day, keeping their order. */
export function groupByDay(items: Notification[], now: Date = new Date()): DayGroup[] {
  const today = localDay(now)
  const yesterday = localDay(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1))
  const groups: DayGroup[] = []
  for (const item of items) {
    const day = localDay(instantOf(item.createdAt))
    let group = groups.find((g) => g.day === day)
    if (!group) {
      group = {
        day,
        label: day === today ? 'Today' : day === yesterday ? 'Yesterday' : formatDay(day),
        items: [],
      }
      groups.push(group)
    }
    group.items.push(item)
  }
  return groups
}
