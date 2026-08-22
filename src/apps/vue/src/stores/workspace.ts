import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '../services/http'
import { tcflowApi, type CreateTaskInput, type TaskSearch } from '../services/tcflow-api'
import type {
  AuditRecord,
  EffectivePermissionResult,
  EngineeringTask,
  EngineeringTaskDetails,
  PermissionDefinition,
  Project,
  ProjectFeature,
  ProjectRepository,
  ProjectRole,
  ResourceState,
  TaskVersion,
} from '../types/contracts'
import {
  AiTrustLevel,
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
  const transientFeatures = ref<ProjectFeature[]>([])
  const taskDetails = ref<EngineeringTaskDetails | null>(null)
  const taskHistory = ref<TaskVersion[]>([])
  const effectivePermissions = ref<EffectivePermissionResult | null>(null)
  const permissionDefinitions = ref<PermissionDefinition[]>([])
  const audit = ref<AuditRecord[]>([])
  const createdRoles = ref<ProjectRole[]>([])

  const projectsState = ref<ResourceState>({ status: 'idle' })
  const permissionsState = ref<ResourceState>({ status: 'idle' })
  const repositoriesState = ref<ResourceState>({ status: 'idle' })
  const tasksState = ref<ResourceState>({ status: 'idle' })
  const taskState = ref<ResourceState>({ status: 'idle' })
  const administrationState = ref<ResourceState>({ status: 'idle' })

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
    selectedProjectId.value = projectId
    effectivePermissions.value = null
    permissionsState.value = { status: 'idle' }
    repositories.value = []
    tasks.value = []
    transientFeatures.value = []
    if (typeof window !== 'undefined') {
      if (projectId) window.sessionStorage.setItem(projectStorageKey, projectId)
      else window.sessionStorage.removeItem(projectStorageKey)
    }
  }

  async function loadProjects(keyword?: string): Promise<void> {
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

  async function loadPermissions(userId: string): Promise<void> {
    if (!selectedProjectId.value) return
    permissionsState.value = { status: 'loading' }
    effectivePermissions.value = null
    try {
      effectivePermissions.value = await tcflowApi.effectivePermissions(
        selectedProjectId.value,
        userId,
      )
      permissionsState.value = { status: 'ready' }
    } catch (error) {
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
    const feature = await tcflowApi.createFeature(selectedProjectId.value, name, description)
    transientFeatures.value = [...transientFeatures.value, feature]
  }

  async function loadAdministration(): Promise<void> {
    if (!selectedProjectId.value) return
    administrationState.value = { status: 'loading' }
    const [definitions, records] = await Promise.allSettled([
      tcflowApi.permissionDefinitions(selectedProjectId.value),
      tcflowApi.audit(selectedProjectId.value),
    ])
    permissionDefinitions.value = definitions.status === 'fulfilled' ? definitions.value : []
    audit.value = records.status === 'fulfilled' ? records.value : []
    if (definitions.status === 'rejected' && records.status === 'rejected') {
      administrationState.value = stateFromError(
        definitions.reason,
        'Unable to load project administration.',
      )
      return
    }
    administrationState.value = { status: 'ready' }
  }

  async function createRole(name: string): Promise<ProjectRole> {
    if (!selectedProjectId.value) throw new Error('Select a project before creating a role.')
    const role = await tcflowApi.createRole(selectedProjectId.value, name)
    createdRoles.value = [...createdRoles.value, role]
    await loadAdministration()
    return role
  }

  async function updateRolePermissions(
    roleId: string,
    permissionCodes: string[],
    resourceScope: number,
    componentScopes: number[],
    resourceId?: string,
  ): Promise<void> {
    if (!selectedProjectId.value) return
    const updated = await tcflowApi.updateRolePermissions(
      selectedProjectId.value,
      roleId,
      permissionCodes.map((permissionCode) => ({
        permissionCode,
        resourceScope,
        resourceId: resourceId || undefined,
        componentScopes,
      })),
    )
    createdRoles.value = createdRoles.value.map((role) => (role.id === updated.id ? updated : role))
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
    transientFeatures,
    taskDetails,
    taskHistory,
    effectivePermissions,
    permissionDefinitions,
    audit,
    createdRoles,
    projectsState,
    permissionsState,
    repositoriesState,
    tasksState,
    taskState,
    administrationState,
    taskCounts,
    hasPermission,
    selectProject,
    loadProjects,
    createProject,
    loadPermissions,
    loadRepositories,
    createRepository,
    loadTasks,
    createTask,
    transitionTask,
    loadTask,
    reviewTask,
    assignTask,
    addEvidence,
    createFeature,
    loadAdministration,
    createRole,
    updateRolePermissions,
    assignMemberRoles,
    updateAiPolicy,
    transferOwnership,
    defaults: { RepositoryProviderKind, TaskPriority },
  }
})
