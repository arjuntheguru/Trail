import { Input } from '@/components/ui/input'

import {
  REPORT_RANGE_LABEL,
  REPORT_RANGES,
  type ReportRangeKey,
  type ReportsSearch,
  type ResolvedReportRange,
} from './search'

export const selectClass = 'h-12 w-full rounded-lg border border-input bg-card px-3 text-base'

/** Undefined when the caller may not list sites, so the picker is left out. */
export type SiteOption = { id: string; name: string }

export function SiteSelect({
  value,
  sites,
  onChange,
}: {
  value: string | undefined
  sites: SiteOption[]
  onChange: (siteId: string | undefined) => void
}) {
  return (
    <select
      aria-label="Site"
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value || undefined)}
      className={selectClass}
    >
      <option value="">All sites</option>
      {sites.map((s) => (
        <option key={s.id} value={s.id}>
          {s.name}
        </option>
      ))}
    </select>
  )
}

export function RangeSelect({
  range,
  onChange,
}: {
  range: ResolvedReportRange
  onChange: (next: Partial<ReportsSearch>) => void
}) {
  const setRange = (key: ReportRangeKey) =>
    onChange(
      key === 'custom'
        ? { range: 'custom', from: range.from, to: range.to }
        : { range: key === '30d' ? undefined : key, from: undefined, to: undefined },
    )
  return (
    <select
      aria-label="Date range"
      value={range.key}
      onChange={(e) => setRange(e.target.value as ReportRangeKey)}
      className={selectClass}
    >
      {REPORT_RANGES.map((r) => (
        <option key={r} value={r}>
          {REPORT_RANGE_LABEL[r]}
        </option>
      ))}
    </select>
  )
}

export function CustomDates({
  range,
  onChange,
}: {
  range: ResolvedReportRange
  onChange: (next: Partial<ReportsSearch>) => void
}) {
  if (range.key !== 'custom') return null
  return (
    <div className="grid grid-cols-2 gap-3 sm:max-w-md">
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        From
        <Input
          type="date"
          value={range.from}
          onChange={(e) => e.target.value && onChange({ from: e.target.value })}
          className="h-12"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        To
        <Input
          type="date"
          value={range.to}
          onChange={(e) => e.target.value && onChange({ to: e.target.value })}
          className="h-12"
        />
      </label>
    </div>
  )
}

export function ErrorBlock({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
      <p>{message}</p>
      <button type="button" className="h-10 rounded-lg border px-4 font-medium" onClick={onRetry}>
        Try again
      </button>
    </div>
  )
}
