import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, Link, useRouter } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { applyFieldErrors } from '@/api/problem'
import { useAuthService } from '@/auth/context'
import { FormAlert, FormField } from '@/components/FormField'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { Button } from '@/components/ui/button'
import {
  acceptInvitationSchema,
  PASSWORD_MIN,
  type AcceptInvitationValues,
} from '@/features/auth/schemas'

export const Route = createFileRoute('/accept-invitation')({
  validateSearch: z.object({ token: z.string().optional() }),
  component: AcceptInvitationPage,
})

function AcceptInvitationPage() {
  const { token } = Route.useSearch()
  const auth = useAuthService()
  const router = useRouter()
  const [formErrors, setFormErrors] = useState<string[]>([])
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<AcceptInvitationValues>({
    resolver: zodResolver(acceptInvitationSchema),
    defaultValues: { firstName: '', lastName: '', password: '' },
  })

  if (!token) {
    return (
      <AuthLayout title="Invitation not valid" description="This invitation link is incomplete.">
        <Button asChild size="lg">
          <Link to="/login">Go to sign in</Link>
        </Button>
      </AuthLayout>
    )
  }

  const submit = handleSubmit(async (values) => {
    setFormErrors([])
    try {
      await auth.acceptInvitation({
        token,
        firstName: values.firstName || null,
        lastName: values.lastName || null,
        password: values.password || null,
      })
      router.history.push('/today')
    } catch (error) {
      setFormErrors(applyFieldErrors(error, ['firstName', 'lastName', 'password'], setError))
    }
  })

  return (
    <AuthLayout
      title="Join your team on Trail"
      description="Accept your invitation. If you are new to Trail, add your name and choose a password."
    >
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormAlert messages={formErrors} />
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="First name"
            autoComplete="given-name"
            error={errors.firstName}
            {...register('firstName')}
          />
          <FormField
            label="Last name"
            autoComplete="family-name"
            error={errors.lastName}
            {...register('lastName')}
          />
        </div>
        <FormField
          label="Password"
          type="password"
          autoComplete="new-password"
          hint={`Only needed for a new account. At least ${PASSWORD_MIN} characters.`}
          error={errors.password}
          {...register('password')}
        />
        <Button type="submit" size="lg" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="animate-spin" />}
          Accept invitation
        </Button>
      </form>
    </AuthLayout>
  )
}
