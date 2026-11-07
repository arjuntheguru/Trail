import { useInfiniteQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { UserPlus } from 'lucide-react'
import { useState } from 'react'

import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useDebounced } from '@/features/issues/useDebounced'

import { InviteDialog } from './InviteDialog'
import { useIsSelf } from './memberRules'
import { membersListQuery, type MembersFilter } from './queries'
import {
  EmptyState,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
  StatusBadge,
} from './shared'

const selectClass = 'h-11 rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function MembersPage() {
  return (
    <RequirePermission permission={Permissions.Members.View}>
      <MembersSection />
    </RequirePermission>
  )
}

function MembersSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Members.Manage)
  const isSelf = useIsSelf()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<MembersFilter['status']>('all')
  const debouncedSearch = useDebounced(search.trim(), 300)
  const members = useInfiniteQuery(
    membersListQuery(client, organizationId, { search: debouncedSearch, status }),
  )
  const [inviting, setInviting] = useState(false)
  const items = members.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Members"
        description="People who can sign in to this organization."
        action={
          canManage && (
            <Button onClick={() => setInviting(true)}>
              <UserPlus aria-hidden />
              Invite member
            </Button>
          )
        }
      />
      <div className="flex flex-wrap gap-2">
        <Input
          type="search"
          aria-label="Search members"
          placeholder="Search by name or email"
          className="min-w-0 flex-1 basis-48"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          aria-label="Status"
          className={selectClass}
          value={status}
          onChange={(e) => setStatus(e.target.value as MembersFilter['status'])}
        >
          <option value="all">All statuses</option>
          <option value="Active">Active</option>
          <option value="Invited">Invited</option>
          <option value="Suspended">Suspended</option>
        </select>
      </div>
      {members.isPending ? (
        <ListSkeleton />
      ) : members.isError ? (
        <LoadError error={members.error} retry={() => void members.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState>No members found.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((m) => (
            <li key={m.id}>
              <Link
                to="/settings/members/$membershipId"
                params={{ membershipId: m.id }}
                className="flex flex-col gap-1 rounded-xl border bg-card p-3 hover:bg-accent/50"
              >
                <span className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">
                    {`${m.firstName} ${m.lastName}`.trim() || m.email}
                  </span>
                  {isSelf(m) && <Badge variant="outline">You</Badge>}
                  <StatusBadge status={m.status} />
                </span>
                <span className="truncate text-sm text-muted-foreground">
                  {m.email} · {m.roleName}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {members.hasNextPage && (
        <Button
          variant="outline"
          disabled={members.isFetchingNextPage}
          onClick={() => void members.fetchNextPage()}
        >
          Load more
        </Button>
      )}
      {inviting && <InviteDialog onClose={() => setInviting(false)} />}
    </section>
  )
}
