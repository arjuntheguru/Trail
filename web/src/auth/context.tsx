import { createContext, useContext, useSyncExternalStore, type ReactNode } from 'react'

import type { AuthService } from './auth-service'
import { hasPermission } from './permissions'
import type { AuthState, Session } from './session-store'

const AuthContext = createContext<AuthService | null>(null)

export function AuthProvider({ service, children }: { service: AuthService; children: ReactNode }) {
  return <AuthContext.Provider value={service}>{children}</AuthContext.Provider>
}

export function useAuthService(): AuthService {
  const service = useContext(AuthContext)
  if (!service) throw new Error('useAuthService must be used inside <AuthProvider>.')
  return service
}

export function useAuthState(): AuthState {
  const { store } = useAuthService()
  return useSyncExternalStore(store.subscribe, store.getState, store.getState)
}

/** The signed-in session. Only call below the authenticated layout, where one is guaranteed. */
export function useSession(): Session {
  const state = useAuthState()
  if (state.status !== 'authenticated') throw new Error('useSession requires a signed-in user.')
  return state.session
}

/** Whether the active membership holds the permission. False while signed out. */
export function usePermission(permission: string): boolean {
  const state = useAuthState()
  return hasPermission(state.session?.permissions, permission)
}
