import type { TokenResponse } from '../types/contracts'

const storageKey = 'tcflow.auth'

export interface StoredAuthSession extends TokenResponse {
  tenant: string
}

function storage(): Storage | undefined {
  return typeof window === 'undefined' ? undefined : window.sessionStorage
}

export const authSession = {
  read(): StoredAuthSession | null {
    const value = storage()?.getItem(storageKey)
    if (!value) return null

    try {
      return JSON.parse(value) as StoredAuthSession
    } catch {
      storage()?.removeItem(storageKey)
      return null
    }
  },

  write(value: StoredAuthSession): void {
    storage()?.setItem(storageKey, JSON.stringify(value))
  },

  clear(): void {
    storage()?.removeItem(storageKey)
  },

  exists(): boolean {
    return this.read() !== null
  },
}
