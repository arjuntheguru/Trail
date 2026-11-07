import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { AccessPicker, type AccessValue } from './AccessPicker'
import { memberAccessQuery, type MemberAccess } from './queries'
import { Hint, ListSkeleton, LoadError } from './shared'
import { useSettingsMutation } from './useSettingsMutation'
import { useAccessOptions } from './useAccessOptions'

/** What a member can reach: all sites, or chosen regions and sites, plus their departments. */
export function AccessEditor({
  membershipId,
  readOnlyReason,
}: {
  membershipId: string
  /** When set the editor is shown disabled and this explains why. */
  readOnlyReason?: string
}) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const access = useQuery(memberAccessQuery(client, organizationId, membershipId))
  const options = useAccessOptions()

  return (
    <section aria-labelledby="access-heading" className="flex flex-col gap-3">
      <h3 id="access-heading" className="text-lg font-semibold">
        Site access
      </h3>
      {access.isPending || options.isPending ? (
        <ListSkeleton rows={3} />
      ) : access.isError ? (
        <LoadError error={access.error} retry={() => void access.refetch()} />
      ) : options.error ? (
        <LoadError error={options.error} retry={options.refetch} />
      ) : !options.available ? (
        <Hint>You need permission to view sites to see or change this member's access.</Hint>
      ) : (
        <AccessForm
          // A fresh copy of the server state resets the draft after a save or a reload.
          key={JSON.stringify(access.data)}
          membershipId={membershipId}
          saved={access.data}
          options={options}
          readOnlyReason={readOnlyReason}
        />
      )}
    </section>
  )
}

function AccessForm({
  membershipId,
  saved,
  options,
  readOnlyReason,
}: {
  membershipId: string
  saved: MemberAccess
  options: ReturnType<typeof useAccessOptions>
  readOnlyReason?: string
}) {
  const { client } = useAuthService()
  const [draft, setDraft] = useState<AccessValue>({
    allSites: saved.allSites,
    siteIds: saved.siteIds,
    regionIds: saved.regionIds,
    departmentIds: saved.departmentIds,
  })
  const mutation = useSettingsMutation({
    run: (value: AccessValue) =>
      unwrap(
        client.PUT('/api/v1/members/{membershipId}/access', {
          params: { path: { membershipId } },
          body: value,
        }),
      ),
    areas: [['members']],
    onSuccess: () => toast.success('Access saved.'),
  })
  const readOnly = !!readOnlyReason

  return (
    <div className="flex flex-col gap-3">
      <AccessPicker
        value={draft}
        onChange={setDraft}
        sites={options.sites}
        regions={options.regions}
        departments={options.departments}
        disabled={readOnly}
      />
      {readOnly ? (
        <Hint>{readOnlyReason}</Hint>
      ) : (
        <>
          {mutation.isError && <FormAlert messages={[errorMessage(mutation.error)]} />}
          <div>
            <Button disabled={mutation.isPending} onClick={() => mutation.mutate(draft)}>
              {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
              Save access
            </Button>
          </div>
        </>
      )}
    </div>
  )
}
