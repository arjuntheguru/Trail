import { Check, Info, TriangleAlert } from 'lucide-react'
import { useId, type ReactNode } from 'react'
import { useController, type Control } from 'react-hook-form'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'

import { isRequired, ruleHint, unitOf } from './evaluate'
import { useTaskForm } from './form-context'
import { PhotoPicker } from './Photos'
import { SignaturePad } from './SignaturePad'
import type { FormAnswer, TaskField, TaskFormValues } from './types'

interface FieldRowProps {
  field: TaskField
  control: Control<TaskFormValues>
  /** A blocking problem: shown in red and focused on submit. */
  error?: string
  /** The answer is outside the rules; allowed, but needs evidence before completing. */
  failure?: string
  /** Called when the person edits, so the parent can clear a stale error. */
  onEdit?: (key: string) => void
}

const choiceClass = (selected: boolean) =>
  cn(
    'flex min-h-14 flex-1 items-center justify-center gap-2 rounded-xl border-2 px-4 py-2 text-lg font-medium transition-colors',
    selected
      ? 'border-primary bg-primary text-primary-foreground'
      : 'border-input bg-card hover:bg-accent',
  )

export function FieldRow({ field, control, error, failure, onEdit }: FieldRowProps) {
  const { readOnly } = useTaskForm()
  const id = useId()
  const {
    field: { value: answer, onChange },
  } = useController({ name: `answers.${field.key}`, control })

  if (field.type === 'Instruction') {
    return (
      <div
        className="flex gap-3 rounded-xl border border-primary/30 bg-primary/5 p-4"
        data-field-key={field.key}
      >
        <Info className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
        <div>
          <p className="font-medium">{field.label}</p>
          {field.helpText && <p className="mt-1 text-sm text-muted-foreground">{field.helpText}</p>}
        </div>
      </div>
    )
  }

  const current = answer as FormAnswer | undefined
  if (!current) return null
  const set = (patch: Partial<FormAnswer>) => {
    onChange({ ...current, ...patch })
    onEdit?.(field.key)
  }
  const hint = ruleHint(field)
  const describedBy = [error && `${id}-error`, hint && `${id}-hint`].filter(Boolean).join(' ')
  const invalid = error ? true : undefined

  const showEvidence =
    !!failure || current.comment !== '' || (field.type !== 'Photo' && current.photoIds.length > 0)
  const wantsPhoto = !!field.photoRequiredOnFailure || current.photoIds.length > 0

  return (
    <div
      className={cn(
        'flex flex-col gap-2 rounded-xl border bg-card p-4',
        error && 'border-destructive',
        !error && failure && 'border-destructive/50',
      )}
      data-field-key={field.key}
    >
      <Label id={`${id}-label`} htmlFor={`${id}-input`} className="text-base leading-snug">
        {field.label}
        {!isRequired(field) && (
          <span className="font-normal text-muted-foreground"> (optional)</span>
        )}
      </Label>
      {field.helpText && <p className="text-sm text-muted-foreground">{field.helpText}</p>}

      <FieldControl
        field={field}
        id={`${id}-input`}
        labelId={`${id}-label`}
        answer={current}
        set={set}
        readOnly={readOnly}
        describedBy={describedBy || undefined}
        invalid={invalid}
      />

      {hint && (
        <p id={`${id}-hint`} className="text-sm text-muted-foreground">
          {hint}
        </p>
      )}
      {failure && (
        <p
          className="flex items-start gap-2 text-sm font-medium text-destructive"
          data-testid="failure"
        >
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          {failure}
        </p>
      )}
      {showEvidence && (
        <div className="mt-1 flex flex-col gap-3 border-t pt-3">
          {field.type !== 'Photo' && (field.photoRequiredOnFailure || wantsPhoto) && (
            <div className="flex flex-col gap-2">
              <span className="text-sm font-medium">
                Photo evidence{field.photoRequiredOnFailure && failure ? ' (required)' : ''}
              </span>
              <PhotoPicker
                label={`${field.label} photo`}
                value={current.photoIds}
                onChange={(photoIds) => set({ photoIds })}
              />
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${id}-comment`} className="text-sm">
              {failure && field.commentRequiredOnFailure !== false
                ? 'What did you do about it? (required)'
                : 'Comment'}
            </Label>
            <Textarea
              id={`${id}-comment`}
              value={current.comment}
              disabled={readOnly}
              onChange={(e) => set({ comment: e.target.value })}
              className="min-h-20"
            />
          </div>
        </div>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

interface ControlProps {
  field: TaskField
  id: string
  labelId: string
  answer: FormAnswer
  set: (patch: Partial<FormAnswer>) => void
  readOnly: boolean
  describedBy?: string
  invalid?: true
}

function FieldControl({
  field,
  id,
  labelId,
  answer,
  set,
  readOnly,
  describedBy,
  invalid,
}: ControlProps) {
  const options = field.options ?? []
  const value = answer.value
  const aria = { 'aria-describedby': describedBy, 'aria-invalid': invalid }

  switch (field.type) {
    case 'Checkbox':
      return (
        <button
          id={id}
          type="button"
          role="checkbox"
          aria-checked={value === true}
          aria-labelledby={labelId}
          {...aria}
          disabled={readOnly}
          onClick={() => set({ value: value !== true })}
          className={choiceClass(value === true)}
        >
          {value === true ? (
            <>
              <Check className="size-6" aria-hidden /> Done
            </>
          ) : (
            'Tap when done'
          )}
        </button>
      )

    case 'YesNo':
      return (
        <div role="radiogroup" aria-labelledby={labelId} {...aria} className="flex gap-3">
          {[true, false].map((option) => (
            <button
              key={String(option)}
              type="button"
              role="radio"
              aria-checked={value === option}
              disabled={readOnly}
              onClick={() => set({ value: option })}
              className={choiceClass(value === option)}
            >
              {option ? 'Yes' : 'No'}
            </button>
          ))}
        </div>
      )

    case 'Number':
    case 'Temperature': {
      const text = typeof value === 'string' ? value : ''
      const unit = unitOf(field)
      return (
        <div className="flex items-center gap-2">
          <Input
            id={id}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={text}
            disabled={readOnly}
            onChange={(e) => set({ value: e.target.value })}
            className="h-14 flex-1 text-xl md:text-xl"
            {...aria}
          />
          {unit && (
            <span className="min-w-8 text-lg font-medium text-muted-foreground">{unit}</span>
          )}
          {/* Phone decimal keypads have no minus key; freezers need one. */}
          <button
            type="button"
            aria-label="Toggle minus sign"
            disabled={readOnly}
            onClick={() =>
              set({ value: text.trim().startsWith('-') ? text.trim().slice(1) : `-${text.trim()}` })
            }
            className="grid size-14 shrink-0 place-items-center rounded-xl border-2 border-input bg-card text-xl font-medium"
          >
            ±
          </button>
        </div>
      )
    }

    case 'Text':
      return (
        <Input
          id={id}
          value={typeof value === 'string' ? value : ''}
          disabled={readOnly}
          onChange={(e) => set({ value: e.target.value })}
          className="h-12"
          {...aria}
        />
      )

    case 'LongText':
      return (
        <Textarea
          id={id}
          value={typeof value === 'string' ? value : ''}
          disabled={readOnly}
          onChange={(e) => set({ value: e.target.value })}
          {...aria}
        />
      )

    case 'SingleSelect':
      if (options.length > 6) {
        return (
          <select
            id={id}
            value={typeof value === 'string' ? value : ''}
            disabled={readOnly}
            onChange={(e) => set({ value: e.target.value })}
            className="h-12 w-full rounded-md border border-input bg-card px-3 text-base"
            {...aria}
          >
            <option value="">Choose…</option>
            {options.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        )
      }
      return (
        <div role="radiogroup" aria-labelledby={labelId} {...aria} className="flex flex-col gap-2">
          {options.map((o) => (
            <button
              key={o.value}
              type="button"
              role="radio"
              aria-checked={value === o.value}
              disabled={readOnly}
              onClick={() => set({ value: o.value })}
              className={choiceClass(value === o.value)}
            >
              {o.label}
            </button>
          ))}
        </div>
      )

    case 'MultiSelect': {
      const picked = Array.isArray(value) ? (value as string[]) : []
      return (
        <div role="group" aria-labelledby={labelId} {...aria} className="flex flex-col gap-2">
          {options.map((o) => {
            const selected = picked.includes(o.value)
            return (
              <button
                key={o.value}
                type="button"
                role="checkbox"
                aria-checked={selected}
                disabled={readOnly}
                onClick={() =>
                  set({
                    value: selected ? picked.filter((v) => v !== o.value) : [...picked, o.value],
                  })
                }
                className={choiceClass(selected)}
              >
                {selected && <Check className="size-5" aria-hidden />}
                {o.label}
              </button>
            )
          })}
        </div>
      )
    }

    case 'Date':
    case 'Time':
      return (
        <Input
          id={id}
          type={field.type === 'Date' ? 'date' : 'time'}
          value={typeof value === 'string' ? value : ''}
          disabled={readOnly}
          onChange={(e) => set({ value: e.target.value })}
          className="h-12 max-w-xs"
          {...aria}
        />
      )

    case 'Photo':
      return (
        <PhotoPicker
          label={field.label}
          value={answer.photoIds}
          onChange={(photoIds) => set({ photoIds })}
        />
      )

    case 'Signature':
      return (
        <SignaturePad
          label={field.label}
          value={typeof value === 'string' ? value : ''}
          onChange={(id) => set({ value: id })}
        />
      )

    default:
      return null
  }
}

export function Section({ title, children }: { title: string | null; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3" aria-label={title ?? undefined}>
      {title && (
        <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          {title}
        </h2>
      )}
      {children}
    </section>
  )
}
