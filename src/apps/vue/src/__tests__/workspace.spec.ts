import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tcflowApi } from '../services/tcflow-api'
import { useWorkspaceStore } from '../stores/workspace'
import {
  AiTrustLevel,
  AuthorityKnowledgeKind,
  AuthoritySourceKind,
  ComponentScopeKind,
  ConventionProfileStatus,
  TaskLifecycleStatus,
} from '../types/contracts'
import { page, taskFixture } from './fixtures'

describe('workspace store', () => {
  afterEach(() => vi.restoreAllMocks())

  it('reloads the board from backend state after a task transition', async () => {
    setActivePinia(createPinia())
    const before = taskFixture()
    const after = taskFixture({
      status: TaskLifecycleStatus.ReadyForReview,
      currentVersion: 2,
    })
    vi.spyOn(tcflowApi, 'tasks')
      .mockResolvedValueOnce(page([before]))
      .mockResolvedValueOnce(page([after]))
    const transition = vi.spyOn(tcflowApi, 'transitionTask').mockResolvedValue(after)

    const workspace = useWorkspaceStore()
    workspace.selectProject(before.projectId)
    await workspace.loadTasks()
    await workspace.transitionTask(before.id, TaskLifecycleStatus.ReadyForReview)

    expect(transition).toHaveBeenCalledWith(
      before.projectId,
      before.id,
      TaskLifecycleStatus.ReadyForReview,
      undefined,
    )
    expect(tcflowApi.tasks).toHaveBeenCalledTimes(2)
    expect(workspace.tasks[0]?.status).toBe(TaskLifecycleStatus.ReadyForReview)
    expect(workspace.tasks[0]?.currentVersion).toBe(2)
  })

  it('loads persisted project administration resources from the backend', async () => {
    setActivePinia(createPinia())
    const projectId = '20000000-0000-0000-0000-000000000001'
    const userId = '30000000-0000-0000-0000-000000000001'
    const roleId = '40000000-0000-0000-0000-000000000001'
    const now = '2026-08-23T00:00:00Z'
    const permissionCodes = [
      'role.view',
      'member.view',
      'component.view',
      'ai.policy.update',
      'authority.view',
      'convention.view',
      'audit.view',
    ]
    vi.spyOn(tcflowApi, 'permissionDefinitions').mockResolvedValue([
      {
        id: 'task.view',
        description: 'View tasks',
        scope: 1,
        allowedResourceScopes: [1],
        allowedComponentScopes: [ComponentScopeKind.Backend],
      },
    ])
    vi.spyOn(tcflowApi, 'roles').mockResolvedValue([
      {
        id: roleId,
        projectId,
        name: 'Maintainer',
        isSystemDefined: false,
        isOwner: false,
        permissions: [],
      },
    ])
    vi.spyOn(tcflowApi, 'members').mockResolvedValue([
      { id: '50000000-0000-0000-0000-000000000001', projectId, userId, isActive: true, roles: [] },
    ])
    vi.spyOn(tcflowApi, 'components').mockResolvedValue(
      page([
        {
          id: '60000000-0000-0000-0000-000000000001',
          projectId,
          repositoryId: '70000000-0000-0000-0000-000000000001',
          name: 'API',
          scope: ComponentScopeKind.Backend,
          createdAt: now,
          createdBy: userId,
        },
      ]),
    )
    vi.spyOn(tcflowApi, 'aiPolicy').mockResolvedValue({
      id: '80000000-0000-0000-0000-000000000001',
      projectId,
      trustLevel: AiTrustLevel.SuggestOnly,
      allowedPermissions: ['ai.analysis.run'],
      updatedBy: userId,
      updatedAt: now,
    })
    vi.spyOn(tcflowApi, 'authorityPolicy').mockResolvedValue({
      id: '90000000-0000-0000-0000-000000000001',
      projectId,
      rules: [
        { knowledge: AuthorityKnowledgeKind.ApiContract, source: AuthoritySourceKind.Backend },
      ],
      updatedAt: now,
      updatedBy: userId,
    })
    vi.spyOn(tcflowApi, 'conventionProfile').mockResolvedValue({
      id: 'a0000000-0000-0000-0000-000000000001',
      projectId,
      status: ConventionProfileStatus.Confirmed,
      architectures: ['vertical slices'],
      apiStyles: ['minimal APIs'],
      persistencePatterns: ['Marten'],
      validationPatterns: ['FluentValidation'],
      dtoPatterns: ['records'],
      updatedAt: now,
      updatedBy: userId,
    })
    vi.spyOn(tcflowApi, 'audit').mockResolvedValue([])

    const workspace = useWorkspaceStore()
    workspace.selectProject(projectId)
    workspace.effectivePermissions = {
      projectId,
      userId,
      grants: permissionCodes.map((permissionCode) => ({
        permissionCode,
        roleId,
        roleName: 'Owner',
        resourceScope: 1,
        componentScopes: [],
      })),
    }

    await workspace.loadAdministration()

    expect(workspace.administrationState.status).toBe('ready')
    expect(workspace.projectRoles[0]?.name).toBe('Maintainer')
    expect(workspace.projectMembers[0]?.userId).toBe(userId)
    expect(workspace.components[0]?.name).toBe('API')
    expect(workspace.aiPolicy?.allowedPermissions).toEqual(['ai.analysis.run'])
    expect(workspace.authorityPolicy?.rules[0]?.source).toBe(AuthoritySourceKind.Backend)
    expect(workspace.conventionProfile?.architectures).toEqual(['vertical slices'])
  })

  it('reloads persisted features after creation', async () => {
    setActivePinia(createPinia())
    const projectId = '20000000-0000-0000-0000-000000000001'
    const feature = {
      id: '40000000-0000-0000-0000-000000000001',
      projectId,
      name: 'Repository analysis',
      description: 'Produce traceable tasks.',
      createdAt: '2026-08-23T00:00:00Z',
      createdBy: '30000000-0000-0000-0000-000000000001',
    }
    vi.spyOn(tcflowApi, 'createFeature').mockResolvedValue(feature)
    vi.spyOn(tcflowApi, 'features').mockResolvedValue(page([feature]))

    const workspace = useWorkspaceStore()
    workspace.selectProject(projectId)
    await workspace.createFeature(feature.name, feature.description)

    expect(tcflowApi.features).toHaveBeenCalledWith(projectId, undefined)
    expect(workspace.features).toEqual([feature])
    expect(workspace.featuresState.status).toBe('ready')
  })
})
