import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'

import { ApiError, errorMessage } from '@/api/problem'
import { useSession } from '@/auth/context'

import { applyIssueUpdate, issueKey } from './queries'
import type { IssueDetail } from './types'

/** A 409 means the issue moved on under the person; 404 that it is gone. Either way the screen reloads. */
const shouldReload = (error: unknown) =>
  error instanceof ApiError && (error.status === 409 || error.status === 404)

/**
 * Runs one issue action. The response replaces the cached issue and marks lists and summaries stale; a
 * failure is kept for the caller to show, and a conflict reloads the issue so the screen shows the truth.
 */
export function useIssueAction<TInput = void>(
  issueId: string,
  run: (input: TInput) => Promise<IssueDetail>,
  onSuccess?: (updated: IssueDetail) => void,
) {
  const queryClient = useQueryClient()
  const organizationId = useSession().organization?.id
  const [error, setError] = useState<unknown>()
  const mutation = useMutation({
    mutationFn: run,
    onMutate: () => setError(undefined),
    onSuccess: (updated) => {
      applyIssueUpdate(queryClient, organizationId, updated)
      onSuccess?.(updated)
    },
    onError: (e) => {
      setError(e)
      if (shouldReload(e)) {
        // The form that raised the error may disappear with the fresh state, so say it here too.
        toast.error(errorMessage(e))
        void queryClient.invalidateQueries({ queryKey: issueKey(organizationId, issueId) })
      }
    },
  })
  return { mutation, error }
}

/**
 * Splits a failure into the message for one property (400 validation errors are keyed by the command's
 * property names) and everything else, which belongs in a banner.
 */
export function splitError(error: unknown, field?: string): { field?: string; general: string[] } {
  if (error === undefined) return { general: [] }
  if (error instanceof ApiError && Object.keys(error.errors).length > 0) {
    const result: { field?: string; general: string[] } = { general: [] }
    for (const [key, messages] of Object.entries(error.errors)) {
      const text = messages.join(' ')
      if (field && key.toLowerCase() === field.toLowerCase()) result.field = text
      else result.general.push(text)
    }
    return result
  }
  return { general: [errorMessage(error)] }
}
