import { createFileRoute, redirect } from '@tanstack/react-router'

// Everyone may see the organization, so /settings always has somewhere to land.
export const Route = createFileRoute('/_authenticated/settings/')({
  beforeLoad: () => {
    throw redirect({ to: '/settings/organization', replace: true })
  },
})
