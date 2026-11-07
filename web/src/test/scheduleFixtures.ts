import { http, HttpResponse } from 'msw'

import type { components } from '@/api/schema'

import { paged, url } from './settingsFixtures'
import { server } from './server'

type Schedule = components['schemas']['ScheduleResponse']
type Template = components['schemas']['TemplateSummaryResponse']

export function schedule(overrides: Partial<Schedule> = {}): Schedule {
  return {
    id: crypto.randomUUID(),
    templateId: 'template-1',
    templateName: 'Opening checks',
    frequency: 'Daily',
    interval: 1,
    startDate: '2026-01-05',
    endDate: null,
    days: 'None',
    dayOfMonth: null,
    summary: 'Every day',
    dueTime: '11:00:00',
    availableFrom: null,
    allSites: false,
    siteIds: ['site-1'],
    regionIds: [],
    departmentId: null,
    isActive: true,
    ...overrides,
  }
}

export function templateSummary(overrides: Partial<Template> = {}): Template {
  return {
    id: 'template-1',
    name: 'Opening checks',
    kind: 'Checklist',
    tags: [],
    fieldCount: 3,
    version: 1,
    isActive: true,
    requiresApproval: false,
    requiresPhoto: false,
    libraryKey: null,
    ...overrides,
  }
}

/** Serves the templates picker; records the query strings of schedule list requests. */
export function serveSchedules(items: Schedule[], templates: Template[] = [templateSummary()]) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get(url('/schedules'), ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      return HttpResponse.json(paged(items))
    }),
    http.get(url('/templates'), ({ request }) => {
      const all = new URL(request.url).searchParams.get('IncludeInactive') === 'true'
      return HttpResponse.json(paged(templates.filter((t) => all || t.isActive)))
    }),
  )
  return queries
}
