import { PhotoGallery } from '@/components/Photos'
import { formatDateTime } from '@/lib/dates'

import { describeEvent } from './describe'
import type { IssueEvent } from './types'

/** What the note means for this kind of event. */
function noteLabel(event: IssueEvent): string | null {
  if (event.kind !== 'StatusChanged') return null
  if (event.to === 'Resolved') return 'Corrective action'
  if (event.to === 'Open') return 'Reason'
  return 'Note'
}

/** The history of an issue, oldest first, as the API returns it. */
export function Timeline({
  events,
  timeZone,
}: {
  events: readonly IssueEvent[]
  timeZone?: string
}) {
  return (
    <ol aria-label="Timeline" className="flex flex-col gap-4 border-l pl-4">
      {events.map((event) => {
        const label = noteLabel(event)
        return (
          <li key={event.id} className="relative flex flex-col gap-1" data-testid="timeline-event">
            <span
              className="absolute top-2 -left-[1.3rem] size-2.5 rounded-full bg-border"
              aria-hidden
            />
            <p className="text-sm">
              <span className="font-medium">{event.actorName ?? 'Someone'}</span>{' '}
              {describeEvent(event, timeZone)}
            </p>
            <p className="text-xs text-muted-foreground">
              {formatDateTime(event.occurredAt, timeZone)}
            </p>
            {event.note && (
              <p className="rounded-lg bg-muted px-3 py-2 text-base whitespace-pre-wrap">
                {label && <span className="font-medium">{label}: </span>}
                {event.note}
              </p>
            )}
            <PhotoGallery ids={event.photoIds} label="Event photo" />
          </li>
        )
      })}
    </ol>
  )
}
