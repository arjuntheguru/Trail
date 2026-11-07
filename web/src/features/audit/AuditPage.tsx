import { useMutation, useQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { Download, Loader2 } from 'lucide-react'
import { useState } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { selectClass } from '@/features/reports/Filters'
import {
  EmptyState,
  Hint,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from '@/features/settings/shared'
import { sitesQuery } from '@/features/today/queries'
import { formatDateTime } from '@/lib/dates'

import { changeLines, fieldLabel } from './changes'
import { downloadAuditCsv } from './exportAudit'
import { actorsQuery, auditQuery, type AuditEntry } from './queries'
import { AUDIT_ACTIONS, type AuditSearch } from './search'

const route = getRouteApi('/_authenticated/settings/audit')

export function AuditPage() {
  return (
    <RequirePermission permission={Permissions.Audit.View}>
      <AuditSection />
    </RequirePermission>
  )
}

function AuditSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const entries = useQuery(auditQuery(client, organizationId, search))
  const canSeeSites = usePermission(Permissions.Sites.View)
  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const canSeeMembers = usePermission(Permissions.Members.View)
  const actors = useQuery({ ...actorsQuery(client, organizationId), enabled: canSeeMembers })
  const canExport = usePermission(Permissions.Reports.Export)

  const apply = (next: AuditSearch) => void navigate({ search: { ...next, page: undefined } })
  const siteName = new Map((sites.data?.items ?? []).map((s) => [s.id, s.name]))

  return (
    <section className="flex flex-col gap-4" aria-label="Audit log">
      <SectionHeader
        title="Audit log"
        description="Who changed what, newest first."
        action={canExport && <ExportButton search={search} />}
      />
      <FilterForm
        key={JSON.stringify(search)}
        search={search}
        sites={canSeeSites ? (sites.data?.items ?? []) : undefined}
        actors={canSeeMembers ? (actors.data?.items ?? []) : undefined}
        onApply={apply}
        onClear={() => void navigate({ search: {} })}
      />
      {entries.isPending ? (
        <ListSkeleton rows={6} />
      ) : entries.isError ? (
        <LoadError error={entries.error} retry={() => void entries.refetch()} />
      ) : entries.data.items.length === 0 ? (
        <EmptyState>No changes match these filters.</EmptyState>
      ) : (
        <>
          <div className="overflow-x-auto rounded-xl border bg-card">
            <table className="w-full min-w-[40rem] text-left text-sm">
              <thead className="border-b text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 font-medium">When</th>
                  <th className="px-3 py-2 font-medium">Who</th>
                  <th className="px-3 py-2 font-medium">What</th>
                  <th className="px-3 py-2 font-medium">Changes</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {entries.data.items.map((entry) => (
                  <EntryRow
                    key={entry.id}
                    entry={entry}
                    siteName={entry.siteId ? siteName.get(entry.siteId) : undefined}
                  />
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between gap-2">
            <Button
              variant="outline"
              disabled={entries.data.page <= 1}
              onClick={() =>
                void navigate({
                  search: (prev) => ({
                    ...prev,
                    page: entries.data.page - 1 > 1 ? entries.data.page - 1 : undefined,
                  }),
                })
              }
            >
              Previous
            </Button>
            <span className="text-sm text-muted-foreground">
              Page {entries.data.page} of {Math.max(entries.data.totalPages ?? 1, 1)} ·{' '}
              {entries.data.totalCount} entries
            </span>
            <Button
              variant="outline"
              disabled={!entries.data.hasNextPage}
              onClick={() =>
                void navigate({ search: (prev) => ({ ...prev, page: entries.data.page + 1 }) })
              }
            >
              Next
            </Button>
          </div>
        </>
      )}
    </section>
  )
}

function FilterForm({
  search,
  sites,
  actors,
  onApply,
  onClear,
}: {
  search: AuditSearch
  sites: { id: string; name: string }[] | undefined
  actors: { userId: string; firstName: string; lastName: string }[] | undefined
  onApply: (next: AuditSearch) => void
  onClear: () => void
}) {
  const [draft, setDraft] = useState<AuditSearch>(search)
  const set = (patch: Partial<AuditSearch>) => setDraft((d) => ({ ...d, ...patch }))
  const rangeError = draft.from && draft.to && draft.to < draft.from
  return (
    <form
      aria-label="Audit filters"
      className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3"
      onSubmit={(e) => {
        e.preventDefault()
        if (rangeError) return
        onApply({ ...draft, entityType: draft.entityType?.trim() || undefined })
      }}
    >
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        Entity type
        <Input
          value={draft.entityType ?? ''}
          placeholder="e.g. Site"
          className="h-12"
          onChange={(e) => set({ entityType: e.target.value || undefined })}
        />
      </label>
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        Action
        <select
          className={selectClass}
          value={draft.action ?? ''}
          onChange={(e) => set({ action: (e.target.value || undefined) as AuditSearch['action'] })}
        >
          <option value="">Any action</option>
          {AUDIT_ACTIONS.map((a) => (
            <option key={a} value={a}>
              {a}
            </option>
          ))}
        </select>
      </label>
      {actors && (
        <label className="flex flex-col gap-1 text-sm text-muted-foreground">
          Actor
          <select
            className={selectClass}
            value={draft.actorId ?? ''}
            onChange={(e) => set({ actorId: e.target.value || undefined })}
          >
            <option value="">Anyone</option>
            {actors.map((a) => (
              <option key={a.userId} value={a.userId}>
                {a.firstName} {a.lastName}
              </option>
            ))}
          </select>
        </label>
      )}
      {sites && (
        <label className="flex flex-col gap-1 text-sm text-muted-foreground">
          Site
          <select
            className={selectClass}
            value={draft.siteId ?? ''}
            onChange={(e) => set({ siteId: e.target.value || undefined })}
          >
            <option value="">All sites</option>
            {sites.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
      )}
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        From
        <Input
          type="date"
          className="h-12"
          value={draft.from ?? ''}
          onChange={(e) => set({ from: e.target.value || undefined })}
        />
      </label>
      <label className="flex flex-col gap-1 text-sm text-muted-foreground">
        To
        <Input
          type="date"
          className="h-12"
          value={draft.to ?? ''}
          onChange={(e) => set({ to: e.target.value || undefined })}
        />
      </label>
      {rangeError && (
        <p role="alert" className="text-sm text-destructive sm:col-span-2 lg:col-span-3">
          The end date must not be before the start date.
        </p>
      )}
      <div className="flex gap-2 sm:col-span-2 lg:col-span-3">
        <Button type="submit" size="lg" disabled={!!rangeError}>
          Apply filters
        </Button>
        <Button type="button" variant="outline" size="lg" onClick={onClear}>
          Clear
        </Button>
      </div>
    </form>
  )
}

const ACTION_VARIANT = {
  Created: 'default',
  Updated: 'secondary',
  Deleted: 'destructive',
} as const

function EntryRow({ entry, siteName }: { entry: AuditEntry; siteName?: string }) {
  const lines = changeLines(entry.changes as Record<string, { old: unknown; new: unknown }>)
  return (
    <tr className="align-top" data-testid="audit-entry">
      <td className="px-3 py-2 whitespace-nowrap">{formatDateTime(entry.occurredAt)}</td>
      <td className="px-3 py-2">{entry.actorName ?? 'System'}</td>
      <td className="px-3 py-2">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={ACTION_VARIANT[entry.action]}>{entry.action}</Badge>
          <span className="font-medium">{entry.entityType}</span>
        </div>
        {siteName && <div className="text-muted-foreground">{siteName}</div>}
        <div className="font-mono text-xs text-muted-foreground" title={entry.entityId}>
          {entry.entityId.slice(0, 8)}
        </div>
      </td>
      <td className="px-3 py-2">
        {lines.length === 0 ? (
          <span className="text-muted-foreground">No field changes</span>
        ) : (
          <ul className="flex flex-col gap-0.5" aria-label="Changes">
            {lines.map((line) => (
              <li key={line.field}>
                <span className="font-medium">{fieldLabel(line.field)}:</span>{' '}
                {entry.action === 'Created' ? (
                  <Value text={line.next} />
                ) : entry.action === 'Deleted' ? (
                  <Value text={line.old} />
                ) : (
                  <>
                    <Value text={line.old} /> <span aria-label="changed to">→</span>{' '}
                    <Value text={line.next} />
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </td>
    </tr>
  )
}

function Value({ text }: { text: string | null }) {
  return text === null ? (
    <span className="text-muted-foreground">—</span>
  ) : (
    <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs break-all">{text}</code>
  )
}

function ExportButton({ search }: { search: AuditSearch }) {
  const { client } = useAuthService()
  const mutation = useMutation({ mutationFn: () => downloadAuditCsv(client, search) })
  const half = !!search.from !== !!search.to
  return (
    <div className="flex flex-col items-end gap-1">
      <Button
        variant="outline"
        disabled={mutation.isPending || half}
        onClick={() => mutation.mutate()}
      >
        {mutation.isPending ? (
          <Loader2 className="animate-spin" aria-hidden />
        ) : (
          <Download aria-hidden />
        )}
        Export CSV
      </Button>
      <div aria-live="polite">
        {half ? (
          <Hint>Set both dates, or neither, to export.</Hint>
        ) : mutation.isError ? (
          <p role="alert" className="max-w-xs text-right text-sm text-destructive">
            {errorMessage(mutation.error)}
          </p>
        ) : mutation.isSuccess ? (
          <Hint>Saved {mutation.data}.</Hint>
        ) : !search.from ? (
          <Hint>Without dates the last 30 days are exported.</Hint>
        ) : null}
      </div>
    </div>
  )
}
