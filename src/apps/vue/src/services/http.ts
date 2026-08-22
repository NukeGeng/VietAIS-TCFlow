import { authSession } from './auth-session'
import type { ApiProblem, TokenResponse } from '../types/contracts'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly problem?: ApiProblem,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

interface RequestOptions extends RequestInit {
  authenticated?: boolean
  retryOnUnauthorized?: boolean
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T

  const text = await response.text()
  const body = text ? (JSON.parse(text) as T | ApiProblem) : undefined
  if (response.ok) return body as T

  const problem = body as ApiProblem | undefined
  const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : undefined
  throw new ApiError(
    response.status,
    validation ||
      problem?.detail ||
      problem?.title ||
      `Request failed with status ${response.status}.`,
    problem,
  )
}

async function refreshSession(): Promise<boolean> {
  const current = authSession.read()
  if (!current?.refreshToken || new Date(current.refreshTokenExpiryTime).getTime() <= Date.now()) {
    return false
  }

  const response = await fetch(`${baseUrl}/api/token/refresh`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      tenant: current.tenant,
    },
    body: JSON.stringify({ token: current.token, refreshToken: current.refreshToken }),
  })
  if (!response.ok) return false

  const tokens = await parseResponse<TokenResponse>(response)
  authSession.write({ ...tokens, tenant: current.tenant })
  return true
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { authenticated = true, retryOnUnauthorized = true, headers, ...init } = options
  const session = authSession.read()
  const requestHeaders = new Headers(headers)
  requestHeaders.set('Accept', 'application/json')
  if (init.body && !requestHeaders.has('Content-Type'))
    requestHeaders.set('Content-Type', 'application/json')
  if (authenticated && session) {
    requestHeaders.set('Authorization', `Bearer ${session.token}`)
    requestHeaders.set('tenant', session.tenant)
  }

  const response = await fetch(`${baseUrl}${path}`, { ...init, headers: requestHeaders })
  if (response.status === 401 && authenticated && retryOnUnauthorized && (await refreshSession())) {
    return apiRequest<T>(path, { ...options, retryOnUnauthorized: false })
  }

  if (response.status === 401 && authenticated) {
    authSession.clear()
    if (typeof window !== 'undefined') window.dispatchEvent(new Event('tcflow:session-expired'))
  }

  return parseResponse<T>(response)
}

export function queryString(values: Record<string, string | number | undefined | null>): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && value !== '') query.set(key, String(value))
  }
  const serialized = query.toString()
  return serialized ? `?${serialized}` : ''
}
