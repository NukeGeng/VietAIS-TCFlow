import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '../services/http'
import { tcflowApi, type CreateTaskInput, type TaskSearch } from '../services/tcflow-api'
import type {
  AuditRecord,
  AiPermissionPolicy,
  AuthorityPolicy,
  AuthorityRule,
  ConventionProfile,
  EffectivePermissionResult,
  EngineeringTask,
  EngineeringTaskDetails,
  PermissionDefinition,
  Project,
  ProjectComponent,
  ProjectFeature,
  ProjectMembership,
  ProjectRepository,
  ProjectRole,
  ResourceState,
  TaskVersion,
} from '../types/contracts'
import {
  AiTrustLevel,
  ConventionProfileStatus,
  RepositoryProviderKind,
  TaskEvidenceKind,
  TaskLifecycleStatus,
  TaskPriority,
  TaskReviewDecision,
} from '../types/contracts'

const projectStorageKey = 'tcflow.selected-project'

function selectedFromStorage(): string | null {
  return typeof window === 'undefined' ? null : window.sessionStorage.getItem(projectStorageKey)
}

function stateFromError(error: unknown, fallback: string): ResourceState {
  if (error instanceof ApiError && error.status === 403) {
    return { status: 'forbidden', message: error.message }
  }
  return { status: 'error', message: error instanceof Error ? error.message : fallback }
}

