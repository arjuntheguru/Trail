import { ArrowDown, ArrowUp, ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react'
import { useId, useState, type KeyboardEvent, type ReactNode } from 'react'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

import {
  addOption,
  moveOption,
  removeOption,
  updateOption,
  type DraftField,
  type ExpectedAnswer,
} from './draft'
import {
  FAILABLE_TYPES,
  FIELD_TYPE_LABELS,
  FIELD_TYPES,
  LIMITS,
  OPTION_TYPES,
  RANGE_TYPES,
  type FieldType,
} from './types'

const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

function Labelled({
  label,
  hint,
  children,
}: {
  label: string
  hint?: string
  children: (id: string) => ReactNode
}) {
  const id = useId()
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children(id)}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  )
}

function Check({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <input
        type="checkbox"
        className="size-5 accent-primary"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  )
}

export interface FieldCardProps {
  field: DraftField
  /** Zero-based position. */
  index: number
  count: number
  /** Problems with this field (client checks and the server's), already worded for the card. */
  problems: readonly string[]
  onChange: (patch: Partial<Omit<DraftField, 'id'>>) => void
  /** Replaces the field wholesale; used by the option editor. */
  onReplace: (next: DraftField) => void
  onMove: (delta: number) => void
  onRemove: () => void
}

export function FieldCard({
  field,
  index,
  count,
  problems,
  onChange,
  onReplace,
  onMove,
  onRemove,
}: FieldCardProps) {
  const [open, setOpen] = useState(true)
  const number = index + 1
  const ranged = RANGE_TYPES.includes(field.type)
  const answerable = field.type !== 'Instruction'
  const failable = FAILABLE_TYPES.includes(field.type)

  // Alt+Up / Alt+Down moves the field without leaving the keyboard.
  const onKeyDown = (e: KeyboardEvent<HTMLElement>) => {
    if (!e.altKey || (e.key !== 'ArrowUp' && e.key !== 'ArrowDown')) return
    e.preventDefault()
    onMove(e.key === 'ArrowUp' ? -1 : 1)
  }

  return (
    <li
      aria-label={`Field ${number}`}
      onKeyDown={onKeyDown}
      className={`flex flex-col gap-3 rounded-xl border bg-card p-3 ${problems.length > 0 ? 'border-destructive' : ''}`}
      data-field-id={field.id}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-muted-foreground">{number}.</span>
        <span className="min-w-0 flex-1 truncate font-medium">
          {field.label.trim() || 'Untitled field'}
          <span className="font-normal text-muted-foreground">
            {' '}
            · {FIELD_TYPE_LABELS[field.type]}
          </span>
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={`Move field ${number} up`}
          disabled={index === 0}
          onClick={() => onMove(-1)}
        >
          <ArrowUp aria-hidden />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={`Move field ${number} down`}
          disabled={index === count - 1}
          onClick={() => onMove(1)}
        >
          <ArrowDown aria-hidden />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={open ? `Collapse field ${number}` : `Expand field ${number}`}
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          {open ? <ChevronUp aria-hidden /> : <ChevronDown aria-hidden />}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={`Remove field ${number}`}
          onClick={onRemove}
        >
          <Trash2 aria-hidden />
        </Button>
      </div>

      {problems.length > 0 && (
        <ul role="alert" className="text-sm text-destructive">
          {problems.map((p) => (
            <li key={p}>{p}</li>
          ))}
        </ul>
      )}

      {open && (
        <div className="flex flex-col gap-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <Labelled label="Type">
              {(id) => (
                <select
                  id={id}
                  className={selectClass}
                  value={field.type}
                  onChange={(e) => onChange({ type: e.target.value as FieldType })}
                >
                  {FIELD_TYPES.map((t) => (
                    <option key={t} value={t}>
                      {FIELD_TYPE_LABELS[t]}
                    </option>
                  ))}
                </select>
              )}
            </Labelled>
            <Labelled label="Label">
              {(id) => (
                <Input
                  id={id}
                  value={field.label}
                  maxLength={LIMITS.label}
                  autoComplete="off"
                  onChange={(e) => onChange({ label: e.target.value })}
                />
              )}
            </Labelled>
            <Labelled
              label="Key"
              hint="Identifies this field in answers and reports. Follows the label until you edit it."
            >
              {(id) => (
                <Input
                  id={id}
                  value={field.key}
                  maxLength={LIMITS.key}
                  autoComplete="off"
                  spellCheck={false}
                  onChange={(e) => onChange({ key: e.target.value })}
                />
              )}
            </Labelled>
            <Labelled label="Section" hint="Optional heading to group fields under.">
              {(id) => (
                <Input
                  id={id}
                  value={field.section}
                  autoComplete="off"
                  onChange={(e) => onChange({ section: e.target.value })}
                />
              )}
            </Labelled>
          </div>
          <Labelled label="Help text">
            {(id) => (
              <Input
                id={id}
                value={field.helpText}
                autoComplete="off"
                onChange={(e) => onChange({ helpText: e.target.value })}
              />
            )}
          </Labelled>

          {answerable && (
            <Check
              label="Required"
              checked={field.required}
              onChange={(required) => onChange({ required })}
            />
          )}

          {ranged && (
            <div className="grid gap-3 sm:grid-cols-3">
              <Labelled label="Minimum">
                {(id) => (
                  <Input
                    id={id}
                    inputMode="decimal"
                    value={field.min}
                    onChange={(e) => onChange({ min: e.target.value })}
                  />
                )}
              </Labelled>
              <Labelled label="Maximum">
                {(id) => (
                  <Input
                    id={id}
                    inputMode="decimal"
                    value={field.max}
                    onChange={(e) => onChange({ max: e.target.value })}
                  />
                )}
              </Labelled>
              <Labelled
                label="Unit"
                hint={field.type === 'Temperature' ? '°C when left empty.' : undefined}
              >
                {(id) => (
                  <Input
                    id={id}
                    value={field.unit}
                    maxLength={20}
                    autoComplete="off"
                    onChange={(e) => onChange({ unit: e.target.value })}
                  />
                )}
              </Labelled>
              <p className="text-xs text-muted-foreground sm:col-span-3">
                An answer outside the range counts as a failure.
              </p>
            </div>
          )}

          {field.type === 'YesNo' && (
            <Labelled
              label="Passing answer"
              hint="Any other answer counts as a failure. Choose Either to never fail."
            >
              {(id) => (
                <select
                  id={id}
                  className={selectClass}
                  value={field.expectedAnswer}
                  onChange={(e) => onChange({ expectedAnswer: e.target.value as ExpectedAnswer })}
                >
                  <option value="any">Either answer</option>
                  <option value="yes">Yes</option>
                  <option value="no">No</option>
                </select>
              )}
            </Labelled>
          )}

          {OPTION_TYPES.includes(field.type) && (
            <OptionsEditor field={field} fieldNumber={number} onReplace={onReplace} />
          )}

          {failable && (
            <fieldset className="flex flex-col gap-2">
              <legend className="mb-1 text-sm font-medium">When an answer fails</legend>
              <Check
                label="Require a photo"
                checked={field.photoRequiredOnFailure}
                onChange={(photoRequiredOnFailure) => onChange({ photoRequiredOnFailure })}
              />
              <Check
                label="Require a comment"
                checked={field.commentRequiredOnFailure}
                onChange={(commentRequiredOnFailure) => onChange({ commentRequiredOnFailure })}
              />
              <Check
                label="Raise an issue"
                checked={field.raiseIssueOnFailure}
                onChange={(raiseIssueOnFailure) => onChange({ raiseIssueOnFailure })}
              />
            </fieldset>
          )}
        </div>
      )}
    </li>
  )
}

