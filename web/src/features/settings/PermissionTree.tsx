import { useId } from 'react'

import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'

import type { PermissionInfo } from './queries'

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1)
const groupLabel = (group: string) => capitalize(group.replace(/_/g, ' '))
const actionLabel = (key: string) =>
  capitalize(key.split('.').slice(1).join(' ').replace(/_/g, ' '))

function Row({
  label,
  detail,
  checked,
  disabled,
  onChange,
  strong,
}: {
  label: string
  detail?: string
  checked: boolean | 'indeterminate'
  disabled?: boolean
  onChange: (checked: boolean) => void
  strong?: boolean
}) {
  const id = useId()
  return (
    <div className="flex min-h-11 items-center gap-3 md:min-h-9">
      <Checkbox
        id={id}
        checked={checked}
        disabled={disabled}
        onCheckedChange={(c) => onChange(c === true)}
      />
      <Label
        htmlFor={id}
        className={strong ? 'text-base font-semibold md:text-sm' : 'text-base md:text-sm'}
      >
        {label}
        {detail && (
          <span className="font-mono text-xs font-normal text-muted-foreground">{detail}</span>
        )}
      </Label>
    </div>
  )
}

/**
 * Permissions grouped by area with a checkbox per area. `grantable` limits what can be switched on: the API
 * refuses permissions the caller does not hold, so those stay disabled.
 */
export function PermissionTree({
  catalog,
  value,
  onChange,
  grantable,
  disabled,
}: {
  catalog: readonly PermissionInfo[]
  value: readonly string[]
  onChange: (value: string[]) => void
  grantable: readonly string[]
  disabled?: boolean
}) {
  const groups = new Map<string, PermissionInfo[]>()
  for (const p of catalog) groups.set(p.group, [...(groups.get(p.group) ?? []), p])

  return (
    <div className="flex flex-col gap-3">
      {[...groups].map(([group, permissions]) => {
        const toggleable = permissions.filter((p) => grantable.includes(p.key))
        const selected = permissions.filter((p) => value.includes(p.key))
        const state: boolean | 'indeterminate' =
          selected.length === 0
            ? false
            : selected.length === permissions.length
              ? true
              : 'indeterminate'
        return (
          <fieldset key={group} className="rounded-lg border p-3">
            <legend className="sr-only">{groupLabel(group)}</legend>
            <Row
              strong
              label={groupLabel(group)}
              checked={state}
              disabled={disabled || toggleable.length === 0}
              onChange={(on) => {
                const keys = new Set(toggleable.map((p) => p.key))
                const rest = value.filter((v) => !keys.has(v))
                onChange(on ? [...rest, ...keys] : rest)
              }}
            />
            <div className="ml-8 flex flex-col">
              {permissions.map((p) => (
                <Row
                  key={p.key}
                  label={actionLabel(p.key)}
                  detail={` ${p.key}`}
                  checked={value.includes(p.key)}
                  disabled={disabled || !grantable.includes(p.key)}
                  onChange={(on) =>
                    onChange(
                      on
                        ? [...value.filter((v) => v !== p.key), p.key]
                        : value.filter((v) => v !== p.key),
                    )
                  }
                />
              ))}
            </div>
          </fieldset>
        )
      })}
    </div>
  )
}
