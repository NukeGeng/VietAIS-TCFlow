import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authSession } from '../services/auth-session'
import { tcflowApi } from '../services/tcflow-api'
import { ApiError } from '../services/http'
import type { ResourceState, UserProfile } from '../types/contracts'

export const useSessionStore = defineStore('session', () => {
  const profile = ref<UserProfile | null>(null)
  const systemPermissions = ref<string[]>([])
  const state = ref<ResourceState>({ status: authSession.exists() ? 'loading' : 'idle' })
  const isAuthenticated = computed(() => authSession.exists() && profile.value !== null)
  let expiryListenerInstalled = false

  function installExpiryListener(): void {
    if (expiryListenerInstalled || typeof window === 'undefined') return
    expiryListenerInstalled = true
    window.addEventListener('tcflow:session-expired', () => {
      profile.value = null
      systemPermissions.value = []
      state.value = { status: 'error', message: 'Your session expired. Sign in again.' }
    })
  }

  async function loadIdentity(): Promise<void> {
    const [user, permissions] = await Promise.all([
      tcflowApi.profile(),
      tcflowApi.systemPermissions(),
    ])
    profile.value = user
    systemPermissions.value = permissions ?? []
    state.value = { status: 'ready' }
  }

  async function login(email: string, password: string, tenant = 'root'): Promise<void> {
    state.value = { status: 'loading' }
    try {
      const tokens = await tcflowApi.login(email.trim(), password, tenant.trim() || 'root')
      authSession.write({ ...tokens, tenant: tenant.trim() || 'root' })
      await loadIdentity()
    } catch (error) {
      authSession.clear()
      profile.value = null
      systemPermissions.value = []
      state.value = {
        status: error instanceof ApiError && error.status === 403 ? 'forbidden' : 'error',
        message: error instanceof Error ? error.message : 'Unable to sign in.',
      }
      throw error
    }
  }

  async function restore(): Promise<void> {
    installExpiryListener()
    if (!authSession.exists()) {
      state.value = { status: 'idle' }
      return
    }

    state.value = { status: 'loading' }
    try {
      await loadIdentity()
    } catch (error) {
      authSession.clear()
      profile.value = null
      systemPermissions.value = []
      state.value = {
        status: 'error',
        message: error instanceof Error ? error.message : 'Unable to restore the session.',
      }
    }
  }

  function logout(): void {
    authSession.clear()
    profile.value = null
    systemPermissions.value = []
    state.value = { status: 'idle' }
  }

  function hasSystemPermission(permission: string): boolean {
    return systemPermissions.value.includes(permission)
  }

  return {
    profile,
    systemPermissions,
    state,
    isAuthenticated,
    login,
    restore,
    logout,
    hasSystemPermission,
  }
})
