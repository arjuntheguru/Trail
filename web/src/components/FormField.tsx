import { useId, type ComponentProps } from 'react'
import type { FieldError } from 'react-hook-form'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface FormFieldProps extends ComponentProps<typeof Input> {
  label: string
  error?: FieldError
  hint?: string
}

export function FormField({ label, error, hint, ...inputProps }: FormFieldProps) {
  const id = useId()
  const messageId = `${id}-message`
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? messageId : undefined}
        {...inputProps}
      />
      {error ? (
        <p id={messageId} role="alert" className="text-sm text-destructive">
          {error.message}
        </p>
      ) : hint ? (
        <p id={messageId} className="text-sm text-muted-foreground">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

export function FormAlert({ messages }: { messages: string[] }) {
  if (messages.length === 0) return null
  return (
    <div
      role="alert"
      className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {messages.map((m) => (
        <p key={m}>{m}</p>
      ))}
    </div>
  )
}
