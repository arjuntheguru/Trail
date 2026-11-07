import { createRootRouteWithContext, Link, Outlet } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import type { RouterContext } from '@/router'

export const Route = createRootRouteWithContext<RouterContext>()({
  component: () => <Outlet />,
  notFoundComponent: () => (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-center">
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="text-muted-foreground">That page does not exist.</p>
      <Button asChild>
        <Link to="/today">Go to Today</Link>
      </Button>
    </div>
  ),
})
