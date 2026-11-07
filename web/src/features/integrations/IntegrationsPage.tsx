import { getRouteApi } from '@tanstack/react-router'
import { RequirePermission } from '@/features/settings/shared'
import { cn } from '@/lib/utils'

import { ApiKeysTab } from './ApiKeysTab'
import { INTEGRATIONS_PERMISSION } from './queries'
import { TAB_LABEL, TABS } from './tabs'
import { SensorsTab } from './SensorsTab'
import { WebhooksTab } from './WebhooksTab'

const route = getRouteApi('/_authenticated/settings/integrations')

/** Every tab needs integrations.manage, which is also what the API checks on each endpoint. */
export function IntegrationsPage() {
  return (
    <RequirePermission permission={INTEGRATIONS_PERMISSION}>
      <IntegrationsTabs />
    </RequirePermission>
  )
}

function IntegrationsTabs() {
  const tab = route.useSearch({ select: (s) => s.tab ?? 'webhooks' })
  const navigate = route.useNavigate()
  return (
    <div className="flex flex-col gap-4">
      <div role="tablist" aria-label="Integrations" className="flex gap-1 border-b">
        {TABS.map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            id={`tab-${key}`}
            aria-selected={tab === key}
            aria-controls={`panel-${key}`}
            className={cn(
              '-mb-px h-11 border-b-2 px-4 text-base font-medium whitespace-nowrap md:text-sm',
              tab === key
                ? 'border-primary text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
            onClick={() => void navigate({ search: { tab: key }, replace: true })}
          >
            {TAB_LABEL[key]}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
        {tab === 'webhooks' && <WebhooksTab />}
        {tab === 'api-keys' && <ApiKeysTab />}
        {tab === 'sensors' && <SensorsTab />}
      </div>
    </div>
  )
}
