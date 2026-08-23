import { apiRequest, queryString } from './http'
import type {
  AuditRecord,
  AiTrustLevel,
  EffectivePermissionResult,
  EngineeringTask,
  EngineeringTaskDetails,
  ConnectedGitHubRepository,
  GitHubAppInstallation,
  GitHubAuthorizationStart,
  GitHubConnectionResult,
  GitHubInstallationStart,
  GitHubRepositorySummary,
  PagedList,
  PermissionDefinition,
  Project,
  ProjectFeature,
  ProjectRepository,
  ProjectRole,
  RepositoryAnalysisDetails,
  RepositoryAnalysisRequest,
  TaskEvidence,
  TaskReview,
  TaskVersion,
  TokenResponse,
  UserProfile,
} from '../types/contracts'
import type {
  RepositoryProviderKind,
  TaskEvidenceKind,
  TaskLifecycleStatus,
  TaskPriority,
  TaskReviewDecision,
} from '../types/contracts'

export interface TaskSearch {
  keyword?: string
  status?: TaskLifecycleStatus
  priority?: TaskPriority
  repositoryId?: string
  featureId?: string
  assigneeId?: string
}

export interface CreateTaskInput {
  repositoryId?: string
  componentId?: string
  featureId?: string
  title: string
  description?: string
  priority: TaskPriority
  sourceChangeId?: string
  artifactIds: string[]
  impactIds: string[]
  affectedArtifacts: string[]
  inputs: string[]
  outputs: string[]
  businessRules: string[]
  dependencies: string[]
}

