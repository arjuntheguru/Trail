import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'

import { applyFieldErrors } from '@/api/problem'
import { useAuthService } from '@/auth/context'
import { FormAlert, FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { PASSWORD_MIN, registerSchema, type RegisterValues } from './schemas'

const FIELDS = ['organizationName', 'firstName', 'lastName', 'email', 'password'] as const

export function RegisterForm({ onRegistered }: { onRegistered: () => void }) {
  const auth = useAuthService()
  const [formErrors, setFormErrors] = useState<string[]>([])
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { organizationName: '', firstName: '', lastName: '', email: '', password: '' },
  })

  const submit = handleSubmit(async (values) => {
    setFormErrors([])
    try {
      await auth.register({ ...values, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone })
      onRegistered()
    } catch (error) {
      setFormErrors(applyFieldErrors(error, FIELDS, setError))
    }
  })

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <FormAlert messages={formErrors} />
      <FormField
        label="Business name"
        autoComplete="organization"
        error={errors.organizationName}
        {...register('organizationName')}
      />
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
        label="Work email"
        type="email"
        autoComplete="email"
        inputMode="email"
        error={errors.email}
        {...register('email')}
      />
      <FormField
        label="Password"
        type="password"
        autoComplete="new-password"
        hint={`At least ${PASSWORD_MIN} characters. A short phrase works well.`}
        error={errors.password}
        {...register('password')}
      />
      <Button type="submit" size="lg" disabled={isSubmitting}>
        {isSubmitting && <Loader2 className="animate-spin" />}
        {isSubmitting ? 'Creating account...' : 'Create account'}
      </Button>
    </form>
  )
}
