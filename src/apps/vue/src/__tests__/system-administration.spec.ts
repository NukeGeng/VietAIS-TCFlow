import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'
import { GlobalAiProviderKind, ProjectLifecycleStatus } from '../types/contracts'
import SystemAdminView from '../views/SystemAdminView.vue'
import { page } from './fixtures'

const userId = '30000000-0000-0000-0000-000000000001'
const projectId = '20000000-0000-0000-0000-000000000001'
const roleId = '40000000-0000-0000-0000-000000000001'
const providerId = '1d93ad55-f5f9-4c6a-a723-ff02f9c6eae1'
const now = '2026-08-23T00:00:00Z'

describe('system administration', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders platform resources and invokes protected lifecycle actions', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.systemPermissions = [
      'Permissions.Users.View',
      'Permissions.Users.Update',
      'Permissions.Roles.View',
      'Permissions.Roles.Create',
      'Permissions.Roles.Delete',
      'Permissions.UserRoles.Update',
      'Permissions.RoleClaims.Update',
      'project.inspect',
      'project.suspend',
      'permission-definition.manage',
      'system-audit.view',
    ]
    const user = {
      id: userId,
      userName: 'developer',
      email: 'developer@example.com',
      isActive: true,
      emailConfirmed: true,
    }
    const project = {
      project: { id: projectId, name: 'Project Alpha', primaryOwnerId: userId, createdAt: now },
      state: {
        id: projectId,
        projectId,
        status: ProjectLifecycleStatus.Active,
        updatedAt: now,
        updatedBy: userId,
      },
    }
    vi.spyOn(tcflowApi, 'users').mockResolvedValue([user])
    vi.spyOn(tcflowApi, 'systemProjects').mockResolvedValue(page([project]))
    vi.spyOn(tcflowApi, 'systemRoles').mockResolvedValue([
      { id: roleId, name: 'Admin', description: 'Built-in role' },
    ])
    vi.spyOn(tcflowApi, 'systemPermissionDefinitions').mockResolvedValue([
      { id: 'project.inspect', description: 'Inspect projects', scope: 0 },
    ])
    vi.spyOn(tcflowApi, 'systemAudit').mockResolvedValue(
      page([
        {
          id: '50000000-0000-0000-0000-000000000001',
          projectId,
          actorId: userId,
          actorType: 'system-admin',
          action: 'project.create',
          occurredAt: now,
          targetType: 'Project',
          targetId: projectId,
        },
      ]),
    )
    const toggleUser = vi.spyOn(tcflowApi, 'toggleUserStatus').mockResolvedValue()
    const updateProject = vi.spyOn(tcflowApi, 'updateSystemProjectStatus').mockResolvedValue({
      ...project,
      state: { ...project.state, status: ProjectLifecycleStatus.Suspended },
    })

    const wrapper = mount(SystemAdminView, { global: { plugins: [pinia] } })
    await flushPromises()

    expect(wrapper.text()).toContain('developer@example.com')
    expect(wrapper.text()).toContain('Project Alpha')
    expect(wrapper.text()).toContain('Admin')
    expect(wrapper.text()).toContain('project.inspect')
    expect(wrapper.text()).toContain('project.create')
    expect(wrapper.text()).not.toContain('Delete')

    const userRow = wrapper
      .findAll('.user-table article')
      .find((row) => row.text().includes('developer@example.com'))
    await userRow!.find('button').trigger('click')
    await flushPromises()
    expect(toggleUser).toHaveBeenCalledWith(userId, false)

    const projectRow = wrapper
      .findAll('.audit-table article')
      .find((row) => row.text().includes('Project Alpha'))
    await projectRow!.find('button').trigger('click')
    await flushPromises()
    expect(updateProject).toHaveBeenCalledWith(projectId, ProjectLifecycleStatus.Suspended)
  })

  it('keeps allowed user administration visible when project access is forbidden', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.systemPermissions = ['Permissions.Users.View']
    vi.spyOn(tcflowApi, 'users').mockResolvedValue([
      {
        id: userId,
        userName: 'viewer',
        email: 'viewer@example.com',
        isActive: true,
        emailConfirmed: true,
      },
    ])
    const systemProjects = vi.spyOn(tcflowApi, 'systemProjects')

    const wrapper = mount(SystemAdminView, { global: { plugins: [pinia] } })
    await flushPromises()

    expect(wrapper.text()).toContain('viewer@example.com')
    expect(wrapper.text()).toContain('Requires project.inspect.')
    expect(systemProjects).not.toHaveBeenCalled()
  })

  it('manages global AI, settings, policies, and usage behind independent permissions', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.systemPermissions = [
      'ai-provider.manage',
      'system-settings.manage',
      'platform-policy.manage',
      'platform-usage.view',
      'system-audit.view',
    ]
    const provider = {
      id: providerId,
      kind: GlobalAiProviderKind.CodexAppServer,
      displayName: 'Codex App Server',
      isEnabled: true,
      updatedAt: now,
      updatedBy: userId,
    }
    const settings = {
      id: '1d93ad55-f5f9-4c6a-a723-ff02f9c6eae2',
      platformName: 'VietAIS TCFlow',
      defaultTimeZone: 'UTC',
      supportUrl: 'https://support.example.com',
      updatedAt: now,
      updatedBy: userId,
    }
    const policy = {
      id: '1d93ad55-f5f9-4c6a-a723-ff02f9c6eae3',
      projectCreationEnabled: true,
      repositoryConnectionsEnabled: true,
      maximumRepositoriesPerProject: 20,
      updatedAt: now,
      updatedBy: userId,
    }
    vi.spyOn(tcflowApi, 'systemAiProviders').mockResolvedValue([provider])
    vi.spyOn(tcflowApi, 'globalSystemSettings').mockResolvedValue(settings)
    vi.spyOn(tcflowApi, 'platformPolicy').mockResolvedValue(policy)
    vi.spyOn(tcflowApi, 'systemUsage').mockResolvedValue({
      projects: 4,
      activeProjects: 3,
      suspendedProjects: 1,
      repositories: 7,
      activeRepositories: 6,
      tasks: 12,
      aiGeneratedTasks: 5,
      auditRecords: 30,
    })
    vi.spyOn(tcflowApi, 'systemAudit').mockResolvedValue(page([]))
    const updateProvider = vi.spyOn(tcflowApi, 'updateSystemAiProvider').mockResolvedValue(provider)
    const updateSettings = vi
      .spyOn(tcflowApi, 'updateGlobalSystemSettings')
      .mockResolvedValue(settings)
    const updatePolicy = vi.spyOn(tcflowApi, 'updatePlatformPolicy').mockResolvedValue(policy)

    const wrapper = mount(SystemAdminView, { global: { plugins: [pinia] } })
    await flushPromises()

    expect(wrapper.text()).toContain('AI Providers')
    expect(wrapper.text()).toContain('Codex App Server')
    const platformNameInput = wrapper
      .findAll('label')
      .find((label) => label.text().includes('Platform name'))!
      .find('input')
    expect((platformNameInput.element as HTMLInputElement).value).toBe('VietAIS TCFlow')
    expect(wrapper.text()).toContain('Resource guardrails')
    expect(wrapper.text()).toContain('Platform resource usage')
    expect(wrapper.text()).toContain('30')

    const providerForm = wrapper
      .findAll('form')
      .find((form) => form.text().includes('Save AI provider'))
    const settingsForm = wrapper
      .findAll('form')
      .find((form) => form.text().includes('Save global settings'))
    const policyForm = wrapper
      .findAll('form')
      .find((form) => form.text().includes('Save platform policies'))
    await providerForm!.trigger('submit')
    await settingsForm!.trigger('submit')
    await policyForm!.trigger('submit')
    await flushPromises()

    expect(updateProvider).toHaveBeenCalledWith(provider)
    expect(updateSettings).toHaveBeenCalledWith(settings)
    expect(updatePolicy).toHaveBeenCalledWith(policy)
  })
})
