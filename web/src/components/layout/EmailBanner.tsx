import { useMutation } from '@tanstack/react-query'
import { MailWarning } from 'lucide-react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { Button } from '@/components/ui/button'

/** Shown until the signed-in user confirms their email address. */
export function EmailBanner() {
  const { user } = useSession()
  const { client } = useAuthService()
  const resend = useMutation({
    mutationFn: () => unwrap(client.POST('/api/v1/me/resend-email-confirmation')),
    onSuccess: () => toast.success(`Confirmation email sent to ${user.email}.`),
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (user.emailConfirmed) return null

  return (
    <div
      role="status"
      className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b bg-warning/15 px-4 py-3 text-sm"
    >
      <MailWarning className="size-5 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1">Please confirm your email address ({user.email}).</span>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={resend.isPending}
        onClick={() => resend.mutate()}
      >
        {resend.isPending ? 'Sending...' : 'Resend'}
      </Button>
    </div>
  )
}
