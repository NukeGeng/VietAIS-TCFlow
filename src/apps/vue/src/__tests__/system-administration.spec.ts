import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'
import { ProjectLifecycleStatus } from '../types/contracts'
import SystemAdminView from '../views/SystemAdminView.vue'
import { page } from './fixtures'

const userId = '30000000-0000-0000-0000-000000000001'
const projectId = '20000000-0000-0000-0000-000000000001'
const roleId = '40000000-0000-0000-0000-000000000001'
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
})
