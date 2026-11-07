import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { BookOpen, Pencil, Plus, Power, PowerOff, Trash2 } from 'lucide-react'
import { useState } from 'react'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useDebounced } from '@/features/issues/useDebounced'
import {
  ConfirmDialog,
  EmptyState,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from '@/features/settings/shared'
import { tagsQuery } from '@/features/settings/queries'

import { templatesListQuery, type TemplatesFilter } from './queries'
import { KIND_LABELS, KINDS, type TaskKind, type TemplateSummary } from './types'

const selectClass = 'h-11 rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function TemplatesPage() {
  return (
    <RequirePermission permission={Permissions.Templates.View}>
      <TemplatesSection />
    </RequirePermission>
  )
}

type Action = { kind: 'activate' | 'deactivate' | 'delete'; template: TemplateSummary }

function TemplatesSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Templates.Manage)
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState<TaskKind | ''>('')
  const [tagId, setTagId] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const filter: TemplatesFilter = {
    search: useDebounced(search.trim(), 300),
    kind,
    tagId,
    includeInactive,
  }
  const templates = useInfiniteQuery(templatesListQuery(client, organizationId, filter))
  const tags = useQuery(tagsQuery(client, organizationId))
  const [action, setAction] = useState<Action | null>(null)
  const items = templates.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Templates"
        description="Reusable checklists, forms and record logs that schedules turn into tasks."
        action={
          <div className="flex flex-wrap gap-2">
            <Link
              to="/settings/templates/library"
              className={buttonVariants({ variant: 'outline' })}
            >
              <BookOpen aria-hidden />
              Browse library
            </Link>
            {canManage && (
              <Link to="/settings/templates/new" className={buttonVariants()}>
                <Plus aria-hidden />
                New template
              </Link>
            )}
          </div>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Input
          type="search"
          aria-label="Search templates"
          placeholder="Search by name"
          className="min-w-0 flex-1 basis-48"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          aria-label="Kind"
          className={selectClass}
          value={kind}
          onChange={(e) => setKind(e.target.value as TaskKind | '')}
        >
          <option value="">All kinds</option>
          {KINDS.map((k) => (
            <option key={k} value={k}>
              {KIND_LABELS[k]}
            </option>
          ))}
        </select>
        <select
          aria-label="Tag"
          className={selectClass}
          value={tagId}
          onChange={(e) => setTagId(e.target.value)}
        >
          <option value="">All tags</option>
          {(tags.data ?? []).map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Status"
          className={selectClass}
          value={includeInactive ? 'all' : 'active'}
          onChange={(e) => setIncludeInactive(e.target.value === 'all')}
        >
          <option value="active">Active only</option>
          <option value="all">Include inactive</option>
        </select>
      </div>
      {templates.isPending ? (
        <ListSkeleton />
      ) : templates.isError ? (
        <LoadError error={templates.error} retry={() => void templates.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState>
          No templates found.{' '}
          {canManage && (
            <Link to="/settings/templates/library" className="underline">
              Start from the library.
            </Link>
          )}
        </EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((template) => (
            <li key={template.id} className="flex flex-col gap-2 rounded-xl border bg-card p-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="min-w-0 truncate font-medium">{template.name}</span>
                <Badge variant="outline">{KIND_LABELS[template.kind]}</Badge>
                {!template.isActive && <Badge variant="secondary">Inactive</Badge>}
                {template.requiresApproval && <Badge variant="outline">Needs approval</Badge>}
                {template.requiresPhoto && <Badge variant="outline">Photo required</Badge>}
              </div>
              <p className="text-sm text-muted-foreground">
                {template.fieldCount} {template.fieldCount === 1 ? 'field' : 'fields'} · Version{' '}
                {template.version}
              </p>
              {template.tags.length > 0 && (
                <ul aria-label="Tags" className="flex flex-wrap gap-1">
                  {template.tags.map((tag) => (
                    <li key={tag.id}>
                      <Badge variant="secondary">{tag.name}</Badge>
                    </li>
                  ))}
                </ul>
              )}
              {canManage && (
                <div className="flex flex-wrap gap-2">
                  <Link
                    to="/settings/templates/$templateId"
                    params={{ templateId: template.id }}
                    aria-label={`Edit template ${template.name}`}
                    className={buttonVariants({ variant: 'outline', size: 'sm' })}
                  >
                    <Pencil aria-hidden />
                    Edit
                  </Link>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`${template.isActive ? 'Deactivate' : 'Activate'} template ${template.name}`}
                    onClick={() =>
                      setAction({ kind: template.isActive ? 'deactivate' : 'activate', template })
                    }
                  >
                    {template.isActive ? <PowerOff aria-hidden /> : <Power aria-hidden />}
                    {template.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Delete template ${template.name}`}
                    onClick={() => setAction({ kind: 'delete', template })}
                  >
                    <Trash2 aria-hidden />
                    Delete
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {templates.hasNextPage && (
        <Button
          variant="outline"
          disabled={templates.isFetchingNextPage}
          onClick={() => void templates.fetchNextPage()}
        >
          Load more
        </Button>
      )}
      {action && <TemplateActionDialog action={action} onClose={() => setAction(null)} />}
    </section>
  )
}

function TemplateActionDialog({ action, onClose }: { action: Action; onClose: () => void }) {
  const { client } = useAuthService()
  const { kind, template } = action
  const params = { params: { path: { id: template.id } } }
  const common = { areas: [['templates']] as const, onClose }
  if (kind === 'delete') {
    return (
      <ConfirmDialog
        {...common}
        title="Delete template?"
        description={`"${template.name}" will be removed. Tasks already created from it keep their history. A template that a schedule still uses cannot be deleted.`}
        confirmLabel="Delete template"
        successMessage="Template deleted."
        run={() => unwrap(client.DELETE('/api/v1/templates/{id}', params))}
      />
    )
  }
  if (kind === 'deactivate') {
    return (
      <ConfirmDialog
        {...common}
        title="Deactivate template?"
        description={`"${template.name}" can no longer be scheduled and creates no new scheduled tasks. You can activate it again at any time.`}
        confirmLabel="Deactivate template"
        destructive={false}
        successMessage="Template deactivated."
        run={() => unwrap(client.POST('/api/v1/templates/{id}/deactivate', params))}
      />
    )
  }
  return (
    <ConfirmDialog
      {...common}
      title="Activate template?"
      description={`"${template.name}" can be scheduled again.`}
      confirmLabel="Activate template"
      destructive={false}
      successMessage="Template activated."
      run={() => unwrap(client.POST('/api/v1/templates/{id}/activate', params))}
    />
  )
}