export const useWorkspaceStore = defineStore('workspace', () => {
  const projects = ref<Project[]>([])
  const selectedProjectId = ref<string | null>(selectedFromStorage())
  const repositories = ref<ProjectRepository[]>([])
  const tasks = ref<EngineeringTask[]>([])
  const features = ref<ProjectFeature[]>([])
  const components = ref<ProjectComponent[]>([])
  const taskDetails = ref<EngineeringTaskDetails | null>(null)
  const taskHistory = ref<TaskVersion[]>([])
  const effectivePermissions = ref<EffectivePermissionResult | null>(null)
  const permissionDefinitions = ref<PermissionDefinition[]>([])
  const audit = ref<AuditRecord[]>([])
  const projectRoles = ref<ProjectRole[]>([])
  const projectMembers = ref<ProjectMembership[]>([])
  const aiPolicy = ref<AiPermissionPolicy | null>(null)
  const authorityPolicy = ref<AuthorityPolicy | null>(null)
  const conventionProfile = ref<ConventionProfile | null>(null)

  const projectsState = ref<ResourceState>({ status: 'idle' })
  const permissionsState = ref<ResourceState>({ status: 'idle' })
  const repositoriesState = ref<ResourceState>({ status: 'idle' })
  const featuresState = ref<ResourceState>({ status: 'idle' })
  const tasksState = ref<ResourceState>({ status: 'idle' })
  const taskState = ref<ResourceState>({ status: 'idle' })
  const administrationState = ref<ResourceState>({ status: 'idle' })
  let projectsRequest: Promise<void> | null = null
  let permissionsRequest: {
    projectId: string
    userId: string
    promise: Promise<void>
  } | null = null

  const selectedProject = computed(
    () => projects.value.find((project) => project.id === selectedProjectId.value) ?? null,
  )
  const projectPermissionCodes = computed(
    () => new Set(effectivePermissions.value?.grants.map((grant) => grant.permissionCode) ?? []),
  )

  function hasPermission(permission: string): boolean {
    return projectPermissionCodes.value.has(permission)
  }

  function selectProject(projectId: string | null): void {
    // Selecting the already active project is a no-op. Clearing its hydrated
    // permissions here leaves the sidebar disabled because the selected id did
    // not change, so no reactive reload is triggered.
    if (selectedProjectId.value === projectId) return

    selectedProjectId.value = projectId
    effectivePermissions.value = null
    permissionsState.value = { status: 'idle' }
    repositories.value = []
    tasks.value = []
    features.value = []
    components.value = []
    taskDetails.value = null
    taskHistory.value = []
    projectRoles.value = []
    projectMembers.value = []
    aiPolicy.value = null
    authorityPolicy.value = null
    conventionProfile.value = null
    repositoriesState.value = { status: 'idle' }
    featuresState.value = { status: 'idle' }
    tasksState.value = { status: 'idle' }
    taskState.value = { status: 'idle' }
    administrationState.value = { status: 'idle' }
    if (typeof window !== 'undefined') {
      if (projectId) window.sessionStorage.setItem(projectStorageKey, projectId)
      else window.sessionStorage.removeItem(projectStorageKey)
    }
  }

  async function loadProjects(keyword?: string): Promise<void> {
    if (!keyword && projectsRequest) return projectsRequest

    const request = loadProjectsFromApi(keyword)
    if (keyword) {
      await request
      return
    }

    projectsRequest = request
    try {
      await request
    } finally {
      if (projectsRequest === request) projectsRequest = null
    }
  }

  async function loadProjectsFromApi(keyword?: string): Promise<void> {
    projectsState.value = { status: 'loading' }
    try {
      const response = await tcflowApi.projects(keyword)
      projects.value = response.items
      projectsState.value = { status: response.items.length ? 'ready' : 'empty' }
      if (
        !selectedProjectId.value ||
        !response.items.some((item) => item.id === selectedProjectId.value)
      ) {
        selectProject(response.items[0]?.id ?? null)
      }
    } catch (error) {
      projectsState.value = stateFromError(error, 'Unable to load projects.')
    }
  }

  async function createProject(name: string, userId: string): Promise<Project> {
    const response = await tcflowApi.createProject(name)
    projects.value = [...projects.value, response.project]
    selectProject(response.project.id)
    await loadPermissions(userId)
    return response.project
  }

  async function updateProject(name: string): Promise<void> {
    if (!selectedProjectId.value) return
    const project = await tcflowApi.updateProject(selectedProjectId.value, name)
    projects.value = projects.value.map((item) => (item.id === project.id ? project : item))
    await loadAdministration()
  }

  async function loadPermissions(userId: string): Promise<void> {
    if (!selectedProjectId.value) return
    const projectId = selectedProjectId.value
    if (permissionsRequest?.projectId === projectId && permissionsRequest.userId === userId) {
      return permissionsRequest.promise
    }

    const request = loadPermissionsFromApi(projectId, userId)
    permissionsRequest = { projectId, userId, promise: request }
    try {
      await request
    } finally {
      if (permissionsRequest?.promise === request) permissionsRequest = null
    }
  }

  async function activateProject(projectId: string, userId: string): Promise<void> {
    selectProject(projectId)

    if (
      effectivePermissions.value?.projectId === projectId &&
      effectivePermissions.value.userId === userId &&
      permissionsState.value.status === 'ready'
    ) {
      return
    }

    await loadPermissions(userId)
  }

  async function loadPermissionsFromApi(projectId: string, userId: string): Promise<void> {
    permissionsState.value = { status: 'loading' }
    effectivePermissions.value = null
    try {
      const permissions = await tcflowApi.effectivePermissions(projectId, userId)
      if (selectedProjectId.value !== projectId) return
      effectivePermissions.value = permissions
      permissionsState.value = { status: 'ready' }
    } catch (error) {
      if (selectedProjectId.value !== projectId) return
      permissionsState.value = stateFromError(
        error,
        'Project permissions are unavailable. Actions remain disabled for safety.',
      )
    }
  }

  async function loadRepositories(keyword?: string): Promise<void> {
    if (!selectedProjectId.value) return
    repositoriesState.value = { status: 'loading' }
    try {
      const response = await tcflowApi.repositories(selectedProjectId.value, keyword)
      repositories.value = response.items
      repositoriesState.value = { status: response.items.length ? 'ready' : 'empty' }
    } catch (error) {
      repositoriesState.value = stateFromError(error, 'Unable to load repositories.')
    }
  }

  async function createRepository(input: {
    name: string
    provider: RepositoryProviderKind
    localPath?: string
    remoteUrl?: string
    defaultBranch: string
  }): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.createRepository(selectedProjectId.value, input)
    await loadRepositories()
  }

  async function updateRepository(
    repositoryId: string,
    input: {
      name: string
      localPath?: string
      remoteUrl?: string
      defaultBranch: string
      status: ProjectRepository['status']
    },
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateRepository(selectedProjectId.value, repositoryId, input)
    await loadRepositories()
  }

  async function disableRepository(repositoryId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.disableRepository(selectedProjectId.value, repositoryId)
    await loadRepositories()
  }

  async function loadTasks(search: TaskSearch = {}): Promise<void> {
    if (!selectedProjectId.value) return
    tasksState.value = { status: 'loading' }
    try {
      const response = await tcflowApi.tasks(selectedProjectId.value, search)
      tasks.value = response.items
      tasksState.value = { status: response.items.length ? 'ready' : 'empty' }
    } catch (error) {
      tasksState.value = stateFromError(error, 'Unable to load tasks.')
    }
  }

  async function createTask(input: CreateTaskInput): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.createTask(selectedProjectId.value, input)
    await loadTasks()
  }

  async function transitionTask(
    taskId: string,
    status: TaskLifecycleStatus,
    reason?: string,
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.transitionTask(selectedProjectId.value, taskId, status, reason)
    await loadTasks()
    if (taskDetails.value?.task.id === taskId) await loadTask(taskId)
  }

  async function loadTask(taskId: string): Promise<void> {
    if (!selectedProjectId.value) return
    taskState.value = { status: 'loading' }
    try {
      const [details, history] = await Promise.all([
        tcflowApi.task(selectedProjectId.value, taskId),
        tcflowApi.taskHistory(selectedProjectId.value, taskId),
      ])
      taskDetails.value = details
      taskHistory.value = history
      taskState.value = { status: 'ready' }
    } catch (error) {
      taskState.value = stateFromError(error, 'Unable to load task details.')
    }
  }

  async function reviewTask(
    taskId: string,
    decision: TaskReviewDecision,
    comment?: string,
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.reviewTask(selectedProjectId.value, taskId, decision, comment)
    await loadTask(taskId)
  }

  async function assignTask(taskId: string, assigneeId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.assignTask(selectedProjectId.value, taskId, assigneeId)
    await loadTask(taskId)
  }

  async function addEvidence(taskId: string, summary: string, location?: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.addEvidence(selectedProjectId.value, taskId, {
      kind: TaskEvidenceKind.Verification,
      summary,
      location,
    })
    await loadTask(taskId)
  }

  async function createFeature(name: string, description?: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.createFeature(selectedProjectId.value, name, description)
    await loadFeatures()
  }

  async function updateFeature(
    featureId: string,
    name: string,
    description?: string,
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateFeature(selectedProjectId.value, featureId, name, description)
    await loadFeatures()
  }

  async function deleteFeature(featureId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.deleteFeature(selectedProjectId.value, featureId)
    await loadFeatures()
  }

  async function loadFeatures(keyword?: string): Promise<void> {
    if (!selectedProjectId.value) return
    featuresState.value = { status: 'loading' }
    try {
      const response = await tcflowApi.features(selectedProjectId.value, keyword)
      features.value = response.items
      featuresState.value = { status: response.items.length ? 'ready' : 'empty' }
    } catch (error) {
      featuresState.value = stateFromError(error, 'Unable to load features.')
    }
  }

  async function loadAdministration(): Promise<void> {
    if (!selectedProjectId.value) return
    administrationState.value = { status: 'loading' }
    const projectId = selectedProjectId.value
    const [
      definitions,
      roles,
      members,
      componentPage,
      currentAiPolicy,
      currentAuthority,
      currentConvention,
      records,
    ] = await Promise.allSettled([
      hasPermission('role.view') ? tcflowApi.permissionDefinitions(projectId) : Promise.resolve([]),
      hasPermission('role.view') ? tcflowApi.roles(projectId) : Promise.resolve([]),
      hasPermission('member.view') ? tcflowApi.members(projectId) : Promise.resolve([]),
      hasPermission('component.view')
        ? tcflowApi.components(projectId)
        : Promise.resolve({ items: [] } as { items: ProjectComponent[] }),
      hasPermission('ai.policy.update') ? tcflowApi.aiPolicy(projectId) : Promise.resolve(null),
      hasPermission('authority.view')
        ? tcflowApi.authorityPolicy(projectId)
        : Promise.resolve(null),
      hasPermission('convention.view')
        ? tcflowApi.conventionProfile(projectId)
        : Promise.resolve(null),
      hasPermission('audit.view') ? tcflowApi.audit(projectId) : Promise.resolve([]),
    ])
    permissionDefinitions.value = definitions.status === 'fulfilled' ? definitions.value : []
    projectRoles.value = roles.status === 'fulfilled' ? roles.value : []
    projectMembers.value = members.status === 'fulfilled' ? members.value : []
    components.value = componentPage.status === 'fulfilled' ? componentPage.value.items : []
    aiPolicy.value = currentAiPolicy.status === 'fulfilled' ? currentAiPolicy.value : null
    authorityPolicy.value = currentAuthority.status === 'fulfilled' ? currentAuthority.value : null
    conventionProfile.value =
      currentConvention.status === 'fulfilled' ? currentConvention.value : null
    audit.value = records.status === 'fulfilled' ? records.value : []
    const failures = [
      definitions,
      roles,
      members,
      componentPage,
      currentAiPolicy,
      currentAuthority,
      currentConvention,
      records,
    ].filter((result) => result.status === 'rejected')
    if (failures.length > 0) {
      const firstFailure = failures[0] as PromiseRejectedResult
      administrationState.value = stateFromError(
        firstFailure.reason,
        'Unable to load project administration.',
      )
      return
    }
    administrationState.value = { status: 'ready' }
  }

  async function createRole(name: string): Promise<ProjectRole> {
    if (!selectedProjectId.value) throw new Error('Select a project before creating a role.')
    const role = await tcflowApi.createRole(selectedProjectId.value, name)
    await loadAdministration()
    return role
  }

  async function deleteRole(roleId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.deleteRole(selectedProjectId.value, roleId)
    await loadAdministration()
  }

  async function updateRolePermissions(
    roleId: string,
    permissions: ProjectRole['permissions'],
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateRolePermissions(selectedProjectId.value, roleId, permissions)
    await loadAdministration()
  }

  async function addMember(userId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.addMember(selectedProjectId.value, userId)
    await loadAdministration()
  }

  async function removeMember(userId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.removeMember(selectedProjectId.value, userId)
    await loadAdministration()
  }

  async function assignMemberRoles(memberId: string, roleIds: string[]): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.assignMemberRoles(selectedProjectId.value, memberId, roleIds)
    await loadAdministration()
  }

  async function updateAiPolicy(trustLevel: AiTrustLevel, permissions: string[]): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateAiPolicy(selectedProjectId.value, trustLevel, permissions)
    await loadAdministration()
  }

  async function updateAuthorityPolicy(rules: AuthorityRule[]): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateAuthorityPolicy(selectedProjectId.value, rules)
    await loadAdministration()
  }

  async function updateConventionProfile(input: {
    status: ConventionProfileStatus
    architectures: string[]
    apiStyles: string[]
    persistencePatterns: string[]
    validationPatterns: string[]
    dtoPatterns: string[]
  }): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateConventionProfile(selectedProjectId.value, input)
    await loadAdministration()
  }

  async function createComponent(input: {
    repositoryId: string
    name: string
    scope: number
    rootPath?: string
  }): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.createComponent(selectedProjectId.value, input)
    await loadAdministration()
  }

  async function updateComponent(
    componentId: string,
    input: { name: string; scope: number; rootPath?: string },
  ): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.updateComponent(selectedProjectId.value, componentId, input)
    await loadAdministration()
  }

  async function deleteComponent(componentId: string): Promise<void> {
    if (!selectedProjectId.value) return
    await tcflowApi.deleteComponent(selectedProjectId.value, componentId)
    await loadAdministration()
  }

  async function transferOwnership(newOwnerId: string): Promise<void> {
    if (!selectedProjectId.value) return
    const project = await tcflowApi.transferOwnership(selectedProjectId.value, newOwnerId)
    projects.value = projects.value.map((item) => (item.id === project.id ? project : item))
    await loadAdministration()
  }

  const taskCounts = computed(() => {
    const counts = new Map<TaskLifecycleStatus, number>()
    for (const status of Object.values(TaskLifecycleStatus).filter(
      (value): value is TaskLifecycleStatus => typeof value === 'number',
    )) {
      counts.set(status, tasks.value.filter((task) => task.status === status).length)
    }
    return counts
  })

  return {
    projects,
    selectedProjectId,
    selectedProject,
    repositories,
    tasks,
    features,
    components,
    taskDetails,
    taskHistory,
    effectivePermissions,
    permissionDefinitions,
    audit,
    projectRoles,
    projectMembers,
    aiPolicy,
    authorityPolicy,
    conventionProfile,
    projectsState,
    permissionsState,
    repositoriesState,
    featuresState,
    tasksState,
    taskState,
    administrationState,
    taskCounts,
    hasPermission,
    selectProject,
    loadProjects,
    createProject,
    updateProject,
    activateProject,
    loadPermissions,
    loadRepositories,
    createRepository,
    updateRepository,
    disableRepository,
    loadTasks,
    createTask,
    transitionTask,
    loadTask,
    reviewTask,
    assignTask,
    addEvidence,
    createFeature,
    updateFeature,
    deleteFeature,
    loadFeatures,
    loadAdministration,
    createRole,
    deleteRole,
    updateRolePermissions,
    assignMemberRoles,
    addMember,
    removeMember,
    updateAiPolicy,
    updateAuthorityPolicy,
    updateConventionProfile,
    createComponent,
    updateComponent,
    deleteComponent,
    transferOwnership,
    defaults: { RepositoryProviderKind, TaskPriority },
  }
})
