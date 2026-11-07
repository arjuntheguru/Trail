import { keepPreviousData, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'
import { Permissions } from '@/auth/permissions'

export type Webhook = components['schemas']['WebhookResponse']
export type WebhookDelivery = components['schemas']['WebhookDeliveryResponse']
export type WebhookWithSecret = components['schemas']['WebhookWithSecretResponse']
export type ApiKey = components['schemas']['ApiKeyResponse']
export type ApiKeyCreated = components['schemas']['ApiKeyCreatedResponse']
export type SensorDevice = components['schemas']['SensorDeviceResponse']

/** Permission that opens every integrations tab (Permissions.Integrations.Manage in the API). */
export const INTEGRATIONS_PERMISSION = Permissions.Integrations.Manage

export const DELIVERIES_PAGE_SIZE = 10
export const DEVICES_PAGE_SIZE = 25

type OrgId = string | undefined
const base = (organizationId: OrgId) => ['org', organizationId, 'integrations'] as const

/** Area names for `useSettingsMutation` / `ConfirmDialog` (they prefix `['org', orgId]`). */
export const AREAS = {
  webhooks: ['integrations', 'webhooks'],
  deliveries: ['integrations', 'deliveries'],
  apiKeys: ['integrations', 'api-keys'],
  devices: ['integrations', 'sensor-devices'],
} as const

export const webhooksQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'webhooks'],
    queryFn: () => unwrap(client.GET('/api/v1/webhooks')),
  })

export const webhookEventTypesQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'webhook-event-types'],
    staleTime: Infinity,
    queryFn: () => unwrap(client.GET('/api/v1/webhooks/event-types')),
  })

export const deliveriesQuery = (
  client: ApiClient,
  organizationId: OrgId,
  webhookId: string,
  page: number,
) =>
  queryOptions({
    queryKey: [...base(organizationId), 'deliveries', webhookId, page],
    placeholderData: keepPreviousData,
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/webhooks/{id}/deliveries', {
          params: {
            path: { id: webhookId },
            query: { Page: page, PageSize: DELIVERIES_PAGE_SIZE },
          },
        }),
      ),
  })

export const apiKeysQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'api-keys'],
    queryFn: () => unwrap(client.GET('/api/v1/api-keys')),
  })

export const sensorDevicesQuery = (client: ApiClient, organizationId: OrgId, page: number) =>
  queryOptions({
    queryKey: [...base(organizationId), 'sensor-devices', page],
    placeholderData: keepPreviousData,
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/sensor-devices', {
          params: { query: { Page: page, PageSize: DEVICES_PAGE_SIZE } },
        }),
      ),
  })

/** Record-log templates a sensor can feed. */
export const recordLogTemplatesQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'record-log-templates'],
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/templates', {
          params: { query: { Kind: 'RecordLog', Page: 1, PageSize: 100 } },
        }),
      ),
  })

export const templateDetailQuery = (client: ApiClient, organizationId: OrgId, id: string) =>
  queryOptions({
    queryKey: [...base(organizationId), 'template', id],
    queryFn: () => unwrap(client.GET('/api/v1/templates/{id}', { params: { path: { id } } })),
  })

/** Scopes the API knows (ApiKeyScopes.All). */
export const API_KEY_SCOPES = [{ value: 'sensors.write', label: 'Send sensor readings' }] as const
