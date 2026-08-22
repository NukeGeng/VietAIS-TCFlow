import { afterEach, describe, expect, it } from 'vitest'
import { createAppRouter } from '../router'
import { authSession } from '../services/auth-session'

describe('router authentication boundary', () => {
  afterEach(() => authSession.clear())

  it('redirects an unauthenticated deep link to sign in and preserves its destination', async () => {
    const router = createAppRouter()
    await router.push('/projects/project-id/tasks')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/projects/project-id/tasks')
  })
})
