import { useMutation } from '@tanstack/react-query'
import { Download, Loader2 } from 'lucide-react'

import { errorMessage } from '@/api/problem'
import { useAuthService, usePermission } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'

import { downloadExport, type ExportKind, type ExportParams } from './download'

const EXPORTS: { kind: ExportKind; label: string; needsAudit?: boolean }[] = [
  { kind: 'tasks', label: 'Tasks CSV' },
  { kind: 'issues', label: 'Issues CSV' },
  { kind: 'audit', label: 'Audit trail CSV', needsAudit: true },
]

/** CSV downloads for the page's date range and site. Hidden entirely without the export permission. */
export function ExportPanel({ params, disabled }: { params: ExportParams; disabled?: boolean }) {
  const { client } = useAuthService()
  const canExport = usePermission(Permissions.Reports.Export)
  const canAudit = usePermission(Permissions.Audit.View)
  const mutation = useMutation({
    mutationFn: (kind: ExportKind) => downloadExport(client, kind, params),
  })

  if (!canExport) return null

  const busy = mutation.isPending ? mutation.variables : undefined
  return (
    <section aria-labelledby="exports" className="flex flex-col gap-3">
      <div>
        <h2 id="exports" className="text-lg font-semibold">
          Export
        </h2>
        <p className="text-sm text-muted-foreground">
          Download the rows for the date range and site chosen above.
        </p>
      </div>
      <div className="flex flex-wrap gap-2">
        {EXPORTS.filter((e) => !e.needsAudit || canAudit).map((e) => (
          <Button
            key={e.kind}
            type="button"
            variant="outline"
            size="lg"
            disabled={disabled || mutation.isPending}
            onClick={() => mutation.mutate(e.kind)}
          >
            {busy === e.kind ? (
              <Loader2 className="animate-spin" aria-hidden />
            ) : (
              <Download aria-hidden />
            )}
            {busy === e.kind ? 'Preparing…' : e.label}
          </Button>
        ))}
      </div>
      <div aria-live="polite">
        {mutation.isError && (
          <p role="alert" className="text-destructive">
            {errorMessage(mutation.error)}
          </p>
        )}
        {mutation.isSuccess && (
          <p className="text-sm text-muted-foreground">Saved {mutation.data}.</p>
        )}
      </div>
    </section>
  )
}