function OptionsEditor({
  field,
  fieldNumber,
  onReplace,
}: {
  field: DraftField
  fieldNumber: number
  onReplace: (next: DraftField) => void
}) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-medium">Options</legend>
      <p className="text-xs text-muted-foreground">
        Between {LIMITS.minOptions} and {LIMITS.maxOptions}. Mark the options that count as a
        failure.
      </p>
      <ul className="flex flex-col gap-2">
        {field.options.map((option, i) => {
          const n = i + 1
          return (
            <li
              key={option.id}
              aria-label={`Option ${n}`}
              className="flex flex-wrap items-center gap-2 rounded-lg border p-2"
            >
              <Input
                aria-label={`Option ${n} label`}
                placeholder="Label"
                className="min-w-0 flex-1 basis-32"
                value={option.label}
                autoComplete="off"
                onChange={(e) =>
                  onReplace(updateOption(field, option.id, { label: e.target.value }))
                }
              />
              <Input
                aria-label={`Option ${n} value`}
                placeholder="Value"
                className="w-32"
                value={option.value}
                autoComplete="off"
                spellCheck={false}
                onChange={(e) =>
                  onReplace(updateOption(field, option.id, { value: e.target.value }))
                }
              />
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-5 accent-primary"
                  aria-label={`Option ${n} is a failure`}
                  checked={option.isFailure}
                  onChange={(e) =>
                    onReplace(updateOption(field, option.id, { isFailure: e.target.checked }))
                  }
                />
                Failure
              </label>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Move option ${n} of field ${fieldNumber} up`}
                disabled={i === 0}
                onClick={() => onReplace(moveOption(field, option.id, -1))}
              >
                <ArrowUp aria-hidden />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Move option ${n} of field ${fieldNumber} down`}
                disabled={i === field.options.length - 1}
                onClick={() => onReplace(moveOption(field, option.id, 1))}
              >
                <ArrowDown aria-hidden />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Remove option ${n} of field ${fieldNumber}`}
                onClick={() => onReplace(removeOption(field, option.id))}
              >
                <Trash2 aria-hidden />
              </Button>
            </li>
          )
        })}
      </ul>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="w-fit"
        disabled={field.options.length >= LIMITS.maxOptions}
        onClick={() => onReplace(addOption(field))}
      >
        <Plus aria-hidden />
        Add option
      </Button>
    </fieldset>
  )
}
