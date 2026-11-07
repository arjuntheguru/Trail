import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, Link } from '@tanstack/react-router'
import { Loader2, MailCheck } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'

import { applyFieldErrors } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService } from '@/auth/context'
import { redirectIfAuthenticated } from '@/auth/guard'
import { FormAlert, FormField } from '@/components/FormField'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { Button } from '@/components/ui/button'
import { forgotPasswordSchema, type ForgotPasswordValues } from '@/features/auth/schemas'

export const Route = createFileRoute('/forgot-password')({
  beforeLoad: ({ context }) => redirectIfAuthenticated(context.auth),
  component: ForgotPasswordPage,
})

function ForgotPasswordPage() {
  const { client } = useAuthService()
  const [sentTo, setSentTo] = useState<string | null>(null)
  const [formErrors, setFormErrors] = useState<string[]>([])
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  })

  const submit = handleSubmit(async (values) => {
    setFormErrors([])
    try {
      await unwrap(client.POST('/api/v1/auth/forgot-password', { body: values }))
      setSentTo(values.email)
    } catch (error) {
      setFormErrors(applyFieldErrors(error, ['email'], setError))
    }
  })

  const footer = (
    <Link to="/login" className="text-primary hover:underline">
      Back to sign in
    </Link>
  )

  if (sentTo) {
    return (
      <AuthLayout title="Check your email" footer={footer}>
        <div className="flex flex-col items-center gap-3 text-center">
          <MailCheck className="size-10 text-primary" aria-hidden />
          <p>
            If an account exists for <strong>{sentTo}</strong>, we have sent a link to reset your
            password. It expires in one hour.
          </p>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout
      title="Reset your password"
      description="Enter your email and we will send you a reset link."
      footer={footer}
    >
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormAlert messages={formErrors} />
        <FormField
          label="Email"
          type="email"
          autoComplete="email"
          inputMode="email"
          error={errors.email}
          {...register('email')}
        />
        <Button type="submit" size="lg" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="animate-spin" />}
          Send reset link
        </Button>
      </form>
    </AuthLayout>
  )
}
