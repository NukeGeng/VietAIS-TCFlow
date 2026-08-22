const storagePrefix = 'tcflow.github-oauth.'

interface PendingGitHubAuthorization {
  codeVerifier: string
  expiresAt: string
}

function storage(): Storage | undefined {
  return typeof window === 'undefined' ? undefined : window.sessionStorage
}

function key(state: string): string {
  return `${storagePrefix}${state}`
}

export const gitHubConnectionSession = {
  write(state: string, codeVerifier: string, expiresAt: string): void {
    storage()?.setItem(key(state), JSON.stringify({ codeVerifier, expiresAt }))
  },

  read(state: string): PendingGitHubAuthorization | null {
    const stored = storage()?.getItem(key(state))
    if (!stored) return null

    try {
      const pending = JSON.parse(stored) as PendingGitHubAuthorization
      if (!pending.codeVerifier || new Date(pending.expiresAt).getTime() <= Date.now()) {
        this.remove(state)
        return null
      }
      return pending
    } catch {
      this.remove(state)
      return null
    }
  },

  remove(state: string): void {
    storage()?.removeItem(key(state))
  },
}
