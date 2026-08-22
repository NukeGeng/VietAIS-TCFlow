import { afterEach, describe, expect, it, vi } from 'vitest'
import { authSession } from '../services/auth-session'
import { apiRequest } from '../services/http'

describe('authenticated HTTP client', () => {
  afterEach(() => {
    authSession.clear()
    vi.unstubAllGlobals()
  })

  it('refreshes an expired access token once and retries the original request', async () => {
    authSession.write({
      token: 'old-token',
      refreshToken: 'refresh-token',
      refreshTokenExpiryTime: '2099-01-01T00:00:00Z',
      tenant: 'root',
    })
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ title: 'Unauthorized', status: 401 }), {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            token: 'new-token',
            refreshToken: 'new-refresh-token',
            refreshTokenExpiryTime: '2099-02-01T00:00:00Z',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ id: 'project-id', name: 'TCFlow' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
    vi.stubGlobal('fetch', fetchMock)

    const project = await apiRequest<{ id: string; name: string }>('/api/v1/projects/project-id')

    expect(project.name).toBe('TCFlow')
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(authSession.read()?.token).toBe('new-token')
    const retriedHeaders = fetchMock.mock.calls[2]?.[1]?.headers as Headers
    expect(retriedHeaders.get('Authorization')).toBe('Bearer new-token')
    expect(retriedHeaders.get('tenant')).toBe('root')
  })
})
