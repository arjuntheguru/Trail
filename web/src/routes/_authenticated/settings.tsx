import { createFileRoute } from '@tanstack/react-router'

import { ComingSoon } from '@/components/ComingSoon'

export const Route = createFileRoute('/_authenticated/settings')({
  component: () => <ComingSoon title="Settings" />,
})
