import { act, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { sessionResponse } from '@/test/fixtures'
import { createTestAuth } from '@/test/renderApp'

import { AuthProvider, usePermission } from './context'
import { hasPermission, Permissions } from './permissions'
import { toSession } from './session-store'

function Probe({ permission }: { permission: string }) {
  return <p>{usePermission(permission) ? 'allowed' : 'denied'}</p>
}

describe('usePermission', () => {
  it('reflects the active membership permissions and updates when they change', () => {
    const auth = createTestAuth()
    auth.store.setSession(toSession(sessionResponse({ permissions: [Permissions.Tasks.View] })))
    render(
      <AuthProvider service={auth}>
        <Probe permission={Permissions.Tasks.View} />
        <Probe permission={Permissions.Reports.View} />
      </AuthProvider>,
    )
    expect(screen.getAllByText('allowed')).toHaveLength(1)
    expect(screen.getAllByText('denied')).toHaveLength(1)

    // e.g. after switching to an organization where the user is an administrator
    act(() => {
      auth.store.setSession(
        toSession(
          sessionResponse({ permissions: [Permissions.Tasks.View, Permissions.Reports.View] }),
        ),
      )
    })
    expect(screen.getAllByText('allowed')).toHaveLength(2)
  })

  it('is false while signed out', () => {
    const auth = createTestAuth()
    auth.store.clear()
    render(
      <AuthProvider service={auth}>
        <Probe permission={Permissions.Tasks.View} />
      </AuthProvider>,
    )
    expect(screen.getByText('denied')).toBeInTheDocument()
  })
})

describe('hasPermission', () => {
  it('matches exact strings only', () => {
    expect(hasPermission(['tasks.view'], 'tasks.view')).toBe(true)
    expect(hasPermission(['tasks.view'], 'tasks')).toBe(false)
    expect(hasPermission(['tasks.view'], 'tasks.view.all')).toBe(false)
    expect(hasPermission(undefined, 'tasks.view')).toBe(false)
  })
})
