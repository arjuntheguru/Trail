import { useId } from 'react'
import { Controller, type Control } from 'react-hook-form'

import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

import { groupBySection } from './answers'
import { FieldRow, Section } from './FieldInput'
import { useTaskForm } from './form-context'
import { PhotoPicker } from './Photos'
import type { FormProblems } from './problems'
import type { TaskField, TaskFormValues } from './types'

interface FieldsEditorProps {
  fields: readonly TaskField[]
  control: Control<TaskFormValues>
  problems: FormProblems
  failures: ReadonlyMap<string, string>
  requiresPhoto: boolean
  noteLabel: string
  onEdit: (key: string) => void
  onClearPhotoProblem: () => void
}

/** Every field of a task in its sections, then the task-level photos (when required) and the note. */
export function FieldsEditor({
  fields,
  control,
  problems,
  failures,
  requiresPhoto,
  noteLabel,
  onEdit,
  onClearPhotoProblem,
}: FieldsEditorProps) {
  return (
    <div className="flex flex-col gap-6">
      {groupBySection(fields).map((group, i) => (
        <Section key={i} title={group.section}>
          {group.fields.map((field) => (
            <FieldRow
              key={field.key}
              field={field}
              control={control}
              error={problems.fields[field.key]}
              failure={failures.get(field.key)}
              onEdit={onEdit}
            />
          ))}
        </Section>
      ))}

      {requiresPhoto && (
        <section
          className="flex flex-col gap-2 rounded-xl border bg-card p-4"
          data-section="photos"
          aria-label="Task photos"
        >
          <h2 className="font-medium">Photo required</h2>
          <p className="text-sm text-muted-foreground">
            Add at least one photo as proof this was done.
          </p>
          <Controller
            control={control}
            name="photoIds"
            render={({ field }) => (
              <PhotoPicker
                label="Task photo"
                value={field.value}
                onChange={(ids) => {
                  field.onChange(ids)
                  onClearPhotoProblem()
                }}
              />
            )}
          />
          {problems.photos && (
            <p role="alert" className="text-sm font-medium text-destructive">
              {problems.photos}
            </p>
          )}
        </section>
      )}

      <NoteField control={control} label={noteLabel} error={problems.note} />
    </div>
  )
}

export function NoteField({
  control,
  label,
  error,
}: {
  control: Control<TaskFormValues>
  label: string
  error?: string
}) {
  const { readOnly } = useTaskForm()
  const id = useId()
  return (
    <div className="flex flex-col gap-2" data-section="note">
      <Label htmlFor={id} className="text-base">
        {label}
      </Label>
      <Controller
        control={control}
        name="note"
        render={({ field }) => (
          <Textarea
            id={id}
            {...field}
            disabled={readOnly}
            aria-invalid={error ? true : undefined}
          />
        )}
      />
      {error && (
        <p role="alert" className="text-sm font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
