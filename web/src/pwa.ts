import { registerSW } from 'virtual:pwa-register'
import { toast } from 'sonner'

/** Registers the service worker and offers a reload when a new build has been downloaded. */
export function registerServiceWorker() {
  const update = registerSW({
    onNeedRefresh() {
      toast('A new version of Trail is available', {
        duration: Infinity,
        action: { label: 'Reload', onClick: () => void update(true) },
      })
    },
  })
}
