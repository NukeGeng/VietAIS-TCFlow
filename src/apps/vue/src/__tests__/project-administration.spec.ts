import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useWorkspaceStore } from '../stores/workspace'
import {
  AiTrustLevel,
  AuthorityKnowledgeKind,
  AuthoritySourceKind,
  ComponentScopeKind,
  ConventionProfileStatus,
  type PermissionGrantTrace,
} from '../types/contracts'
import ProjectAdminView from '../views/ProjectAdminView.vue'

const projectId = '20000000-0000-0000-0000-000000000001'
const ownerId = '30000000-0000-0000-0000-000000000001'
const now = '2026-08-23T00:00:00Z'

describe('project administration', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders persisted roles, members, components, governance, and AI policy', () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'TCFlow', primaryOwnerId: ownerId, createdAt: now },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = {
      projectId,
      userId: ownerId,
      grants: [
        grant('role.view'),
        grant('member.view'),
        grant('component.view'),
        grant('authority.view'),
        grant('convention.view'),
        grant('ai.policy.update'),
        grant('audit.view'),
      ],
    }
    workspace.projectRoles = [
      {
        id: '40000000-0000-0000-0000-000000000001',
        projectId,
        name: 'Maintainer',
        isSystemDefined: false,
        isOwner: false,
        permissions: [],
      },
    ]
    workspace.projectMembers = [
      {
        id: '50000000-0000-0000-0000-000000000001',
        projectId,
        userId: ownerId,
        isActive: true,
        roles: [],
      },
    ]
    workspace.components = [
      {
        id: '60000000-0000-0000-0000-000000000001',
        projectId,
        repositoryId: '70000000-0000-0000-0000-000000000001',
        name: 'Repository API',
        scope: ComponentScopeKind.Backend,
        rootPath: 'src/api',
        createdAt: now,
        createdBy: ownerId,
      },
    ]
    workspace.aiPolicy = {
      id: '80000000-0000-0000-0000-000000000001',
      projectId,
      trustLevel: AiTrustLevel.SuggestOnly,
      allowedPermissions: ['ai.analysis.run', 'ai.task.suggest'],
      updatedAt: now,
      updatedBy: ownerId,
    }
    workspace.authorityPolicy = {
      id: '90000000-0000-0000-0000-000000000001',
      projectId,
      rules: [
        { knowledge: AuthorityKnowledgeKind.ApiContract, source: AuthoritySourceKind.Backend },
      ],
      updatedAt: now,
      updatedBy: ownerId,
    }
    workspace.conventionProfile = {
      id: 'a0000000-0000-0000-0000-000000000001',
      projectId,
      status: ConventionProfileStatus.Confirmed,
      architectures: ['vertical slices'],
      apiStyles: ['minimal APIs'],
      persistencePatterns: ['Marten'],
      validationPatterns: ['FluentValidation'],
      dtoPatterns: ['records'],
      updatedAt: now,
      updatedBy: ownerId,
    }
    workspace.administrationState = { status: 'ready' }
    workspace.permissionsState = { status: 'ready' }
    vi.spyOn(workspace, 'loadAdministration').mockResolvedValue()
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()

    const wrapper = mount(ProjectAdminView, { global: { plugins: [pinia] } })

    expect(wrapper.text()).toContain('Maintainer')
    expect(wrapper.text()).toContain(ownerId)
    expect(wrapper.text()).toContain('Repository API')
    expect(wrapper.text()).toContain('SuggestOnly')
    expect(wrapper.findAll('input').some((input) => input.element.value === 'Marten')).toBe(true)
  })
})

function grant(permissionCode: string): PermissionGrantTrace {
  return {
    permissionCode,
    roleId: '40000000-0000-0000-0000-000000000001',
    roleName: 'Owner',
    resourceScope: 1,
    componentScopes: [],
  }
}
