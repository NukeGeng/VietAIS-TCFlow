import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import App from '../App.vue'
import { createAppRouter } from '../router'
import { authSession } from '../services/auth-session'
import { useSessionStore } from '../stores/session'
import { useWorkspaceStore } from '../stores/workspace'

describe('App', () => {
  afterEach(() => {
    authSession.clear()
    vi.restoreAllMocks()
  })

  it('shows allowed project navigation and explains missing access', async () => {
    authSession.write({
      token: 'token',
      refreshToken: 'refresh',
      refreshTokenExpiryTime: '2099-01-01T00:00:00Z',
      tenant: 'root',
    })
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.profile = {
      id: '30000000-0000-0000-0000-000000000001',
      userName: 'owner',
      email: 'owner@example.com',
      isActive: true,
      emailConfirmed: true,
    }
    session.state = { status: 'ready' }

    const workspace = useWorkspaceStore()
    workspace.projects = [
      {
        id: '20000000-0000-0000-0000-000000000001',
        name: 'TCFlow',
        primaryOwnerId: session.profile.id,
        createdAt: '2026-08-20T00:00:00Z',
      },
    ]
    workspace.selectProject(workspace.projects[0]!.id)
    workspace.effectivePermissions = {
      projectId: workspace.projects[0]!.id,
      userId: session.profile.id,
      grants: [
        {
          permissionCode: 'repository.view',
          roleId: '40000000-0000-0000-0000-000000000001',
          roleName: 'Reader',
          resourceScope: 1,
          componentScopes: [],
        },
      ],
    }
    vi.spyOn(workspace, 'loadPermissions').mockResolvedValue()
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const router = createAppRouter()
    await router.push('/')
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [pinia, router] } })

    expect(wrapper.get('nav').text()).toContain('Repositories')
    expect(wrapper.find('nav a[href$="/repositories"]').exists()).toBe(true)
    expect(wrapper.get('.nav-disabled').text()).toContain('Analysis')
    expect(wrapper.get('.nav-disabled').text()).toContain('analysis.view')
  })
})
