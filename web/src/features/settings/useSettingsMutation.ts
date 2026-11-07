import { useMutation, useQueryClient } from '@tanstack/react-query'

import { useSession } from '@/auth/context'

import { invalidateArea } from './queries'

/** Runs a write and marks the named areas (e.g. `['tags']`) stale on success. */
export function useSettingsMutation<TVars = void, TData = unknown>({
  run,
  areas,
  onSuccess,
}: {
  run: (variables: TVars) => Promise<TData>
  areas: readonly (readonly string[])[]
  onSuccess?: (data: TData, variables: TVars) => void
}) {
  const queryClient = useQueryClient()
  const organizationId = useSession().organization?.id
  return useMutation({
    mutationFn: run,
    onSuccess: async (data, variables) => {
      await Promise.all(areas.map((area) => invalidateArea(queryClient, organizationId, ...area)))
      onSuccess?.(data, variables)
    },
  })
}
