import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { auditEntry } from '@/test/integrationFixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { member, paged, signIn, site, url } from '@/test/settingsFixtures'

import { changeLines, fieldLabel, formatValue } from './changes'
import { auditApiQuery } from './search'

afterEach(() => vi.restoreAllMocks())

const HARBOUR = site({ id: 'site-1', name: 'Harbour Cafe' })
const SAM = member({ userId: 'user-sam', firstName: 'Sam', lastName: 'Chef' })

/** Serves the list, records each query string and the request origin of the page param. */
function serveAudit(respond: (params: URLSearchParams) => object) {
  const requests: URLSearchParams[] = []
  server.use(
    http.get(url('/audit'), ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      return HttpResponse.json(respond(params))
    }),
    http.get(url('/sites'), () => HttpResponse.json(paged([HARBOUR]))),
    http.get(url('/members'), () => HttpResponse.json(paged([SAM]))),
  )
  return requests
}

describe('audit log', () => {
  it('renders each change as field: old -> new, and created/deleted rows as single values', async () => {
    signIn()
    serveAudit(() =>
      paged([
        auditEntry({
          changes: {
            name: { old: 'Harbour', new: 'Harbour Cafe' },
            isActive: { old: false, new: true },
            phone: { old: null, new: '0123' },
          },
        }),
        auditEntry({
          action: 'Created',
          entityType: 'Tag',
          changes: { name: { old: null, new: 'Breakfast' } },
          actorName: null,
        }),
        auditEntry({
          action: 'Deleted',
          entityType: 'Region',
          changes: { name: { old: 'North', new: null } },
        }),
        auditEntry({ changes: {} }),
      ]),
    )
    await renderApp('/settings/audit')

    const rows = await screen.findAllByTestId('audit-entry')
    expect(rows).toHaveLength(4)

    const updated = within(rows[0]!)
    expect(updated.getByText('Updated')).toBeInTheDocument()
    expect(updated.getByText('Sam Chef')).toBeInTheDocument()
    const lines = within(updated.getByRole('list', { name: 'Changes' })).getAllByRole('listitem')
    expect(lines.map((l) => l.textContent)).toEqual([
      'Is active: false → true',
      'Name: Harbour → Harbour Cafe',
      'Phone: — → 0123',
    ])

    expect(within(rows[1]!).getByText('System')).toBeInTheDocument()
    expect(within(rows[1]!).getByText('Name:').parentElement).toHaveTextContent('Name: Breakfast')
    expect(within(rows[2]!).getByText('Name:').parentElement).toHaveTextContent('Name: North')
    expect(within(rows[3]!).getByText('No field changes')).toBeInTheDocument()
  })

  it('sends the filters as request params and keeps them in the URL', async () => {
    signIn()
    const requests = serveAudit(() => paged([auditEntry()]))
    const user = userEvent.setup()
    const { router } = await renderApp('/settings/audit')
    await screen.findByTestId('audit-entry')
    expect(requests[0]!.get('Page')).toBe('1')
    expect(requests[0]!.get('PageSize')).toBe('25')
    expect(requests[0]!.get('EntityType')).toBeNull()

    const form = screen.getByRole('form', { name: 'Audit filters' })
    await user.type(within(form).getByLabelText('Entity type'), ' Site ')
    await user.selectOptions(within(form).getByLabelText('Action'), 'Updated')
    await user.selectOptions(await within(form).findByLabelText('Actor'), 'Sam Chef')
    await user.selectOptions(await within(form).findByLabelText('Site'), 'Harbour Cafe')
    await user.type(within(form).getByLabelText('From'), '2026-10-01')
    await user.type(within(form).getByLabelText('To'), '2026-10-31')
    await user.click(within(form).getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => expect(requests.at(-1)!.get('EntityType')).toBe('Site'))
    const last = requests.at(-1)!
    expect(last.get('Action')).toBe('Updated')
    expect(last.get('ActorId')).toBe('user-sam')
    expect(last.get('SiteId')).toBe('site-1')
    // Local midnight of the first day up to (exclusive) local midnight after the last.
    expect(last.get('From')).toBe(new Date(2026, 9, 1).toISOString())
    expect(last.get('To')).toBe(new Date(2026, 10, 1).toISOString())
    expect(router.state.location.search).toMatchObject({
      entityType: 'Site',
      action: 'Updated',
      actorId: 'user-sam',
      siteId: 'site-1',
      from: '2026-10-01',
      to: '2026-10-31',
    })

    // Applying remounts the form with the URL's values, so look it up again.
    await user.click(screen.getByRole('button', { name: 'Clear' }))
    await waitFor(() => expect(requests.at(-1)!.get('EntityType')).toBeNull())
    expect(requests.at(-1)!.get('From')).toBeNull()
    expect(router.state.location.search).toEqual({})
  })

  it('restores filters from the URL', async () => {
    signIn()
    const requests = serveAudit(() => paged([auditEntry()]))
    await renderApp('/settings/audit?entityType=Tag&action=Deleted&from=2026-10-01&to=2026-10-02')
    await screen.findByTestId('audit-entry')

    expect(requests[0]!.get('EntityType')).toBe('Tag')
    expect(requests[0]!.get('Action')).toBe('Deleted')
    expect(screen.getByLabelText('Entity type')).toHaveValue('Tag')
    expect(screen.getByLabelText('From')).toHaveValue('2026-10-01')
  })

  it('rejects an end date before the start date without sending', async () => {
    signIn()
    const requests = serveAudit(() => paged([auditEntry()]))
    const user = userEvent.setup()
    await renderApp('/settings/audit')
    await screen.findByTestId('audit-entry')

    await user.type(screen.getByLabelText('From'), '2026-10-10')
    await user.type(screen.getByLabelText('To'), '2026-10-01')
    expect(screen.getByText('The end date must not be before the start date.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply filters' })).toBeDisabled()
    expect(requests).toHaveLength(1)
  })

  it('pages with Previous and Next', async () => {
    signIn()
    const requests = serveAudit((params) => {
      const page = Number(params.get('Page'))
      return paged([auditEntry({ entityType: `Thing${page}` })], {
        page,
        totalCount: 60,
        totalPages: 3,
        hasNextPage: page < 3,
      })
    })
    const user = userEvent.setup()
    await renderApp('/settings/audit')

    expect(await screen.findByText('Thing1')).toBeInTheDocument()
    expect(screen.getByText('Page 1 of 3 · 60 entries')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText('Thing2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText('Thing3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Previous' }))
    expect(await screen.findByText('Thing2')).toBeInTheDocument()
    expect(requests.map((r) => r.get('Page'))).toEqual(['1', '2', '3', '2'])
  })

  it('shows an empty state', async () => {
    signIn()
    serveAudit(() => paged([]))
    await renderApp('/settings/audit')
    expect(await screen.findByText('No changes match these filters.')).toBeInTheDocument()
  })

  it('needs audit.view', async () => {
    signIn(['reports.export', 'sites.view'])
    let requested = false
    server.use(http.get(url('/audit'), () => ((requested = true), HttpResponse.json(paged([])))))
    await renderApp('/settings/audit')

    expect(
      await screen.findByText('You do not have permission to view this section.'),
    ).toBeInTheDocument()
    expect(requested).toBe(false)
  })

  it('leaves out the actor and site pickers without members.view and sites.view', async () => {
    signIn(['audit.view'])
    const requests = serveAudit(() => paged([auditEntry()]))
    await renderApp('/settings/audit')
    await screen.findByTestId('audit-entry')

    expect(screen.queryByLabelText('Actor')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Site')).not.toBeInTheDocument()
    expect(requests).toHaveLength(1)
    expect(screen.queryByRole('button', { name: 'Export CSV' })).not.toBeInTheDocument()
  })
})

describe('audit csv export', () => {
  function spyDownload() {
    const clicks: string[] = []
    vi.spyOn(URL, 'createObjectURL').mockImplementation(() => 'blob:test/1')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicks.push(this.download)
    })
    return clicks
  }

  it('exports with the entity type, site and dates through the reports download helpers', async () => {
    signIn()
    serveAudit(() => paged([auditEntry()]))
    const exports: URLSearchParams[] = []
    server.use(
      http.get(url('/reports/audit.csv'), ({ request }) => {
        exports.push(new URL(request.url).searchParams)
        return new HttpResponse('a,b\n1,2\n', {
          headers: {
            'Content-Type': 'text/csv',
            'Content-Disposition': 'attachment; filename=audit-2026-10-01-to-2026-10-31.csv',
          },
        })
      }),
    )
    const clicks = spyDownload()
    const user = userEvent.setup()
    await renderApp('/settings/audit?entityType=Site&siteId=site-1&from=2026-10-01&to=2026-10-31')
    await screen.findByTestId('audit-entry')

    await user.click(screen.getByRole('button', { name: 'Export CSV' }))

    expect(await screen.findByText('Saved audit-2026-10-01-to-2026-10-31.csv.')).toBeInTheDocument()
    expect(exports).toHaveLength(1)
    expect(Object.fromEntries(exports[0]!)).toEqual({
      EntityType: 'Site',
      SiteId: 'site-1',
      From: '2026-10-01',
      To: '2026-10-31',
    })
    expect(clicks).toEqual(['audit-2026-10-01-to-2026-10-31.csv'])
  })

  it('shows the API refusal and needs both dates or none', async () => {
    signIn()
    serveAudit(() => paged([auditEntry()]))
    server.use(
      http.get(url('/reports/audit.csv'), () =>
        HttpResponse.json(
          {
            type: 'about:blank',
            title: 'x',
            status: 400,
            detail: 'Choose a range of at most 93 days.',
            code: 'report.range_too_long',
          },
          { status: 400 },
        ),
      ),
    )
    spyDownload()
    const user = userEvent.setup()
    await renderApp('/settings/audit?from=2026-01-01&to=2026-11-01')
    await screen.findByTestId('audit-entry')

    await user.click(screen.getByRole('button', { name: 'Export CSV' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a range of at most 93 days.')
  })

  it('disables the export when only one date is set', async () => {
    signIn()
    serveAudit(() => paged([auditEntry()]))
    await renderApp('/settings/audit?from=2026-10-01')
    await screen.findByTestId('audit-entry')

    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeDisabled()
    expect(screen.getByText('Set both dates, or neither, to export.')).toBeInTheDocument()
  })

  it('is hidden without reports.export', async () => {
    signIn(['audit.view'])
    serveAudit(() => paged([auditEntry()]))
    await renderApp('/settings/audit')
    await screen.findByTestId('audit-entry')
    expect(screen.queryByRole('button', { name: 'Export CSV' })).not.toBeInTheDocument()
  })
})

describe('change helpers', () => {
  it('formats values and labels', () => {
    expect(formatValue(null)).toBeNull()
    expect(formatValue('')).toBe('(empty)')
    expect(formatValue(3)).toBe('3')
    expect(formatValue(['a', 1])).toBe('["a",1]')
    expect(fieldLabel('openingHours')).toBe('Opening hours')
    expect(fieldLabel('address.line1')).toBe('Address line1')
    expect(changeLines(undefined)).toEqual([])
  })

  it('turns a date range into a half-open instant range', () => {
    const q = auditApiQuery({ from: '2026-10-31', to: '2026-10-31' })
    expect(q.From).toBe(new Date(2026, 9, 31).toISOString())
    expect(q.To).toBe(new Date(2026, 10, 1).toISOString())
  })
})