export const tcflowApi = {
  login(email: string, password: string, tenant: string): Promise<TokenResponse> {
    return apiRequest<TokenResponse>('/api/token/', {
      method: 'POST',
      authenticated: false,
      headers: { tenant },
      body: JSON.stringify({ email, password }),
    })
  },

  profile(): Promise<UserProfile> {
    return apiRequest<UserProfile>('/api/users/profile')
  },

  systemPermissions(): Promise<string[]> {
    return apiRequest<string[]>('/api/users/permissions')
  },

  users(): Promise<UserProfile[]> {
    return apiRequest<UserProfile[]>('/api/users/')
  },

  projects(keyword?: string): Promise<PagedList<Project>> {
    return apiRequest<PagedList<Project>>(
      `/api/v1/projects${queryString({ pageNumber: 1, pageSize: 100, keyword })}`,
    )
  },

  createProject(name: string): Promise<{ project: Project }> {
    return apiRequest<{ project: Project }>('/api/v1/projects', {
      method: 'POST',
      body: JSON.stringify({ name }),
    })
  },

  project(projectId: string): Promise<Project> {
    return apiRequest<Project>(`/api/v1/projects/${projectId}`)
  },

  effectivePermissions(projectId: string, userId: string): Promise<EffectivePermissionResult> {
    return apiRequest<EffectivePermissionResult>(
      `/api/v1/projects/${projectId}/members/${userId}/effective-permissions`,
    )
  },

  permissionDefinitions(projectId: string): Promise<PermissionDefinition[]> {
    return apiRequest<PermissionDefinition[]>(
      `/api/v1/projects/${projectId}/permission-definitions`,
    )
  },

  createRole(projectId: string, name: string): Promise<ProjectRole> {
    return apiRequest<ProjectRole>(`/api/v1/projects/${projectId}/roles`, {
      method: 'POST',
      body: JSON.stringify({ name }),
    })
  },

  updateRolePermissions(
    projectId: string,
    roleId: string,
    permissions: Array<{
      permissionCode: string
      resourceScope: number
      resourceId?: string
      componentScopes: number[]
    }>,
  ): Promise<ProjectRole> {
    return apiRequest<ProjectRole>(`/api/v1/projects/${projectId}/roles/${roleId}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({ permissions }),
    })
  },

  assignMemberRoles(projectId: string, memberId: string, roleIds: string[]): Promise<unknown> {
    return apiRequest(`/api/v1/projects/${projectId}/members/${memberId}/roles`, {
      method: 'PUT',
      body: JSON.stringify({ roleIds }),
    })
  },

  updateAiPolicy(
    projectId: string,
    trustLevel: AiTrustLevel,
    allowedPermissions: string[],
  ): Promise<unknown> {
    return apiRequest(`/api/v1/projects/${projectId}/ai-policy`, {
      method: 'PUT',
      body: JSON.stringify({ trustLevel, allowedPermissions }),
    })
  },

  transferOwnership(projectId: string, newOwnerId: string): Promise<Project> {
    return apiRequest<Project>(`/api/v1/projects/${projectId}/ownership-transfers`, {
      method: 'POST',
      body: JSON.stringify({ newOwnerId, confirmed: true }),
    })
  },

  audit(projectId: string): Promise<AuditRecord[]> {
    return apiRequest<AuditRecord[]>(`/api/v1/projects/${projectId}/audit`)
  },

  repositories(projectId: string, keyword?: string): Promise<PagedList<ProjectRepository>> {
    return apiRequest<PagedList<ProjectRepository>>(
      `/api/v1/projects/${projectId}/repositories${queryString({ pageNumber: 1, pageSize: 100, keyword })}`,
    )
  },

  createRepository(
    projectId: string,
    input: {
      name: string
      provider: RepositoryProviderKind
      localPath?: string
      remoteUrl?: string
      defaultBranch: string
    },
  ): Promise<ProjectRepository> {
    return apiRequest<ProjectRepository>(`/api/v1/projects/${projectId}/repositories`, {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  startGitHubConnection(projectId: string): Promise<GitHubInstallationStart> {
    return apiRequest<GitHubInstallationStart>(`/api/v1/projects/${projectId}/github/connections`, {
      method: 'POST',
    })
  },

  prepareGitHubAuthorization(
    state: string,
    installationId: number,
  ): Promise<GitHubAuthorizationStart> {
    return apiRequest<GitHubAuthorizationStart>('/api/v1/github/connections/authorize', {
      method: 'POST',
      body: JSON.stringify({ state, installationId }),
    })
  },

  completeGitHubConnection(
    state: string,
    code: string,
    codeVerifier: string,
  ): Promise<GitHubConnectionResult> {
    return apiRequest<GitHubConnectionResult>('/api/v1/github/connections/complete', {
      method: 'POST',
      body: JSON.stringify({ state, code, codeVerifier }),
    })
  },

  gitHubInstallations(projectId: string): Promise<GitHubAppInstallation[]> {
    return apiRequest<GitHubAppInstallation[]>(`/api/v1/projects/${projectId}/github/installations`)
  },

  gitHubRepositories(
    projectId: string,
    installationId: number,
  ): Promise<GitHubRepositorySummary[]> {
    return apiRequest<GitHubRepositorySummary[]>(
      `/api/v1/projects/${projectId}/github/installations/${installationId}/repositories`,
    )
  },

  connectGitHubRepository(
    projectId: string,
    installationId: number,
    gitHubRepositoryId: number,
  ): Promise<ConnectedGitHubRepository> {
    return apiRequest<ConnectedGitHubRepository>(
      `/api/v1/projects/${projectId}/github/repositories`,
      {
        method: 'POST',
        body: JSON.stringify({ installationId, gitHubRepositoryId }),
      },
    )
  },

  triggerInitialGitHubScan(
    projectId: string,
    repositoryId: string,
  ): Promise<RepositoryAnalysisRequest> {
    return apiRequest<RepositoryAnalysisRequest>(
      `/api/v1/projects/${projectId}/github/repositories/${repositoryId}/initial-scan`,
      { method: 'POST' },
    )
  },

  latestRepositoryAnalysis(
    projectId: string,
    repositoryId: string,
  ): Promise<RepositoryAnalysisDetails> {
    return apiRequest<RepositoryAnalysisDetails>(
      `/api/v1/projects/${projectId}/github/repositories/${repositoryId}/analyses/latest`,
    )
  },

  repositoryAnalysis(
    projectId: string,
    repositoryId: string,
    analysisRequestId: string,
  ): Promise<RepositoryAnalysisDetails> {
    return apiRequest<RepositoryAnalysisDetails>(
      `/api/v1/projects/${projectId}/github/repositories/${repositoryId}/analyses/${analysisRequestId}`,
    )
  },

  createFeature(projectId: string, name: string, description?: string): Promise<ProjectFeature> {
    return apiRequest<ProjectFeature>(`/api/v1/projects/${projectId}/features`, {
      method: 'POST',
      body: JSON.stringify({ name, description }),
    })
  },

  tasks(projectId: string, search: TaskSearch = {}): Promise<PagedList<EngineeringTask>> {
    return apiRequest<PagedList<EngineeringTask>>(
      `/api/v1/projects/${projectId}/tasks${queryString({
        pageNumber: 1,
        pageSize: 100,
        ...search,
      })}`,
    )
  },

  createTask(projectId: string, input: CreateTaskInput): Promise<EngineeringTask> {
    return apiRequest<EngineeringTask>(`/api/v1/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  task(projectId: string, taskId: string): Promise<EngineeringTaskDetails> {
    return apiRequest<EngineeringTaskDetails>(`/api/v1/projects/${projectId}/tasks/${taskId}`)
  },

  transitionTask(
    projectId: string,
    taskId: string,
    status: TaskLifecycleStatus,
    reason?: string,
  ): Promise<EngineeringTask> {
    return apiRequest<EngineeringTask>(`/api/v1/projects/${projectId}/tasks/${taskId}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status, reason }),
    })
  },

  assignTask(projectId: string, taskId: string, assigneeId: string): Promise<unknown> {
    return apiRequest(`/api/v1/projects/${projectId}/tasks/${taskId}/assignment`, {
      method: 'PUT',
      body: JSON.stringify({ assigneeId }),
    })
  },

  reviewTask(
    projectId: string,
    taskId: string,
    decision: TaskReviewDecision,
    comment?: string,
  ): Promise<TaskReview> {
    return apiRequest<TaskReview>(`/api/v1/projects/${projectId}/tasks/${taskId}/reviews`, {
      method: 'POST',
      body: JSON.stringify({ decision, comment }),
    })
  },

  addEvidence(
    projectId: string,
    taskId: string,
    input: {
      kind: TaskEvidenceKind
      summary: string
      location?: string
      sourceChangeId?: string
      artifactId?: string
      impactId?: string
      confidence?: number
    },
  ): Promise<TaskEvidence> {
    return apiRequest<TaskEvidence>(`/api/v1/projects/${projectId}/tasks/${taskId}/evidence`, {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  taskHistory(projectId: string, taskId: string): Promise<TaskVersion[]> {
    return apiRequest<TaskVersion[]>(`/api/v1/projects/${projectId}/tasks/${taskId}/history`)
  },
}
