import { createFileRoute } from '@tanstack/react-router'

import { TagsPage } from '@/features/settings/TagsPage'

export const Route = createFileRoute('/_authenticated/settings/tags')({
  component: TagsPage,
})
