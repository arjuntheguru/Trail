import { useInfiniteQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Pencil, Plus, Power, PowerOff, Trash2 } from 'lucide-react'
import { useState } from 'react'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useDebounced } from '@/features/issues/useDebounced'

import { sitesListQuery, type Site, type SitesFilter } from './queries'
import {
  ConfirmDialog,
  EmptyState,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from './shared'

const selectClass = 'h-11 rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function SitesPage() {
  return (
    <RequirePermission permission={Permissions.Sites.View}>
      <SitesSection />
    </RequirePermission>
  )
}

type Action = { kind: 'activate' | 'deactivate' | 'delete'; site: Site }

function SitesSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Sites.Manage)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<SitesFilter['status']>('all')
  const debouncedSearch = useDebounced(search.trim(), 300)
  const sites = useInfiniteQuery(
    sitesListQuery(client, organizationId, { search: debouncedSearch, status }),
  )
  const [action, setAction] = useState<Action | null>(null)
  const items = sites.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Sites"
        description="The venues your team works at."
        action={
          canManage && (
            <Link to="/settings/sites/new" className={buttonVariants()}>
              <Plus aria-hidden />
              Add site
            </Link>
          )
        }
      />
      <div className="flex flex-wrap gap-2">
        <Input
          type="search"
          aria-label="Search sites"
          placeholder="Search by name or code"
          className="min-w-0 flex-1 basis-48"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          aria-label="Status"
          className={selectClass}
          value={status}
          onChange={(e) => setStatus(e.target.value as SitesFilter['status'])}
        >
          <option value="all">All sites</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </select>
      </div>
      {sites.isPending ? (
        <ListSkeleton />
      ) : sites.isError ? (
        <LoadError error={sites.error} retry={() => void sites.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState>No sites found.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((site) => (
            <li key={site.id} className="flex flex-col gap-2 rounded-xl border bg-card p-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="min-w-0 truncate font-medium">{site.name}</span>
                {site.code && <Badge variant="outline">{site.code}</Badge>}
                {!site.isActive && <Badge variant="secondary">Inactive</Badge>}
              </div>
              <p className="text-sm text-muted-foreground">
                {[site.regionName, site.timeZone].filter(Boolean).join(' · ')}
              </p>
              {canManage && (
                <div className="flex flex-wrap gap-2">
                  <Link
                    to="/settings/sites/$siteId"
                    params={{ siteId: site.id }}
                    aria-label={`Edit site ${site.name}`}
                    className={buttonVariants({ variant: 'outline', size: 'sm' })}
                  >
                    <Pencil aria-hidden />
                    Edit
                  </Link>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`${site.isActive ? 'Deactivate' : 'Activate'} site ${site.name}`}
                    onClick={() =>
                      setAction({ kind: site.isActive ? 'deactivate' : 'activate', site })
                    }
                  >
                    {site.isActive ? <PowerOff aria-hidden /> : <Power aria-hidden />}
                    {site.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Delete site ${site.name}`}
                    onClick={() => setAction({ kind: 'delete', site })}
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
      {sites.hasNextPage && (
        <Button
          variant="outline"
          disabled={sites.isFetchingNextPage}
          onClick={() => void sites.fetchNextPage()}
        >
          Load more
        </Button>
      )}
      {action && <SiteActionDialog action={action} onClose={() => setAction(null)} />}
    </section>
  )
}

function SiteActionDialog({ action, onClose }: { action: Action; onClose: () => void }) {
  const { client } = useAuthService()
  const { kind, site } = action
  const params = { params: { path: { id: site.id } } }
  const common = {
    areas: [['sites']] as const,
    onClose,
  }
  if (kind === 'delete') {
    return (
      <ConfirmDialog
        {...common}
        title="Delete site?"
        description={`"${site.name}" will be removed. Its history is kept, but nobody can work at it any more.`}
        confirmLabel="Delete site"
        successMessage="Site deleted."
        run={() => unwrap(client.DELETE('/api/v1/sites/{id}', params))}
      />
    )
  }
  if (kind === 'deactivate') {
    return (
      <ConfirmDialog
        {...common}
        title="Deactivate site?"
        description={`"${site.name}" stays visible but should receive no new work. You can activate it again at any time.`}
        confirmLabel="Deactivate site"
        destructive={false}
        successMessage="Site deactivated."
        run={() => unwrap(client.POST('/api/v1/sites/{id}/deactivate', params))}
      />
    )
  }
  return (
    <ConfirmDialog
      {...common}
      title="Activate site?"
      description={`"${site.name}" will receive work again.`}
      confirmLabel="Activate site"
      destructive={false}
      successMessage="Site activated."
      run={() => unwrap(client.POST('/api/v1/sites/{id}/activate', params))}
    />
  )
}
