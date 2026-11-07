import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem } from '@/test/fixtures'
import { apiKey, delivery, device, EVENT_TYPES, webhook } from '@/test/integrationFixtures'
import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import { paged, recordBodies, signIn, site, url } from '@/test/settingsFixtures'

const SECRET = 'whsec_SUPER_SECRET_VALUE_123'
const KEY = 'trk_ab12_FULL_KEY_VALUE_456'

const HOOK = webhook({ id: 'hook-1', url: 'https://hooks.example.com/a' })

function serveWebhooks(list = [HOOK]) {
  server.use(
    http.get(url('/webhooks'), () => HttpResponse.json(list)),
    http.get(url('/webhooks/event-types'), () => HttpResponse.json(EVENT_TYPES)),
  )
}

/** The secret must not survive in the query or mutation caches. */
function cachesHold(queryClient: import('@tanstack/react-query').QueryClient, text: string) {
  const queries = queryClient
    .getQueryCache()
    .getAll()
    .map((q) => JSON.stringify(q.state.data ?? null))
  const mutations = queryClient
    .getMutationCache()
    .getAll()
    .map((m) => JSON.stringify(m.state.data ?? null))
  return [...queries, ...mutations].some((s) => s.includes(text))
}

// Forms are driven keystroke by keystroke, so allow for a loaded machine.
describe('webhooks', { timeout: 20_000 }, () => {
  it('creates a webhook, shows the secret once and drops it on close', async () => {
    signIn()
    serveWebhooks([])
    const bodies = recordBodies('post', '/webhooks', () =>
      HttpResponse.json(
        {
          webhook: webhook({ id: 'hook-9', url: 'https://hooks.example.com/new' }),
          secret: SECRET,
        },
        { status: 201 },
      ),
    )
    const user = userEvent.setup()
    const { queryClient } = await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: 'Add webhook' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add webhook' })

    // Friendly client-side checks run before anything is sent.
    await user.type(within(dialog).getByLabelText('URL'), 'http://hooks.example.com/x')
    await user.click(within(dialog).getByRole('button', { name: 'Add webhook' }))
    expect(await within(dialog).findByText('The URL must use https.')).toBeInTheDocument()
    expect(within(dialog).getByText('Choose at least one event type.')).toBeInTheDocument()
    expect(bodies).toHaveLength(0)

    await user.clear(within(dialog).getByLabelText('URL'))
    await user.type(within(dialog).getByLabelText('URL'), 'https://192.168.1.5/x')
    await user.click(within(dialog).getByRole('button', { name: 'Add webhook' }))
    expect(await within(dialog).findByText(/private, loopback/)).toBeInTheDocument()

    await user.clear(within(dialog).getByLabelText('URL'))
    await user.type(within(dialog).getByLabelText('URL'), ' https://hooks.example.com/new ')
    await user.type(within(dialog).getByLabelText('Description'), 'Ops')
    await user.click(await within(dialog).findByRole('checkbox', { name: 'task.completed' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'issue.raised' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add webhook' }))

    const reveal = await screen.findByRole('dialog', { name: 'Webhook created' })
    expect(bodies).toEqual([
      {
        url: 'https://hooks.example.com/new',
        description: 'Ops',
        eventTypes: ['task.completed', 'issue.raised'],
      },
    ])
    expect(within(reveal).getByTestId('secret-value')).toHaveTextContent(SECRET)
    expect(within(reveal).getByText(/You won.t see this again/)).toBeInTheDocument()

    await user.click(within(reveal).getByRole('button', { name: 'Copy' }))
    expect(await navigator.clipboard.readText()).toBe(SECRET)
    expect(cachesHold(queryClient, SECRET)).toBe(false)

    await user.click(within(reveal).getByRole('button', { name: 'I have saved it' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.queryByText(SECRET)).not.toBeInTheDocument()
    expect(cachesHold(queryClient, SECRET)).toBe(false)
  })

  it('shows the server refusal inside the form', async () => {
    signIn()
    serveWebhooks([])
    server.use(
      http.post(url('/webhooks'), () =>
        HttpResponse.json(
          problem(400, 'validation', 'Invalid', { errors: { url: ['The URL must use https.'] } }),
          { status: 400 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: 'Add webhook' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('URL'), 'https://example.com/ok')
    await user.click(await within(dialog).findByRole('checkbox', { name: 'task.completed' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add webhook' }))

    expect(await within(dialog).findByText('The URL must use https.')).toBeInTheDocument()
  })

  it('edits a webhook and can switch it off', async () => {
    signIn()
    serveWebhooks()
    const bodies = recordBodies('put', '/webhooks/hook-1', () => HttpResponse.json(HOOK))
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: `Edit webhook ${HOOK.url}` }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit webhook' })
    expect(within(dialog).getByLabelText('URL')).toHaveValue(HOOK.url)
    await user.click(await within(dialog).findByRole('checkbox', { name: 'Active' }))
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodies).toEqual([
      { url: HOOK.url, description: null, eventTypes: ['task.completed'], isActive: false },
    ])
  })

  it('rotates the secret after confirmation and shows the new one once', async () => {
    signIn()
    serveWebhooks()
    let calls = 0
    server.use(
      http.post(url('/webhooks/hook-1/rotate-secret'), () => {
        calls++
        return HttpResponse.json({ webhook: HOOK, secret: SECRET })
      }),
    )
    const user = userEvent.setup()
    const { queryClient } = await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: `Rotate secret for ${HOOK.url}` }))
    const confirm = await screen.findByRole('alertdialog')
    expect(calls).toBe(0)
    await user.click(within(confirm).getByRole('button', { name: 'Rotate secret' }))

    const reveal = await screen.findByRole('dialog', { name: 'New signing secret' })
    expect(within(reveal).getByTestId('secret-value')).toHaveTextContent(SECRET)
    expect(calls).toBe(1)
    expect(cachesHold(queryClient, SECRET)).toBe(false)
    await user.click(within(reveal).getByRole('button', { name: 'I have saved it' }))
    await waitFor(() => expect(screen.queryByText(SECRET)).not.toBeInTheDocument())
  })

  it('sends a test event', async () => {
    signIn()
    serveWebhooks()
    let calls = 0
    server.use(
      http.post(url('/webhooks/hook-1/test'), () => {
        calls++
        return HttpResponse.json(delivery({ status: 'Pending' }), { status: 202 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: `Send test to ${HOOK.url}` }))
    await waitFor(() => expect(calls).toBe(1))
  })

  it('deletes a webhook after confirmation', async () => {
    signIn()
    serveWebhooks()
    const bodies = recordBodies('delete', '/webhooks/hook-1')
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: `Delete webhook ${HOOK.url}` }))
    const confirm = await screen.findByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: 'Delete webhook' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
  })

  it('pages through deliveries and redelivers a failed one', async () => {
    signIn()
    serveWebhooks()
    const pages: string[] = []
    const failed = delivery({
      id: 'del-fail',
      status: 'Failed',
      attempts: 5,
      lastStatusCode: 500,
      lastError: 'Server error',
      deliveredAt: null,
    })
    server.use(
      http.get(url('/webhooks/hook-1/deliveries'), ({ request }) => {
        const params = new URL(request.url).searchParams
        const page = Number(params.get('Page'))
        pages.push(`${params.get('Page')}/${params.get('PageSize')}`)
        return HttpResponse.json(
          page === 1
            ? paged([failed], { pageSize: 10, totalPages: 2, hasNextPage: true })
            : paged([delivery({ eventType: 'issue.raised' })], {
                page: 2,
                pageSize: 10,
                totalPages: 2,
              }),
        )
      }),
    )
    const redeliveries = recordBodies('post', '/webhooks/deliveries/del-fail/redeliver', () =>
      HttpResponse.json(delivery({ status: 'Pending' })),
    )
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    await user.click(await screen.findByRole('button', { name: 'Deliveries' }))
    const row = await screen.findByTestId('delivery')
    expect(within(row).getByText('Failed')).toBeInTheDocument()
    expect(within(row).getByText(/5 attempts · response 500/)).toBeInTheDocument()
    expect(within(row).getByText('Server error')).toBeInTheDocument()

    await user.click(within(row).getByRole('button', { name: /Redeliver/ }))
    await waitFor(() => expect(redeliveries).toHaveLength(1))

    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(
      await screen.findByText('issue.raised', { selector: 'span.font-medium' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument()
    expect(pages).toContain('2/10')
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })
})

// Forms are driven keystroke by keystroke, so allow for a loaded machine.
describe('api keys', { timeout: 20_000 }, () => {
  const tab = '/settings/integrations?tab=api-keys'

  it('creates a key and shows the full key once', async () => {
    signIn()
    const created = apiKey({ id: 'key-9', name: 'Kitchen gateway' })
    let keys = [apiKey({ id: 'key-1', name: 'Old key', lastUsedAt: '2026-10-20T08:00:00Z' })]
    server.use(
      http.get(url('/api-keys'), () => HttpResponse.json(keys)),
      http.get(url('/sites'), () =>
        HttpResponse.json(paged([site({ id: 'site-1', name: 'Harbour Cafe' })])),
      ),
    )
    const bodies = recordBodies('post', '/api-keys', () => {
      keys = [...keys, created]
      return HttpResponse.json({ apiKey: created, key: KEY }, { status: 201 })
    })
    const user = userEvent.setup()
    const { queryClient } = await renderApp(tab)

    expect(await screen.findByText('Old key')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Create API key' }))
    const dialog = await screen.findByRole('dialog', { name: 'Create API key' })
    await user.click(within(dialog).getByRole('button', { name: 'Create key' }))
    expect(await within(dialog).findByText('Enter a name.')).toBeInTheDocument()
    expect(bodies).toHaveLength(0)

    await user.type(within(dialog).getByLabelText('Name'), 'Kitchen gateway')
    await user.click(await within(dialog).findByRole('checkbox', { name: 'Harbour Cafe' }))
    await user.click(within(dialog).getByRole('button', { name: 'Create key' }))

    const reveal = await screen.findByRole('dialog', { name: 'API key created' })
    expect(bodies).toEqual([
      {
        name: 'Kitchen gateway',
        scopes: ['sensors.write'],
        siteIds: ['site-1'],
        expiresAt: null,
      },
    ])
    expect(within(reveal).getByTestId('secret-value')).toHaveTextContent(KEY)
    expect(cachesHold(queryClient, KEY)).toBe(false)

    await user.click(within(reveal).getByRole('button', { name: 'I have saved it' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.queryByText(KEY)).not.toBeInTheDocument()
    expect(await screen.findByText('Kitchen gateway')).toBeInTheDocument()
    expect(cachesHold(queryClient, KEY)).toBe(false)
  })

  it('revokes a key after confirmation and hides its revoke button', async () => {
    signIn()
    let keys = [
      apiKey({ id: 'key-1', name: 'Gateway' }),
      apiKey({ id: 'key-2', name: 'Gone', revokedAt: '2026-10-05T00:00:00Z' }),
    ]
    server.use(http.get(url('/api-keys'), () => HttpResponse.json(keys)))
    const revoked = recordBodies('post', '/api-keys/key-1/revoke', () => {
      keys = [{ ...keys[0]!, revokedAt: '2026-11-07T00:00:00Z' }, keys[1]!]
      return new HttpResponse(null, { status: 204 })
    })
    const user = userEvent.setup()
    await renderApp(tab)

    await screen.findByText('Gateway')
    expect(screen.queryByRole('button', { name: 'Revoke API key Gone' })).not.toBeInTheDocument()
    expect(screen.getByText('Revoked')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Revoke API key Gateway' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(revoked).toHaveLength(0)
    await user.click(within(confirm).getByRole('button', { name: 'Revoke key' }))

    await waitFor(() => expect(revoked).toHaveLength(1))
    await waitFor(() =>
      expect(
        screen.queryByRole('button', { name: 'Revoke API key Gateway' }),
      ).not.toBeInTheDocument(),
    )
  })
})

// Forms are driven keystroke by keystroke, so allow for a loaded machine.
describe('sensors', { timeout: 20_000 }, () => {
  const tab = '/settings/integrations?tab=sensors'
  const TEMPLATE = { id: 'tpl-1', name: 'Fridge log' }

  function serveSensors(devices = [device()]) {
    server.use(
      http.get(url('/sensor-devices'), () => HttpResponse.json(paged(devices))),
      http.get(url('/sites'), () =>
        HttpResponse.json(paged([site({ id: 'site-1', name: 'Harbour Cafe' })])),
      ),
      http.get(url('/templates'), () => HttpResponse.json(paged([TEMPLATE]))),
      http.get(url('/templates/tpl-1'), () =>
        HttpResponse.json({
          ...TEMPLATE,
          kind: 'RecordLog',
          fields: [
            { key: 'note', label: 'Note', type: 'Text' },
            { key: 'temp', label: 'Fridge temp', type: 'Temperature', unit: 'C' },
          ],
        }),
      ),
    )
  }

  it('lists devices and explains how to post readings', async () => {
    signIn()
    serveSensors()
    await renderApp(tab)

    expect(await screen.findByText('Walk-in fridge')).toBeInTheDocument()
    expect(await screen.findByText(/Harbour Cafe · Fridge log \/ temp/)).toBeInTheDocument()
    const example = screen.getByTestId('sensor-example')
    expect(example).toHaveTextContent('/api/v1/sensors/readings')
    expect(example).toHaveTextContent('X-Api-Key')
    expect(example).toHaveTextContent('"deviceId":"fridge-01"')
  })

  it('creates a device mapped to a temperature field', async () => {
    signIn()
    serveSensors([])
    const bodies = recordBodies('post', '/sensor-devices', (body) =>
      HttpResponse.json(device(body as object), { status: 201 }),
    )
    const user = userEvent.setup()
    await renderApp(tab)

    await user.click(await screen.findByRole('button', { name: 'Add device' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add device' })
    await user.click(within(dialog).getByRole('button', { name: 'Add device' }))
    expect(await within(dialog).findByText('Choose a site.')).toBeInTheDocument()
    expect(bodies).toHaveLength(0)

    await user.type(within(dialog).getByLabelText('Device id'), 'fridge-02')
    await user.type(within(dialog).getByLabelText('Name'), 'Freezer')
    await user.selectOptions(await within(dialog).findByLabelText('Site'), 'Harbour Cafe')
    await user.selectOptions(within(dialog).getByLabelText('Record log template'), 'Fridge log')
    const field = within(dialog).getByLabelText('Temperature field')
    await waitFor(() => expect(field).toBeEnabled())
    expect(within(field).queryByRole('option', { name: 'Note' })).not.toBeInTheDocument()
    await user.selectOptions(within(dialog).getByLabelText('Temperature field'), 'Fridge temp (C)')
    await user.click(within(dialog).getByRole('button', { name: 'Add device' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(bodies).toEqual([
      {
        externalId: 'fridge-02',
        name: 'Freezer',
        siteId: 'site-1',
        templateId: 'tpl-1',
        fieldKey: 'temp',
      },
    ])
  })

  it('shows a taken device id on the field', async () => {
    signIn()
    serveSensors([])
    server.use(
      http.post(url('/sensor-devices'), () =>
        HttpResponse.json(
          problem(409, 'sensor_device.external_id_taken', 'A device with this id already exists.'),
          {
            status: 409,
          },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp(tab)

    await user.click(await screen.findByRole('button', { name: 'Add device' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Device id'), 'dup')
    await user.type(within(dialog).getByLabelText('Name'), 'Dup')
    await user.selectOptions(await within(dialog).findByLabelText('Site'), 'Harbour Cafe')
    await user.selectOptions(within(dialog).getByLabelText('Record log template'), 'Fridge log')
    await waitFor(() => expect(within(dialog).getByLabelText('Temperature field')).toBeEnabled())
    await user.selectOptions(within(dialog).getByLabelText('Temperature field'), 'Fridge temp (C)')
    await user.click(within(dialog).getByRole('button', { name: 'Add device' }))

    expect(
      await within(dialog).findByText('A device with this id already exists.'),
    ).toBeInTheDocument()
  })

  it('edits a device with its current mapping and deletes one', async () => {
    signIn()
    const user = userEvent.setup()
    const existing = device({ id: 'dev-1', name: 'Walk-in fridge' })
    serveSensors([existing])
    const edits = recordBodies('put', '/sensor-devices/dev-1', () => HttpResponse.json(existing))
    const deletes = recordBodies('delete', '/sensor-devices/dev-1')
    await renderApp(tab)

    await user.click(await screen.findByRole('button', { name: 'Edit device Walk-in fridge' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit device' })
    expect(within(dialog).getByLabelText('Device id')).toHaveValue('fridge-01')
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Temperature field')).toHaveValue('temp'),
    )
    await user.clear(within(dialog).getByLabelText('Name'))
    await user.type(within(dialog).getByLabelText('Name'), 'Fridge A')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(edits).toHaveLength(1))
    expect(edits[0]).toEqual({
      externalId: 'fridge-01',
      name: 'Fridge A',
      siteId: 'site-1',
      templateId: 'tpl-1',
      fieldKey: 'temp',
    })

    await user.click(await screen.findByRole('button', { name: 'Delete device Walk-in fridge' }))
    const confirm = await screen.findByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: 'Delete device' }))
    await waitFor(() => expect(deletes).toHaveLength(1))
  })
})

describe('permission gating', () => {
  it('explains instead of loading when integrations.manage is missing', async () => {
    signIn(['sites.view'])
    let requested = false
    server.use(http.get(url('/webhooks'), () => ((requested = true), HttpResponse.json([]))))
    await renderApp('/settings/integrations')

    expect(
      await screen.findByText('You do not have permission to view this section.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(requested).toBe(false)
  })

  it('switches between the three tabs', async () => {
    signIn()
    serveWebhooks([])
    server.use(
      http.get(url('/api-keys'), () => HttpResponse.json([])),
      http.get(url('/sensor-devices'), () => HttpResponse.json(paged([]))),
      http.get(url('/sites'), () => HttpResponse.json(paged([]))),
      http.get(url('/templates'), () => HttpResponse.json(paged([]))),
    )
    const user = userEvent.setup()
    await renderApp('/settings/integrations')

    expect(await screen.findByText('No webhooks yet.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'API keys' }))
    expect(await screen.findByText('No API keys yet.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Sensors' }))
    expect(await screen.findByText('No sensor devices yet.')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Sensors' })).toHaveAttribute('aria-selected', 'true')
  })
})
