import { createFileRoute, Link, useRouter } from '@tanstack/react-router'

import { redirectIfAuthenticated } from '@/auth/guard'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { RegisterForm } from '@/features/auth/RegisterForm'

export const Route = createFileRoute('/register')({
  beforeLoad: ({ context }) => redirectIfAuthenticated(context.auth),
  component: RegisterPage,
})

function RegisterPage() {
  const router = useRouter()
  return (
    <AuthLayout
      title="Create your account"
      description="Set up your business and start your first checklist."
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="text-primary hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <RegisterForm onRegistered={() => router.history.push('/today')} />
    </AuthLayout>
  )
}
