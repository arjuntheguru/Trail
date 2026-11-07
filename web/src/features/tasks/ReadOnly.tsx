import { TriangleAlert } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { formatTime } from '@/lib/dates'

import { groupBySection } from './answers'
import { isAnswerable } from './evaluate'
import { formatAnswer } from './format'
import { AttachmentImage, PhotoGallery } from './Photos'
import type { TaskEntry, TaskField } from './types'

/** The saved answers of one entry, with the reason of every failed check. Nothing here can be edited. */
export function AnswerList({ fields, entry }: { fields: readonly TaskField[]; entry: TaskEntry }) {
  const failures = new Map(entry.failures.map((f) => [f.fieldKey, f.reason]))
  return (
    <div className="flex flex-col gap-4">
      {groupBySection(fields).map((group, i) => (
        <section key={i} className="flex flex-col gap-2" aria-label={group.section ?? undefined}>
          {group.section && (
            <h3 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              {group.section}
            </h3>
          )}
          {group.fields.filter(isAnswerable).map((field) => {
            const answer = entry.answers[field.key]
            const failure = failures.get(field.key)
            const text = formatAnswer(field, answer)
            return (
              <div
                key={field.key}
                data-field-key={field.key}
                className="rounded-lg border bg-card p-3"
              >
                <p className="text-sm text-muted-foreground">{field.label}</p>
                {text !== null && <p className="text-base font-medium">{text}</p>}
                {field.type === 'Signature' && typeof answer?.value === 'string' && (
                  <div className="mt-1 h-20 w-40 overflow-hidden rounded border bg-white">
                    <AttachmentImage
                      id={answer.value}
                      alt={`${field.label} signature`}
                      className="object-contain"
                    />
                  </div>
                )}
                {failure && (
                  <p className="mt-1 flex items-start gap-2 text-sm font-medium text-destructive">
                    <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                    {failure}
                  </p>
                )}
                {answer?.comment && <p className="mt-1 text-sm">“{answer.comment}”</p>}
                {(answer?.photoIds?.length ?? 0) > 0 && (
                  <div className="mt-2">
                    <PhotoGallery ids={answer!.photoIds!} label={`${field.label} photo`} />
                  </div>
                )}
              </div>
            )
          })}
        </section>
      ))}
      {entry.note && (
        <div className="rounded-lg border bg-card p-3">
          <p className="text-sm text-muted-foreground">Note</p>
          <p className="text-base whitespace-pre-wrap">{entry.note}</p>
        </div>
      )}
      {entry.photoIds.length > 0 && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-muted-foreground">Task photos</p>
          <PhotoGallery ids={entry.photoIds} label="Task photo" />
        </div>
      )}
    </div>
  )
}

/** One submitted reading of a record log. */
export function ReadingCard({
  fields,
  entry,
  timeZone,
}: {
  fields: readonly TaskField[]
  entry: TaskEntry
  timeZone: string
}) {
  return (
    <article className="flex flex-col gap-3 rounded-xl border p-4" data-testid="reading">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-medium">
          {entry.submittedAt ? formatTime(entry.submittedAt, timeZone) : 'Draft'}
          {entry.submittedBy && (
            <span className="font-normal text-muted-foreground"> · {entry.submittedBy}</span>
          )}
        </p>
        {entry.hasFailures && <Badge variant="destructive">Failed checks</Badge>}
      </header>
      <AnswerList fields={fields} entry={entry} />
    </article>
  )
}
