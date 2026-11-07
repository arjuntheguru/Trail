import type { components } from '@/api/schema'

type Webhook = components['schemas']['WebhookResponse']
type Delivery = components['schemas']['WebhookDeliveryResponse']
type ApiKey = components['schemas']['ApiKeyResponse']
type Device = components['schemas']['SensorDeviceResponse']
type AuditEntry = components['schemas']['AuditEntryResponse']

let counter = 0
const nextId = () => `00000000-0000-4000-8000-${String(++counter).padStart(12, '0')}`

export const EVENT_TYPES = ['task.completed', 'issue.raised', 'issue.resolved']

export const webhook = (overrides: Partial<Webhook> = {}): Webhook => ({
  id: nextId(),
  url: 'https://hooks.example.com/trail',
  description: null,
  eventTypes: ['task.completed'],
  isActive: true,
  failureCount: 0,
  disabledAt: null,
  lastSuccessAt: null,
  lastFailureAt: null,
  createdAt: '2026-10-01T09:00:00Z',
  ...overrides,
})

export const delivery = (overrides: Partial<Delivery> = {}): Delivery => ({
  id: nextId(),
  eventId: nextId(),
  eventType: 'task.completed',
  status: 'Succeeded',
  attempts: 1,
  nextAttemptAt: '2026-10-02T09:00:00Z',
  lastStatusCode: 200,
  lastError: null,
  deliveredAt: '2026-10-02T09:00:01Z',
  createdAt: '2026-10-02T09:00:00Z',
  ...overrides,
})

export const apiKey = (overrides: Partial<ApiKey> = {}): ApiKey => ({
  id: nextId(),
  name: 'Gateway',
  prefix: 'trk_ab12',
  scopes: ['sensors.write'],
  siteIds: [],
  expiresAt: null,
  lastUsedAt: null,
  revokedAt: null,
  createdAt: '2026-10-01T09:00:00Z',
  ...overrides,
})

export const device = (overrides: Partial<Device> = {}): Device => ({
  id: nextId(),
  externalId: 'fridge-01',
  name: 'Walk-in fridge',
  siteId: 'site-1',
  templateId: 'tpl-1',
  fieldKey: 'temp',
  lastReadingAt: null,
  lastValue: null,
  ...overrides,
})

export const auditEntry = (overrides: Partial<AuditEntry> = {}): AuditEntry => ({
  id: nextId(),
  occurredAt: '2026-11-01T10:00:00Z',
  entityType: 'Site',
  entityId: '11111111-2222-4333-8444-555555555555',
  action: 'Updated',
  changes: { name: { old: 'Harbour', new: 'Harbour Cafe' } },
  actorId: 'u-1',
  actorName: 'Sam Chef',
  siteId: null,
  ipAddress: null,
  traceId: null,
  ...overrides,
})
