import { useId } from 'react'

import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import type { Region, Site } from '@/features/settings/queries'

export interface TargetValue {
  allSites: boolean
  siteIds: string[]
  regionIds: string[]
}

function toggle(list: string[], id: string, on: boolean) {
  return on ? [...list.filter((x) => x !== id), id] : list.filter((x) => x !== id)
}

function CheckRow({
  label,
  detail,
  checked,
  onChange,
}: {
  label: string
  detail?: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  const id = useId()
  return (
    <div className="flex min-h-11 items-center gap-3 md:min-h-9">
      <Checkbox id={id} checked={checked} onCheckedChange={(c) => onChange(c === true)} />
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

/** Where a schedule runs: every site, or chosen regions and sites. */
export function TargetPicker({
  value,
  onChange,
  sites,
  regions,
}: {
  value: TargetValue
  onChange: (value: TargetValue) => void
  sites: readonly Site[]
  regions: readonly Region[]
}) {
  return (
    <div className="flex flex-col gap-4">
      <CheckRow
        label="All sites"
        checked={value.allSites}
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
                onChange={(on) => onChange({ ...value, siteIds: toggle(value.siteIds, s.id, on) })}
              />
            ))}
          </Group>
        </>
      )}
    </div>
  )
}
