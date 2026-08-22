import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { authSession } from '../services/auth-session'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'

describe('session store', () => {
  afterEach(() => {
    authSession.clear()
    vi.restoreAllMocks()
  })

  it('establishes and clears a FullStackHero token session', async () => {
    setActivePinia(createPinia())
    vi.spyOn(tcflowApi, 'login').mockResolvedValue({
      token: 'access-token',
      refreshToken: 'refresh-token',
      refreshTokenExpiryTime: '2099-01-01T00:00:00Z',
    })
    vi.spyOn(tcflowApi, 'profile').mockResolvedValue({
      id: '30000000-0000-0000-0000-000000000001',
      userName: 'owner',
      isActive: true,
      emailConfirmed: true,
    })
    vi.spyOn(tcflowApi, 'systemPermissions').mockResolvedValue(['Permissions.Users.View'])

    const session = useSessionStore()
    await session.login('owner@example.com', 'password', 'root')

    expect(session.isAuthenticated).toBe(true)
    expect(authSession.read()?.token).toBe('access-token')
    expect(session.hasSystemPermission('Permissions.Users.View')).toBe(true)

    session.logout()
    expect(session.isAuthenticated).toBe(false)
    expect(authSession.read()).toBeNull()
  })
})
