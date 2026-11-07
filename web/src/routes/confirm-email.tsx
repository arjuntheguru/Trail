import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { CircleAlert, CircleCheck, Loader2 } from 'lucide-react'
import { z } from 'zod'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService } from '@/auth/context'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { Button } from '@/components/ui/button'

export const Route = createFileRoute('/confirm-email')({
  validateSearch: z.object({ token: z.string().optional() }),
  component: ConfirmEmailPage,
})

function ConfirmEmailPage() {
  const { token } = Route.useSearch()
  const { client, store } = useAuthService()

  // A query (not an effect) so StrictMode and re-renders cannot redeem the single-use token twice.
  const confirmation = useQuery({
    queryKey: ['confirm-email', token],
    enabled: !!token,
    retry: false,
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      await unwrap(client.POST('/api/v1/auth/confirm-email', { body: { token: token! } }))
      const state = store.getState()
      if (state.status === 'authenticated') {
        store.patchSession({ user: { ...state.session.user, emailConfirmed: true } })
      }
      return true
    },
  })

  if (!token || confirmation.isError) {
    return (
      <AuthLayout title="Could not confirm your email">
        <div className="flex flex-col items-center gap-3 text-center">
          <CircleAlert className="size-10 text-destructive" aria-hidden />
          <p>
            {token ? errorMessage(confirmation.error) : 'This confirmation link is incomplete.'}
          </p>
          <p className="text-sm text-muted-foreground">
            Sign in and use "Resend" in the banner at the top to get a new link.
          </p>
        </div>
        <Button asChild size="lg">
          <Link to="/today">Continue</Link>
        </Button>
      </AuthLayout>
    )
  }

  if (confirmation.isSuccess) {
    return (
      <AuthLayout title="Email confirmed">
        <div className="flex flex-col items-center gap-3 text-center">
          <CircleCheck className="size-10 text-success" aria-hidden />
          <p>Thanks, your email address is confirmed.</p>
        </div>
        <Button asChild size="lg">
          <Link to="/today">Continue</Link>
        </Button>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Confirming your email">
      <div className="flex items-center justify-center gap-2 py-4" role="status">
        <Loader2 className="animate-spin" aria-hidden /> Just a moment...
      </div>
    </AuthLayout>
  )
}
