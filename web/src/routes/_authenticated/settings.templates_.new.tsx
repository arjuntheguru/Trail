import { createFileRoute } from '@tanstack/react-router'

import { TemplateEditorPage } from '@/features/templates/TemplateEditorPage'

export const Route = createFileRoute('/_authenticated/settings/templates_/new')({
  component: () => <TemplateEditorPage />,
})
