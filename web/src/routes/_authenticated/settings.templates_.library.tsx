import { createFileRoute } from '@tanstack/react-router'

import { LibraryPage } from '@/features/templates/LibraryPage'

export const Route = createFileRoute('/_authenticated/settings/templates_/library')({
  component: LibraryPage,
})
