import { createFileRoute } from '@tanstack/react-router'

import { TemplateEditorPage } from '@/features/templates/TemplateEditorPage'

export const Route = createFileRoute('/_authenticated/settings/templates_/$templateId')({
  component: TemplateRoute,
})

function TemplateRoute() {
  const { templateId } = Route.useParams()
  return <TemplateEditorPage key={templateId} templateId={templateId} />
}
