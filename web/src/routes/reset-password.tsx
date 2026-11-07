import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, Link } from '@tanstack/react-router'
import { CircleCheck, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { applyFieldErrors } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService } from '@/auth/context'
import { FormAlert, FormField } from '@/components/FormField'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { Button } from '@/components/ui/button'
import {
  PASSWORD_MIN,
  resetPasswordSchema,
  type ResetPasswordValues,
} from '@/features/auth/schemas'

export const Route = createFileRoute('/reset-password')({
  validateSearch: z.object({ token: z.string().optional() }),
  component: ResetPasswordPage,
})

function ResetPasswordPage() {
  const { token } = Route.useSearch()
  const { client } = useAuthService()
  const [done, setDone] = useState(false)
  const [formErrors, setFormErrors] = useState<string[]>([])
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })

  const signIn = (
    <Button asChild size="lg">
      <Link to="/login">Sign in</Link>
    </Button>
  )

  if (!token) {
    return (
      <AuthLayout title="Link not valid" description="This password reset link is incomplete.">
        <Button asChild size="lg">
          <Link to="/forgot-password">Request a new link</Link>
        </Button>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout title="Password updated">
        <div className="flex flex-col items-center gap-3 text-center">
          <CircleCheck className="size-10 text-success" aria-hidden />
          <p>Your password has been changed and you have been signed out everywhere.</p>
        </div>
        {signIn}
      </AuthLayout>
    )
  }

  const submit = handleSubmit(async (values) => {
    setFormErrors([])
    try {
      await unwrap(
        client.POST('/api/v1/auth/reset-password', {
          body: { token, newPassword: values.newPassword },
        }),
      )
      setDone(true)
    } catch (error) {
      setFormErrors(applyFieldErrors(error, ['newPassword'], setError))
    }
  })

  return (
    <AuthLayout title="Choose a new password">
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormAlert messages={formErrors} />
        <FormField
          label="New password"
          type="password"
          autoComplete="new-password"
          hint={`At least ${PASSWORD_MIN} characters.`}
          error={errors.newPassword}
          {...register('newPassword')}
        />
        <FormField
          label="Confirm new password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword}
          {...register('confirmPassword')}
        />
        <Button type="submit" size="lg" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="animate-spin" />}
          Update password
        </Button>
      </form>
    </AuthLayout>
  )
}
