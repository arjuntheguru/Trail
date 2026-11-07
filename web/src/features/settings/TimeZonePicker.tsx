import { useId, useMemo, useState } from 'react'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

import { supportedTimeZones } from './timeZones'

const MAX_SHOWN = 60

const normalize = (text: string) => text.toLowerCase().replace(/[_/\s]+/g, ' ')

/** A searchable list of IANA time zones. The list is always visible so it works the same on touch screens. */
export function TimeZonePicker({
  value,
  onChange,
  label = 'Time zone',
  error,
}: {
  value: string
  onChange: (zone: string) => void
  label?: string
  error?: string
}) {
  const id = useId()
  const [query, setQuery] = useState('')
  const zones = useMemo(() => supportedTimeZones(value), [value])
  const matches = useMemo(() => {
    const term = normalize(query.trim())
    return term ? zones.filter((z) => normalize(z).includes(term)) : zones
  }, [zones, query])
  const shown = matches.slice(0, MAX_SHOWN)

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={`${id}-search`}>{label}</Label>
      <p className="text-sm" data-testid="selected-time-zone">
        Selected: <span className="font-medium">{value || 'none'}</span>
      </p>
      <Input
        id={`${id}-search`}
        type="search"
        placeholder="Search, for example London"
        autoComplete="off"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        aria-invalid={error ? true : undefined}
      />
      <ul
        role="listbox"
        aria-label={`${label} options`}
        className="max-h-48 overflow-y-auto rounded-md border bg-card"
      >
        {shown.map((zone) => (
          <li key={zone} role="presentation">
            <button
              type="button"
              role="option"
              aria-selected={zone === value}
              onClick={() => onChange(zone)}
              className={cn(
                'flex min-h-11 w-full items-center px-3 text-left text-sm hover:bg-accent md:min-h-9',
                zone === value && 'bg-accent font-medium',
              )}
            >
              {zone}
            </button>
          </li>
        ))}
        {shown.length === 0 && (
          <li role="presentation" className="px-3 py-3 text-sm text-muted-foreground">
            No matching time zone.
          </li>
        )}
      </ul>
      {matches.length > shown.length && (
        <p className="text-xs text-muted-foreground">
          Showing {shown.length} of {matches.length}. Keep typing to narrow the list.
        </p>
      )}
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
