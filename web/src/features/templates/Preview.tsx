import { Camera, PenLine } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'

import { emptyFormValues, groupBySection, toServerAnswers } from '@/features/tasks/answers'
import { evaluateAnswers } from '@/features/tasks/evaluate'
import { FieldRow, Section } from '@/features/tasks/FieldInput'
import { TaskFormProvider } from '@/features/tasks/form-context'
import type { TaskFormValues } from '@/features/tasks/types'

import { previewable } from './previewable'
import type { TemplateField } from './types'

/**
 * Renders fields exactly as the person completing the task will see them, in a throwaway form: nothing typed
 * here is saved. Remounts only when the shape changes (keys or types), so editing a label does not wipe answers.
 */
export function Preview({ fields }: { fields: readonly TemplateField[] }) {
  const usable = useMemo(() => previewable(fields), [fields])
  const shape = usable.map((f) => `${f.key}:${f.type}`).join('|')
  return (
    <TaskFormProvider siteId="" readOnly={false}>
      <Sandbox key={shape} fields={usable} />
    </TaskFormProvider>
  )
}

function Sandbox({ fields }: { fields: readonly TemplateField[] }) {
  const [defaults] = useState(() => emptyFormValues(fields))
  const form = useForm<TaskFormValues>({ defaultValues: defaults })
  const values = useWatch({ control: form.control }) as TaskFormValues

  const failures = useMemo(
    () =>
      new Map(
        evaluateAnswers(fields, toServerAnswers(fields, values.answers ?? {}), false).failures.map(
          (f) => [f.fieldKey, f.reason],
        ),
      ),
    [fields, values.answers],
  )

  if (fields.length === 0) {
    return (
      <p className="rounded-lg border border-dashed p-6 text-center text-muted-foreground">
        Add a field to see how it will look.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-6" data-testid="template-preview">
      {groupBySection(fields).map((group, i) => (
        <Section key={i} title={group.section}>
          {group.fields.map((field) =>
            field.type === 'Photo' || field.type === 'Signature' ? (
              <Placeholder key={field.key} field={field} />
            ) : (
              <FieldRow
                key={field.key}
                field={field}
                control={form.control}
                failure={failures.get(field.key)}
              />
            ),
          )}
        </Section>
      ))}
    </div>
  )
}

/** Camera and signature capture need a site to upload to, so the preview only shows where they appear. */
function Placeholder({ field }: { field: TemplateField }) {
  const Icon = field.type === 'Photo' ? Camera : PenLine
  return (
    <div className="flex flex-col gap-2 rounded-xl border bg-card p-4" data-field-key={field.key}>
      <p className="text-base leading-snug font-medium">
        {field.label}
        {field.required === false && (
          <span className="font-normal text-muted-foreground"> (optional)</span>
        )}
      </p>
      {field.helpText && <p className="text-sm text-muted-foreground">{field.helpText}</p>}
      <p className="flex items-center gap-2 rounded-lg border border-dashed p-3 text-sm text-muted-foreground">
        <Icon className="size-4" aria-hidden />
        {field.type === 'Photo' ? 'Photo capture' : 'Signature pad'} (not available in the preview)
      </p>
    </div>
  )
}
