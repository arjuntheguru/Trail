import { createFileRoute, Link, useRouter } from '@tanstack/react-router'
import { z } from 'zod'

import { redirectIfAuthenticated, safeRedirect } from '@/auth/guard'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { LoginForm } from '@/features/auth/LoginForm'

export const Route = createFileRoute('/login')({
  validateSearch: z.object({ redirect: z.string().optional() }),
  beforeLoad: ({ context }) => redirectIfAuthenticated(context.auth),
  component: LoginPage,
})

function LoginPage() {
  const { redirect } = Route.useSearch()
  const router = useRouter()
  return (
    <AuthLayout
      title="Sign in"
      description="Welcome back. Sign in to see today's tasks."
      footer={
        <>
          New to Trail?{' '}
          <Link to="/register" className="text-primary hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      <LoginForm onSignedIn={() => router.history.push(safeRedirect(redirect))} />
    </AuthLayout>
  )
}
