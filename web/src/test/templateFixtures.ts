import { http, HttpResponse } from 'msw'

import type { components } from '@/api/schema'

import { server } from './server'
import { paged, url } from './settingsFixtures'

type Template = components['schemas']['TemplateResponse']
type Summary = components['schemas']['TemplateSummaryResponse']
type LibrarySummary = components['schemas']['LibraryTemplateSummaryResponse']
type LibraryTemplate = components['schemas']['LibraryTemplateResponse']
type TemplateField = components['schemas']['TemplateField']

export function templateField(
  overrides: Partial<TemplateField> & Pick<TemplateField, 'key' | 'type'>,
): TemplateField {
  return {
    label: overrides.key,
    required: true,
    helpText: null,
    section: null,
    min: null,
    max: null,
    unit: null,
    expectedAnswer: null,
    options: [],
    photoRequiredOnFailure: false,
    commentRequiredOnFailure: true,
    raiseIssueOnFailure: true,
    ...overrides,
  }
}

export function template(overrides: Partial<Template> = {}): Template {
  return {
    id: 'template-1',
    name: 'Opening checks',
    description: null,
    kind: 'Checklist',
    fields: [templateField({ key: 'doors_unlocked', label: 'Doors unlocked', type: 'Checkbox' })],
    requiresApproval: false,
    requiresPhoto: false,
    version: 1,
    isActive: true,
    libraryKey: null,
    tagIds: [],
    ...overrides,
  }
}

export function summary(overrides: Partial<Summary> = {}): Summary {
  return {
    id: 'template-1',
    name: 'Opening checks',
    kind: 'Checklist',
    tags: [],
    fieldCount: 1,
    version: 1,
    isActive: true,
    requiresApproval: false,
    requiresPhoto: false,
    libraryKey: null,
    ...overrides,
  }
}

export const librarySummary = (overrides: Partial<LibrarySummary> = {}): LibrarySummary => ({
  key: 'fridge-temps',
  name: 'Fridge temperature log',
  category: 'Food safety',
  description: 'Record fridge temperatures through the day.',
  kind: 'RecordLog',
  fieldCount: 1,
  ...overrides,
})

export const libraryTemplate = (overrides: Partial<LibraryTemplate> = {}): LibraryTemplate => ({
  key: 'fridge-temps',
  name: 'Fridge temperature log',
  category: 'Food safety',
  description: 'Record fridge temperatures through the day.',
  kind: 'RecordLog',
  requiresApproval: false,
  requiresPhoto: false,
  fields: [
    templateField({
      key: 'temperature',
      label: 'Fridge temperature',
      type: 'Temperature',
      min: 0,
      max: 5,
    }),
  ],
  ...overrides,
})

/** Serves the templates list, applying the Kind / Search / IncludeInactive filters; records each query. */
export function serveTemplates(items: Summary[] = [summary()]) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get(url('/templates'), ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      const search = params.get('Search')?.toLowerCase()
      const kind = params.get('Kind')
      const includeInactive = params.get('IncludeInactive') === 'true'
      return HttpResponse.json(
        paged(
          items.filter(
            (t) =>
              (includeInactive || t.isActive) &&
              (!kind || t.kind === kind) &&
              (!search || t.name.toLowerCase().includes(search)),
          ),
        ),
      )
    }),
    http.get(url('/tags'), () => HttpResponse.json([])),
  )
  return queries
}
