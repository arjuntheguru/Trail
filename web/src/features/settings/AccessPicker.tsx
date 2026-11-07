import { useId } from 'react'

import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'

import type { Department, Region, Site } from './queries'

export interface AccessValue {
  allSites: boolean
  siteIds: string[]
  regionIds: string[]
  departmentIds: string[]
}

function toggle(list: string[], id: string, on: boolean) {
  return on ? [...list.filter((x) => x !== id), id] : list.filter((x) => x !== id)
}

function CheckRow({
  label,
  checked,
  disabled,
  onChange,
  detail,
}: {
  label: string
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
  detail?: string
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
      <Label htmlFor={id} className="flex-1 text-base leading-snug md:text-sm">
        {label}
        {detail && <span className="font-normal text-muted-foreground">{detail}</span>}
      </Label>
    </div>
  )
}

function Group({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset className="flex flex-col gap-1">
      <legend className="mb-1 text-sm font-medium">{title}</legend>
      <div className="max-h-60 overflow-y-auto rounded-md border p-2">{children}</div>
    </fieldset>
  )
}

/** Checkbox lists for what a member can reach. The caller supplies the options the API returned. */
export function AccessPicker({
  value,
  onChange,
  sites,
  regions,
  departments,
  disabled,
}: {
  value: AccessValue
  onChange: (value: AccessValue) => void
  sites: readonly Site[]
  regions: readonly Region[]
  /** Omit to leave departments out (the invitation has no department field). */
  departments?: readonly Department[]
  disabled?: boolean
}) {
  return (
    <div className="flex flex-col gap-4">
      <CheckRow
        label="Access to all sites"
        checked={value.allSites}
        disabled={disabled}
        onChange={(allSites) => onChange({ ...value, allSites })}
      />
      {value.allSites ? (
        <p className="text-sm text-muted-foreground">
          This includes sites added later. Turn it off to choose regions or individual sites.
        </p>
      ) : (
        <>
          <Group title="Regions (every site in the region)">
            {regions.length === 0 && <p className="text-sm text-muted-foreground">No regions.</p>}
            {regions.map((r) => (
              <CheckRow
                key={r.id}
                label={r.name}
                checked={value.regionIds.includes(r.id)}
                disabled={disabled}
                onChange={(on) =>
                  onChange({ ...value, regionIds: toggle(value.regionIds, r.id, on) })
                }
              />
            ))}
          </Group>
          <Group title="Individual sites">
            {sites.length === 0 && <p className="text-sm text-muted-foreground">No sites.</p>}
            {sites.map((s) => (
              <CheckRow
                key={s.id}
                label={s.name}
                detail={s.isActive ? undefined : ' (inactive)'}
                checked={value.siteIds.includes(s.id)}
                disabled={disabled}
                onChange={(on) => onChange({ ...value, siteIds: toggle(value.siteIds, s.id, on) })}
              />
            ))}
          </Group>
        </>
      )}
      {departments && (
        <Group title="Departments">
          {departments.length === 0 && (
            <p className="text-sm text-muted-foreground">No departments.</p>
          )}
          {departments.map((d) => (
            <CheckRow
              key={d.id}
              label={d.name}
              checked={value.departmentIds.includes(d.id)}
              disabled={disabled}
              onChange={(on) =>
                onChange({ ...value, departmentIds: toggle(value.departmentIds, d.id, on) })
              }
            />
          ))}
        </Group>
      )}
    </div>
  )
}
