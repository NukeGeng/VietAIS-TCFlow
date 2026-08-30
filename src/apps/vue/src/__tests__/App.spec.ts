import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import App from '../App.vue'
import { createAppRouter } from '../router'
import { authSession } from '../services/auth-session'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'
import { useWorkspaceStore } from '../stores/workspace'
import { page } from './fixtures'

describe('App', () => {
  afterEach(() => {
    authSession.clear()
    window.localStorage.clear()
    document.documentElement.lang = 'vi'
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
        {
          permissionCode: 'feature.view',
          roleId: '40000000-0000-0000-0000-000000000001',
          roleName: 'Reader',
          resourceScope: 1,
          componentScopes: [],
        },
        {
          permissionCode: 'component.view',
          roleId: '40000000-0000-0000-0000-000000000001',
          roleName: 'Reader',
          resourceScope: 1,
          componentScopes: [],
        },
      ],
    }
    workspace.permissionsState = { status: 'ready' }
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const router = createAppRouter()
    await router.push('/')
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [pinia, router] } })

    expect(wrapper.get('nav').text()).toContain('Kho mã nguồn')
    expect(wrapper.find('nav a[href$="/repositories"]').exists()).toBe(true)
    expect(wrapper.find('nav a[href$="/features"]').text()).toContain('Tính năng')
    expect(wrapper.find('nav a[href$="/admin"]').text()).toContain('Quản trị dự án')
    expect(wrapper.get('.nav-disabled').text()).toContain('Phân tích')
    expect(wrapper.get('.nav-disabled').text()).toContain('analysis.view')
    const languageSelect = wrapper.get('.language-switcher select')
    expect((languageSelect.element as HTMLSelectElement).value).toBe('vi')
    expect(languageSelect.findAll('option').map((option) => option.text())).toEqual([
      'Tiếng Việt',
      'English',
    ])

    await languageSelect.setValue('en')
    expect(wrapper.get('nav').text()).toContain('Repositories')
    expect(document.documentElement.lang).toBe('en')
  })

  it('hydrates the project sidebar when opening a project deep-link', async () => {
    authSession.clear()
    window.sessionStorage.removeItem('tcflow.selected-project')
    authSession.write({
      token: 'token',
      refreshToken: 'refresh',
      refreshTokenExpiryTime: '2099-01-01T00:00:00Z',
      tenant: 'root',
    })

    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    const userId = '30000000-0000-0000-0000-000000000001'
    const project = {
      id: '20000000-0000-0000-0000-000000000001',
      name: 'Portfolio',
      primaryOwnerId: userId,
      createdAt: '2026-08-20T00:00:00Z',
    }
    session.profile = {
      id: userId,
      userName: 'owner',
      email: 'owner@example.com',
      isActive: true,
      emailConfirmed: true,
    }
    session.state = { status: 'ready' }

    const workspace = useWorkspaceStore()
    vi.spyOn(tcflowApi, 'projects').mockResolvedValue(page([project]))
    vi.spyOn(tcflowApi, 'effectivePermissions').mockResolvedValue({
      projectId: project.id,
      userId,
      grants: [
        {
          permissionCode: 'task.view',
          roleId: '40000000-0000-0000-0000-000000000001',
          roleName: 'Owner',
          resourceScope: 1,
          componentScopes: [],
        },
      ],
    })
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const router = createAppRouter()
    await router.push(`/projects/${project.id}/tasks`)
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [pinia, router] } })

    await vi.waitFor(() => expect(wrapper.get('.sidebar-project').text()).toContain('Portfolio'))
    expect(wrapper.get('.nav-label').text()).toContain('Không gian làm việc')
    expect(wrapper.find('nav a[href$="/tasks"]').exists()).toBe(true)
    expect(workspace.selectedProjectId).toBe(project.id)
  })

  it('unlocks project navigation after the first project selection without a reload', async () => {
    authSession.write({
      token: 'token',
      refreshToken: 'refresh',
      refreshTokenExpiryTime: '2099-01-01T00:00:00Z',
      tenant: 'root',
    })
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    const userId = '30000000-0000-0000-0000-000000000001'
    session.profile = {
      id: userId,
      userName: 'owner',
      email: 'owner@example.com',
      isActive: true,
      emailConfirmed: true,
    }
    session.state = { status: 'ready' }

    const workspace = useWorkspaceStore()
    const project = {
      id: '20000000-0000-0000-0000-000000000001',
      name: 'First project',
      primaryOwnerId: userId,
      createdAt: '2026-08-20T00:00:00Z',
    }
    workspace.projects = [project]
    const effectivePermissions = vi.spyOn(tcflowApi, 'effectivePermissions').mockResolvedValue({
      projectId: project.id,
      userId,
      grants: [
        {
          permissionCode: 'task.view',
          roleId: '40000000-0000-0000-0000-000000000001',
          roleName: 'Owner',
          resourceScope: 1,
          componentScopes: [],
        },
      ],
    })
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()

    const router = createAppRouter()
    await router.push('/projects')
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [pinia, router] } })

    await wrapper.get('.list-row').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.find(`nav a[href="/projects/${project.id}/tasks"]`).exists()).toBe(true)
      expect(router.currentRoute.value.fullPath).toBe(`/projects/${project.id}/tasks`)
    })
    expect(effectivePermissions).toHaveBeenCalledWith(project.id, userId)
    expect(wrapper.get('.sidebar-project').text()).toContain('First project')
  })
})
