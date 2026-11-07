import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { schedule, serveSchedules, templateSummary } from '@/test/scheduleFixtures'
import { server } from '@/test/server'
import { department, paged, recordBodies, region, signIn, site, url } from '@/test/settingsFixtures'

const NORTH = region({ id: 'region-north', name: 'North' })
const HARBOUR = site({ id: 'site-1', name: 'Harbour Cafe', timeZone: 'Europe/London' })
const QUAY = site({ id: 'site-2', name: 'Quay Bar', timeZone: 'America/New_York' })
const KITCHEN = department({ id: 'dept-1', name: 'Kitchen' })

function serveReference() {
  server.use(
    http.get(url('/sites'), () => HttpResponse.json(paged([HARBOUR, QUAY]))),
    http.get(url('/regions'), () => HttpResponse.json([NORTH])),
    http.get(url('/departments'), () => HttpResponse.json([KITCHEN])),
  )
}

const OPENING = templateSummary({ id: 'template-1', name: 'Opening checks' })
const CLOSING = templateSummary({ id: 'template-2', name: 'Closing checks' })

describe('schedules list', () => {
  it('shows the schedule in words with its targets, and marks stopped ones', async () => {
    signIn()
    serveReference()
    serveSchedules([
      schedule({
        id: 's1',
        frequency: 'Weekly',
        days: 'Weekdays',
        availableFrom: '06:00:00',
        dueTime: '11:00:00',
        siteIds: ['site-1', 'site-2'],
      }),
      schedule({
        id: 's2',
        templateName: 'Deep clean',
        frequency: 'Monthly',
        dayOfMonth: -1,
        allSites: true,
        siteIds: [],
        isActive: false,
      }),
    ])
    await renderApp('/settings/schedules')

    expect(
      await screen.findByText('Every weekday, from 06:00, due by 11:00, at 2 sites'),
    ).toBeInTheDocument()
    expect(await screen.findByText('Harbour Cafe, Quay Bar')).toBeInTheDocument()
    expect(
      screen.getByText('Every month on the last day, due by 11:00, at all sites'),
    ).toBeInTheDocument()
    expect(screen.getByText('Stopped')).toBeInTheDocument()
  })

  it('filters by template, site and status through the API', async () => {
    signIn()
    serveReference()
    const queries = serveSchedules([schedule()], [OPENING, CLOSING])
    const user = userEvent.setup()
    await renderApp('/settings/schedules')

    await screen.findByText(/Every day/)
    expect(queries.at(-1)?.get('IncludeInactive')).toBe('false')

    await user.selectOptions(screen.getByLabelText('Template'), 'template-2')
    await waitFor(() => expect(queries.at(-1)?.get('TemplateId')).toBe('template-2'))

    await user.selectOptions(await screen.findByLabelText('Site'), 'site-2')
    await waitFor(() => expect(queries.at(-1)?.get('SiteId')).toBe('site-2'))

    await user.selectOptions(screen.getByLabelText('Status'), 'all')
    await waitFor(() => expect(queries.at(-1)?.get('IncludeInactive')).toBe('true'))
  })

  it('shows an empty state', async () => {
    signIn()
    serveReference()
    serveSchedules([])
    await renderApp('/settings/schedules')

    expect(await screen.findByText('No schedules found.')).toBeInTheDocument()
  })

  it('stops and resumes after confirmation', async () => {
    signIn()
    serveReference()
    serveSchedules([
      schedule({ id: 's1', templateName: 'Opening checks' }),
      schedule({ id: 's2', templateName: 'Closing checks', isActive: false }),
    ])
    const stop = recordBodies('post', '/schedules/s1/deactivate')
    const resume = recordBodies('post', '/schedules/s2/activate')
    const user = userEvent.setup()
    await renderApp('/settings/schedules')

    await user.click(await screen.findByRole('button', { name: 'Stop schedule Opening checks' }))
    await user.click(await screen.findByRole('button', { name: 'Stop schedule' }))
    await waitFor(() => expect(stop).toHaveLength(1))

    await user.click(await screen.findByRole('button', { name: 'Resume schedule Closing checks' }))
    await user.click(await screen.findByRole('button', { name: 'Resume schedule' }))
    await waitFor(() => expect(resume).toHaveLength(1))
  })

  it('deletes after confirmation and shows a refusal inside the dialog', async () => {
    signIn()
    serveReference()
    serveSchedules([schedule({ id: 's1' })])
    server.use(
      http.delete(url('/schedules/s1'), () =>
        HttpResponse.json(problem(404, 'schedule.not_found', 'Schedule not found.'), {
          status: 404,
        }),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/schedules')

    await user.click(await screen.findByRole('button', { name: 'Delete schedule Opening checks' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/not yet due and untouched/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Delete schedule' }))

    expect(await within(dialog).findByText('Schedule not found.')).toBeInTheDocument()
  })

  it('deletes successfully', async () => {
    signIn()
    serveReference()
    serveSchedules([schedule({ id: 's1' })])
    const deleted = recordBodies('delete', '/schedules/s1')
    const user = userEvent.setup()
    await renderApp('/settings/schedules')

    await user.click(await screen.findByRole('button', { name: 'Delete schedule Opening checks' }))
    await user.click(await screen.findByRole('button', { name: 'Delete schedule' }))
    await waitFor(() => expect(deleted).toHaveLength(1))
  })

  it('is closed to someone without schedules.manage', async () => {
    signIn(['templates.view', 'sites.view'])
    await renderApp('/settings/schedules')

    expect(await screen.findByText(/do not have permission/)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Add schedule' })).not.toBeInTheDocument()
  })

  it('closes the form to someone without schedules.manage', async () => {
    signIn(['templates.view', 'sites.view'])
    await renderApp('/settings/schedules/new')

    expect(await screen.findByText(/do not have permission/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add schedule' })).not.toBeInTheDocument()
  })
})

async function openNewForm() {
  signIn()
  serveReference()
  serveSchedules(
    [],
    [OPENING, CLOSING, templateSummary({ id: 'template-3', name: 'Old', isActive: false })],
  )
  const user = userEvent.setup()
  await renderApp('/settings/schedules/new')
  await screen.findByLabelText('Template')
  return user
}

const setField = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } })

const created = () => HttpResponse.json(schedule({ id: 'new' }), { status: 201 })

describe('schedule form', () => {
  it('creates a daily schedule at one site', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    setField('Starts on', '2030-01-07')
    setField('Due by', '11:00')
    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      templateId: 'template-1',
      frequency: 'Daily',
      interval: 1,
      startDate: '2030-01-07',
      endDate: null,
      days: 'None',
      dayOfMonth: null,
      dueTime: '11:00:00',
      availableFrom: null,
      allSites: false,
      siteIds: ['site-1'],
      regionIds: [],
      departmentId: null,
    })
    // Back on the list afterwards.
    expect(await screen.findByRole('link', { name: 'Add schedule' })).toBeInTheDocument()
  })

  it('offers only active templates', async () => {
    await openNewForm()
    const select = screen.getByLabelText('Template')
    expect(within(select).getByRole('option', { name: 'Opening checks' })).toBeInTheDocument()
    expect(within(select).queryByRole('option', { name: 'Old' })).toBeNull()
  })

  it('creates a weekday schedule with times, end date, department and all sites', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-2')
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Weekly')
    await user.click(screen.getByRole('button', { name: 'Weekdays' }))
    setField('Starts on', '2030-01-07')
    setField('Ends on', '2030-12-31')
    setField('Available from', '06:30')
    setField('Due by', '11:00')
    await user.selectOptions(screen.getByLabelText('Department'), 'dept-1')
    await user.click(screen.getByRole('checkbox', { name: 'All sites' }))

    expect(screen.getByTestId('schedule-summary')).toHaveTextContent(
      'Every weekday, until 31 Dec 2030, from 06:30, due by 11:00, at all sites',
    )
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      templateId: 'template-2',
      frequency: 'Weekly',
      interval: 1,
      startDate: '2030-01-07',
      endDate: '2030-12-31',
      days: 'Monday, Tuesday, Wednesday, Thursday, Friday',
      dayOfMonth: null,
      dueTime: '11:00:00',
      availableFrom: '06:30:00',
      allSites: true,
      siteIds: [],
      regionIds: [],
      departmentId: 'dept-1',
    })
  })

  it('sends a fortnightly schedule on chosen days at a region', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Weekly')
    setField('Every how many week(s)', '2')
    await user.click(screen.getByRole('checkbox', { name: 'Monday' }))
    await user.click(screen.getByRole('checkbox', { name: 'Thursday' }))
    setField('Starts on', '2030-01-07')
    setField('Due by', '09:00')
    await user.click(screen.getByRole('checkbox', { name: 'North' }))

    expect(screen.getByTestId('schedule-summary')).toHaveTextContent(
      'Every 2 weeks on Mon, Thu, due by 09:00, at 1 region',
    )
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      frequency: 'Weekly',
      interval: 2,
      days: 'Monday, Thursday',
      allSites: false,
      siteIds: [],
      regionIds: ['region-north'],
    })
  })

  it('sends the last day for a monthly schedule and leaves no stray weekdays', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    // Pick weekly days first, then switch: they must not be sent for a monthly schedule.
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Weekly')
    await user.click(screen.getByRole('checkbox', { name: 'Friday' }))
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Monthly')
    await user.selectOptions(screen.getByLabelText('On day'), '-1')
    setField('Starts on', '2030-01-31')
    setField('Due by', '17:00')
    await user.click(screen.getByRole('checkbox', { name: 'Quay Bar' }))

    expect(screen.getByTestId('schedule-summary')).toHaveTextContent(
      'Every month on the last day, due by 17:00, at 1 site',
    )
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      frequency: 'Monthly',
      interval: 1,
      days: 'None',
      dayOfMonth: -1,
      siteIds: ['site-2'],
    })
  })

  it('sends a yearly schedule', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Yearly')
    setField('Starts on', '2030-03-09')
    setField('Due by', '12:00')
    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      frequency: 'Yearly',
      interval: 1,
      startDate: '2030-03-09',
      dayOfMonth: null,
      days: 'None',
    })
  })

  it('sends a one-off schedule without an interval or end date', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Daily')
    setField('Ends on', '2031-01-01')
    await user.selectOptions(screen.getByLabelText('Repeats'), 'Once')
    expect(screen.queryByLabelText('Ends on')).not.toBeInTheDocument()
    setField('Date', '2030-05-01')
    setField('Due by', '12:00')
    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      frequency: 'Once',
      interval: 1,
      startDate: '2030-05-01',
      endDate: null,
    })
  })

  it('previews the next ten dates', async () => {
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Repeats'), 'Monthly')
    setField('Starts on', '2030-01-31')
    setField('Due by', '10:00')

    const list = screen.getByRole('list', { name: 'Next occurrences' })
    const dates = within(list).getAllByRole('listitem')
    expect(dates).toHaveLength(10)
    expect(dates[0]).toHaveTextContent('Thu 31 Jan 2030')
    expect(dates[1]).toHaveTextContent('Thu 28 Feb 2030')
    expect(dates[2]).toHaveTextContent('Sun 31 Mar 2030')
  })

  it('names the time zones of the chosen sites', async () => {
    const user = await openNewForm()

    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(screen.getByRole('checkbox', { name: 'Quay Bar' }))
    expect(
      screen.getByText('Times are the local time at each site (America/New_York, Europe/London).'),
    ).toBeInTheDocument()
  })

  it('validates required fields before sending anything', async () => {
    const bodies = recordBodies('post', '/schedules', created)
    const user = await openNewForm()

    await user.click(screen.getByRole('button', { name: 'Add schedule' }))
    expect(await screen.findByText('Choose a template.')).toBeInTheDocument()
    expect(screen.getByText('Set the time the task is due by.')).toBeInTheDocument()
    expect(
      screen.getByText('Choose all sites, or at least one region or site.'),
    ).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    setField('Starts on', '2030-02-10')
    setField('Ends on', '2030-02-01')
    setField('Available from', '12:00')
    setField('Due by', '11:00')
    setField('Every how many day(s)', '60')
    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(screen.getByRole('button', { name: 'Add schedule' }))

    expect(
      await screen.findByText('The end date cannot be before the start date.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('"Available from" must be earlier than the due time.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Enter a whole number from 1 to 52.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('maps server failures onto their fields', async () => {
    let call = 0
    recordBodies('post', '/schedules', () => {
      call += 1
      if (call === 1)
        return HttpResponse.json(
          problem(400, 'schedule.template_inactive', 'The template is not active.'),
          { status: 400 },
        )
      if (call === 2)
        return HttpResponse.json(
          problem(400, 'validation.failed', 'Invalid.', {
            errors: {
              DueTime: ['Due time is not valid.'],
              recurrence: ['Interval must be between 1 and 52.'],
            },
          }),
          { status: 400 },
        )
      if (call === 3)
        return HttpResponse.json(
          problem(403, 'schedule.out_of_scope', 'You can only schedule tasks for your sites.'),
          { status: 403 },
        )
      return HttpResponse.json(
        problem(
          400,
          'schedule.unknown_ids',
          'One or more sites, regions or the department do not exist.',
        ),
        { status: 400 },
      )
    })
    const user = await openNewForm()

    await user.selectOptions(screen.getByLabelText('Template'), 'template-1')
    setField('Starts on', '2030-01-07')
    setField('Due by', '11:00')
    await user.click(screen.getByRole('checkbox', { name: 'Harbour Cafe' }))
    const submit = () => user.click(screen.getByRole('button', { name: 'Add schedule' }))

    await submit()
    expect(await screen.findByText('The template is not active.')).toBeInTheDocument()
    expect(screen.getByLabelText('Template')).toHaveAttribute('aria-invalid', 'true')

    await submit()
    expect(await screen.findByText('Due time is not valid.')).toBeInTheDocument()
    expect(screen.getByLabelText('Due by')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Interval must be between 1 and 52.')).toBeInTheDocument()

    await submit()
    expect(
      await screen.findByText('You can only schedule tasks for your sites.'),
    ).toBeInTheDocument()

    await submit()
    expect(
      await screen.findByText('One or more sites, regions or the department do not exist.'),
    ).toBeInTheDocument()
  })

  it('loads a schedule for editing and saves the change', async () => {
    signIn()
    serveReference()
    serveSchedules([], [OPENING, CLOSING])
    const existing = schedule({
      id: 's1',
      frequency: 'Weekly',
      interval: 2,
      days: 'Monday, Thursday',
      startDate: '2030-01-07',
      endDate: '2030-12-31',
      availableFrom: '06:00:00',
      dueTime: '11:00:00',
      siteIds: ['site-2'],
      departmentId: 'dept-1',
    })
    server.use(http.get(url('/schedules/s1'), () => HttpResponse.json(existing)))
    const bodies = recordBodies('put', '/schedules/s1', () => HttpResponse.json(existing))
    const user = userEvent.setup()
    await renderApp('/settings/schedules/s1')

    await screen.findByLabelText('Template')
    expect(screen.getByLabelText('Template')).toHaveValue('template-1')
    expect(screen.getByLabelText('Repeats')).toHaveValue('Weekly')
    expect(screen.getByLabelText('Every how many week(s)')).toHaveValue(2)
    expect(screen.getByRole('checkbox', { name: 'Monday' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Tuesday' })).not.toBeChecked()
    expect(screen.getByLabelText('Due by')).toHaveValue('11:00')
    expect(screen.getByLabelText('Available from')).toHaveValue('06:00')
    expect(screen.getByRole('checkbox', { name: 'Quay Bar' })).toBeChecked()
    expect(screen.getByLabelText('Department')).toHaveValue('dept-1')

    setField('Due by', '12:15')
    await user.click(screen.getByRole('checkbox', { name: 'Friday' }))
    await user.click(screen.getByRole('button', { name: 'Save schedule' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      templateId: 'template-1',
      frequency: 'Weekly',
      interval: 2,
      days: 'Monday, Thursday, Friday',
      dueTime: '12:15:00',
      availableFrom: '06:00:00',
      siteIds: ['site-2'],
      departmentId: 'dept-1',
    })
  })

  it('keeps an inactive template selectable on the schedule that uses it', async () => {
    signIn()
    serveReference()
    serveSchedules([], [CLOSING])
    server.use(
      http.get(url('/schedules/s1'), () =>
        HttpResponse.json(schedule({ id: 's1', templateId: 'gone', templateName: 'Retired' })),
      ),
    )
    await renderApp('/settings/schedules/s1')

    await screen.findByLabelText('Template')
    expect(screen.getByLabelText('Template')).toHaveValue('gone')
    expect(screen.getByRole('option', { name: 'Retired (inactive)' })).toBeInTheDocument()
  })

  it('says so when the schedule is not found', async () => {
    signIn()
    serveReference()
    serveSchedules([])
    server.use(
      http.get(url('/schedules/missing'), () =>
        HttpResponse.json(problem(404, 'schedule.not_found', 'Schedule not found.'), {
          status: 404,
        }),
      ),
    )
    await renderApp('/settings/schedules/missing')

    expect(await screen.findByText('Schedule not found.')).toBeInTheDocument()
  })
})
