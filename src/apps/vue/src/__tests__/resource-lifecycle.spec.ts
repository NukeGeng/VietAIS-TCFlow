import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useWorkspaceStore } from '../stores/workspace'
import {
  RepositoryLifecycleStatus,
  RepositoryProviderKind,
  type PermissionGrantTrace,
} from '../types/contracts'
import FeaturesView from '../views/FeaturesView.vue'
import RepositoriesView from '../views/RepositoriesView.vue'

const projectId = '20000000-0000-0000-0000-000000000001'
const actorId = '30000000-0000-0000-0000-000000000001'
const now = '2026-08-23T00:00:00Z'

describe('project resource lifecycle views', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('edits and deletes features only when the matching permissions are granted', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'TCFlow', primaryOwnerId: actorId, createdAt: now },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = permissions(['feature.update', 'feature.delete'])
    workspace.features = [
      {
        id: '40000000-0000-0000-0000-000000000001',
        projectId,
        name: 'Analysis',
        description: 'Old description',
        createdAt: now,
        createdBy: actorId,
      },
    ]
    workspace.featuresState = { status: 'ready' }
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadFeatures').mockResolvedValue()
    const update = vi.spyOn(workspace, 'updateFeature').mockResolvedValue()
    const remove = vi.spyOn(workspace, 'deleteFeature').mockResolvedValue()
    vi.stubGlobal(
      'confirm',
      vi.fn(() => true),
    )
    const wrapper = mount(FeaturesView, { global: { plugins: [pinia] } })

    await wrapper.get('button.secondary-button').trigger('click')
    const editor = wrapper.get('form.resource-editor')
    await editor.get('input').setValue('Repository analysis')
    await editor.trigger('submit')
    expect(update).toHaveBeenCalledWith(
      '40000000-0000-0000-0000-000000000001',
      'Repository analysis',
      'Old description',
    )

    await wrapper.get('button.danger-button').trigger('click')
    expect(remove).toHaveBeenCalledWith('40000000-0000-0000-0000-000000000001')
  })

  it('does not expose feature lifecycle actions without backend grants', () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'TCFlow', primaryOwnerId: actorId, createdAt: now },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = permissions([])
    workspace.features = [
      {
        id: '40000000-0000-0000-0000-000000000001',
        projectId,
        name: 'Analysis',
        createdAt: now,
        createdBy: actorId,
      },
    ]
    workspace.featuresState = { status: 'ready' }
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(workspace, 'loadFeatures').mockResolvedValue()

    const wrapper = mount(FeaturesView, { global: { plugins: [pinia] } })

    expect(wrapper.findAll('button').some((button) => button.text() === 'Edit')).toBe(false)
    expect(wrapper.findAll('button').some((button) => button.text() === 'Delete')).toBe(false)
  })

  it('edits and disables repositories while preserving the lifecycle status contract', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'TCFlow', primaryOwnerId: actorId, createdAt: now },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = permissions(['repository.update', 'repository.delete'])
    workspace.repositories = [
      {
        id: '50000000-0000-0000-0000-000000000001',
        projectId,
        name: 'Backend',
        provider: RepositoryProviderKind.Local,
        localPath: '/workspace/backend',
        defaultBranch: 'main',
        status: RepositoryLifecycleStatus.Active,
        createdAt: now,
        createdBy: actorId,
      },
    ]
    workspace.repositoriesState = { status: 'ready' }
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()
    const update = vi.spyOn(workspace, 'updateRepository').mockResolvedValue()
    const disable = vi.spyOn(workspace, 'disableRepository').mockResolvedValue()
    vi.stubGlobal(
      'confirm',
      vi.fn(() => true),
    )
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/projects/:projectId/repositories', component: RepositoriesView }],
    })
    await router.push(`/projects/${projectId}/repositories`)
    await router.isReady()
    const wrapper = mount(RepositoriesView, { global: { plugins: [pinia, router] } })

    await wrapper.get('button.secondary-button').trigger('click')
    const editor = wrapper.get('form.resource-editor')
    const inputs = editor.findAll('input')
    await inputs[0]!.setValue('Backend API')
    await inputs[2]!.setValue('develop')
    await editor.trigger('submit')
    expect(update).toHaveBeenCalledWith('50000000-0000-0000-0000-000000000001', {
      name: 'Backend API',
      localPath: '/workspace/backend',
      remoteUrl: undefined,
      defaultBranch: 'develop',
      status: RepositoryLifecycleStatus.Active,
    })

    await wrapper.get('button.danger-button').trigger('click')
    expect(disable).toHaveBeenCalledWith('50000000-0000-0000-0000-000000000001')
  })
})

function permissions(permissionCodes: string[]): {
  projectId: string
  userId: string
  grants: PermissionGrantTrace[]
} {
  return {
    projectId,
    userId: actorId,
    grants: permissionCodes.map((permissionCode) => ({
      permissionCode,
      roleId: '60000000-0000-0000-0000-000000000001',
      roleName: 'Owner',
      resourceScope: 1,
      componentScopes: [],
    })),
  }
}
