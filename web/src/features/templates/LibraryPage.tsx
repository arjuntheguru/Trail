import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Eye, Loader2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import {
  EmptyState,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from '@/features/settings/shared'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'

import { Preview } from './Preview'
import { libraryQuery, libraryTemplateQuery } from './queries'
import { KIND_LABELS, type LibrarySummary } from './types'

export function LibraryPage() {
  return (
    <RequirePermission permission={Permissions.Templates.View}>
      <LibrarySection />
    </RequirePermission>
  )
}

function LibrarySection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Templates.Manage)
  const navigate = useNavigate()
  const library = useQuery(libraryQuery(client, organizationId))
  const [search, setSearch] = useState('')
  const [previewKey, setPreviewKey] = useState<string | null>(null)
  const [error, setError] = useState<string[]>([])

  const copy = useSettingsMutation({
    run: (key: string) =>
      unwrap(client.POST('/api/v1/templates/library/{key}/copy', { params: { path: { key } } })),
    areas: [['templates']],
    onSuccess: (template) => {
      toast.success('Template added. Adjust it to suit your business.')
      void navigate({ to: '/settings/templates/$templateId', params: { templateId: template.id } })
    },
  })
  const use = (key: string) => {
    setError([])
    copy.mutate(key, { onError: (e) => setError([errorMessage(e)]) })
  }

  const groups = useMemo(() => {
    const term = search.trim().toLowerCase()
    const matching = (library.data ?? []).filter(
      (t) =>
        !term ||
        t.name.toLowerCase().includes(term) ||
        t.category.toLowerCase().includes(term) ||
        t.description.toLowerCase().includes(term),
    )
    const byCategory = new Map<string, LibrarySummary[]>()
    for (const t of matching) byCategory.set(t.category, [...(byCategory.get(t.category) ?? []), t])
    return [...byCategory.entries()]
  }, [library.data, search])

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/settings/templates"
        className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Back to templates
      </Link>
      <SectionHeader
        title="Template library"
        description="Ready-made templates. Using one adds a copy to your organization that you can change freely."
      />
      <Input
        type="search"
        aria-label="Search library"
        placeholder="Search by name or category"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <FormAlert messages={error} />
      {library.isPending ? (
        <ListSkeleton />
      ) : library.isError ? (
        <LoadError error={library.error} retry={() => void library.refetch()} />
      ) : groups.length === 0 ? (
        <EmptyState>No library templates found.</EmptyState>
      ) : (
        groups.map(([category, templates]) => (
          <section key={category} aria-label={category} className="flex flex-col gap-2">
            <h3 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              {category}
            </h3>
            <ul className="flex flex-col gap-2">
              {templates.map((t) => (
                <li key={t.key} className="flex flex-col gap-2 rounded-xl border bg-card p-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="min-w-0 truncate font-medium">{t.name}</span>
                    <Badge variant="outline">{KIND_LABELS[t.kind]}</Badge>
                    <span className="text-sm text-muted-foreground">
                      {t.fieldCount} {t.fieldCount === 1 ? 'field' : 'fields'}
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground">{t.description}</p>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={`Preview ${t.name}`}
                      onClick={() => setPreviewKey(t.key)}
                    >
                      <Eye aria-hidden />
                      Preview
                    </Button>
                    {canManage && (
                      <Button
                        size="sm"
                        aria-label={`Use template ${t.name}`}
                        disabled={copy.isPending}
                        onClick={() => use(t.key)}
                      >
                        {copy.isPending && copy.variables === t.key && (
                          <Loader2 className="animate-spin" aria-hidden />
                        )}
                        Use this template
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
      {previewKey && (
        <LibraryPreview
          templateKey={previewKey}
          canUse={canManage}
          pending={copy.isPending}
          onUse={() => {
            use(previewKey)
            setPreviewKey(null)
          }}
          onClose={() => setPreviewKey(null)}
        />
      )}
    </section>
  )
}

function LibraryPreview({
  templateKey,
  canUse,
  pending,
  onUse,
  onClose,
}: {
  templateKey: string
  canUse: boolean
  pending: boolean
  onUse: () => void
  onClose: () => void
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const template = useQuery(libraryTemplateQuery(client, organizationId, templateKey))
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{template.data?.name ?? 'Template preview'}</DialogTitle>
          <DialogDescription>
            {template.data?.description ?? 'How this template looks when it is completed.'}
          </DialogDescription>
        </DialogHeader>
        {template.isPending ? (
          <ListSkeleton rows={3} />
        ) : template.isError ? (
          <LoadError error={template.error} retry={() => void template.refetch()} />
        ) : (
          <>
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{KIND_LABELS[template.data.kind]}</Badge>
              {template.data.requiresApproval && <Badge variant="outline">Needs approval</Badge>}
              {template.data.requiresPhoto && <Badge variant="outline">Photo required</Badge>}
            </div>
            <Preview fields={template.data.fields} />
          </>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
          {canUse && (
            <Button disabled={!template.data || pending} onClick={onUse}>
              Use this template
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
