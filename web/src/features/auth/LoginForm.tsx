import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'

import { applyFieldErrors } from '@/api/problem'
import { useAuthService } from '@/auth/context'
import { FormAlert, FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { loginSchema, type LoginValues } from './schemas'

export function LoginForm({ onSignedIn }: { onSignedIn: () => void }) {
  const auth = useAuthService()
  const [formErrors, setFormErrors] = useState<string[]>([])
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const submit = handleSubmit(async (values) => {
    setFormErrors([])
    try {
      await auth.login(values)
      onSignedIn()
    } catch (error) {
      setFormErrors(applyFieldErrors(error, ['email', 'password'], setError))
    }
  })

  return (
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
      <FormField
        label="Password"
        type="password"
        autoComplete="current-password"
        error={errors.password}
        {...register('password')}
      />
      <Button type="submit" size="lg" disabled={isSubmitting}>
        {isSubmitting && <Loader2 className="animate-spin" />}
        {isSubmitting ? 'Signing in...' : 'Sign in'}
      </Button>
      <Link to="/forgot-password" className="text-center text-sm text-primary hover:underline">
        Forgot your password?
      </Link>
    </form>
  )
}
