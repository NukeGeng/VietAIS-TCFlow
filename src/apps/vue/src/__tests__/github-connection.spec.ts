import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createAppRouter } from '../router'
import { authSession } from '../services/auth-session'
import { externalNavigation } from '../services/external-navigation'
import { gitHubConnectionSession } from '../services/github-connection-session'
import { tcflowApi } from '../services/tcflow-api'
import { useWorkspaceStore } from '../stores/workspace'
import AnalysisView from '../views/AnalysisView.vue'
import GitHubCallbackView from '../views/GitHubCallbackView.vue'
import RepositoriesView from '../views/RepositoriesView.vue'
import {
  GitHubAnalysisRequestStatus,
  GitHubAnalysisTriggerKind,
  RepositoryAnalysisRunStatus,
  RepositoryLifecycleStatus,
  RepositoryProviderKind,
} from '../types/contracts'

const projectId = '20000000-0000-0000-0000-000000000001'
const actorId = '30000000-0000-0000-0000-000000000001'
const future = '2099-01-01T00:00:00Z'

function authenticate(): void {
  authSession.write({
    token: 'token',
    refreshToken: 'refresh',
    refreshTokenExpiryTime: future,
    tenant: 'root',
  })
}

function installation() {
  return {
    id: '40000000-0000-0000-0000-000000000001',
    projectId,
    installationId: 101,
    accountId: 202,
    accountLogin: 'NukeGeng',
    accountKind: 0,
    repositorySelection: 0,
    status: 0,
    createdAt: future,
    updatedAt: future,
    updatedBy: actorId,
  }
}

const remoteRepository = {
  id: 303,
  name: 'VietAIS-TCFlow',
  fullName: 'NukeGeng/VietAIS-TCFlow',
  private: true,
  defaultBranch: 'main',
  htmlUrl: 'https://github.com/NukeGeng/VietAIS-TCFlow',
}

describe('GitHub App connection', () => {
  afterEach(() => {
    authSession.clear()
    gitHubConnectionSession.remove('oauth-state')
    vi.restoreAllMocks()
  })

  it('stores PKCE only for the browser session before leaving for GitHub OAuth', async () => {
    authenticate()
    const router = createAppRouter()
    await router.push('/github/callback?installation_id=101&state=install-state')
    await router.isReady()
    vi.spyOn(tcflowApi, 'prepareGitHubAuthorization').mockResolvedValue({
      projectId,
      authorizationUrl: 'https://github.com/login/oauth/authorize?state=oauth-state',
      state: 'oauth-state',
      codeVerifier: 'browser-only-verifier',
      expiresAt: future,
    })
    const navigate = vi.spyOn(externalNavigation, 'assign').mockImplementation(() => undefined)

    const wrapper = mount(GitHubCallbackView, { global: { plugins: [router] } })
    await flushPromises()

    expect(tcflowApi.prepareGitHubAuthorization).toHaveBeenCalledWith('install-state', 101)
    expect(gitHubConnectionSession.read('oauth-state')?.codeVerifier).toBe('browser-only-verifier')
    expect(navigate).toHaveBeenCalledWith(
      'https://github.com/login/oauth/authorize?state=oauth-state',
    )
    expect(wrapper.text()).not.toContain('browser-only-verifier')
  })

  it('completes OAuth with the matching verifier, clears it, and returns to the picker', async () => {
    authenticate()
    gitHubConnectionSession.write('oauth-state', 'browser-only-verifier', future)
    const router = createAppRouter()
    await router.push('/github/callback?code=one-time-code&state=oauth-state')
    await router.isReady()
    vi.spyOn(tcflowApi, 'completeGitHubConnection').mockResolvedValue({
      projectId,
      installation: installation(),
      repositories: [remoteRepository],
    })

    mount(GitHubCallbackView, { global: { plugins: [router] } })
    await flushPromises()

    expect(tcflowApi.completeGitHubConnection).toHaveBeenCalledWith(
      'oauth-state',
      'one-time-code',
      'browser-only-verifier',
    )
    expect(gitHubConnectionSession.read('oauth-state')).toBeNull()
    expect(router.currentRoute.value.name).toBe('repositories')
    expect(router.currentRoute.value.query.installation).toBe('101')
  })

  it('selects a private repository by verified installation and repository IDs only', async () => {
    authenticate()
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'TCFlow', primaryOwnerId: actorId, createdAt: future },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = {
      projectId,
      userId: actorId,
      grants: ['repository.create', 'repository.access.manage'].map((permissionCode) => ({
        permissionCode,
        roleId: '50000000-0000-0000-0000-000000000001',
        roleName: 'Owner',
        resourceScope: 1,
        componentScopes: [],
      })),
    }
    workspace.repositoriesState = { status: 'empty' }
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()
    vi.spyOn(tcflowApi, 'gitHubInstallations').mockResolvedValue([installation()])
    vi.spyOn(tcflowApi, 'gitHubRepositories').mockResolvedValue([remoteRepository])
    const connect = vi.spyOn(tcflowApi, 'connectGitHubRepository').mockResolvedValue({
      repository: {
        id: '60000000-0000-0000-0000-000000000001',
        projectId,
        name: remoteRepository.fullName,
        provider: RepositoryProviderKind.GitHub,
        remoteUrl: remoteRepository.htmlUrl,
        defaultBranch: remoteRepository.defaultBranch,
        status: RepositoryLifecycleStatus.Active,
        createdAt: future,
        createdBy: actorId,
      },
      access: {
        id: '70000000-0000-0000-0000-000000000001',
        projectId,
        projectRepositoryId: '60000000-0000-0000-0000-000000000001',
        installationDocumentId: installation().id,
        installationId: 101,
        gitHubRepositoryId: 303,
        fullName: remoteRepository.fullName,
        isSelected: true,
        selectedAt: future,
        selectedBy: actorId,
      },
    })
    const router = createAppRouter()
    await router.push(`/projects/${projectId}/repositories`)
    await router.isReady()

    const wrapper = mount(RepositoriesView, { global: { plugins: [pinia, router] } })
    await flushPromises()
    const addButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Add selected repository'))
    expect(addButton).toBeDefined()
    await addButton!.trigger('click')
    await flushPromises()

    expect(connect).toHaveBeenCalledWith(projectId, 101, 303)
  })

  it('shows an unsupported repository analysis instead of implying that analysis never ran', async () => {
    authenticate()
    const pinia = createPinia()
    setActivePinia(pinia)
    const workspace = useWorkspaceStore()
    workspace.projects = [
      { id: projectId, name: 'Test', primaryOwnerId: actorId, createdAt: future },
    ]
    workspace.selectProject(projectId)
    workspace.effectivePermissions = {
      projectId,
      userId: actorId,
      grants: [
        {
          permissionCode: 'source.analyze',
          roleId: '50000000-0000-0000-0000-000000000001',
          roleName: 'Owner',
          resourceScope: 1,
          componentScopes: [],
        },
      ],
    }
    const repositoryId = '60000000-0000-0000-0000-000000000002'
    workspace.repositories = [
      {
        id: repositoryId,
        projectId,
        name: 'NukeGeng/Portfolio',
        provider: RepositoryProviderKind.GitHub,
        remoteUrl: 'https://github.com/NukeGeng/Portfolio',
        defaultBranch: 'main',
        status: RepositoryLifecycleStatus.Active,
        createdAt: future,
        createdBy: actorId,
      },
    ]
    workspace.repositoriesState = { status: 'ready' }
    workspace.tasksState = { status: 'empty' }
    vi.spyOn(workspace, 'loadRepositories').mockResolvedValue()
    vi.spyOn(workspace, 'loadTasks').mockResolvedValue()
    vi.spyOn(tcflowApi, 'latestRepositoryAnalysis').mockResolvedValue({
      request: {
        id: '80000000-0000-0000-0000-000000000001',
        projectId,
        repositoryId,
        trigger: GitHubAnalysisTriggerKind.InitialScan,
        reference: 'refs/heads/main',
        fullScan: true,
        requiresChangedFileFetch: false,
        changedFiles: [],
        status: GitHubAnalysisRequestStatus.Ignored,
        requestedAt: future,
        requestedByType: 'User',
        requestedBy: actorId,
      },
      run: {
        id: '80000000-0000-0000-0000-000000000001',
        projectId,
        repositoryId,
        status: RepositoryAnalysisRunStatus.Unsupported,
        attempt: 1,
        sourceRevision: '988facfcf7837a4e043e3901219e2a788e0220bc',
        technologies: ['TypeScript'],
        artifactCount: 0,
        dependencyCount: 0,
        contractCount: 0,
        mismatchCount: 0,
        changeCount: 0,
        impactCount: 0,
        generatedTaskCount: 0,
        diagnostics: [
          {
            code: 'ANALYSIS001',
            message:
              'The repository contains no source facts supported by the configured analyzers.',
            evidenceLevel: 'Confirmed',
          },
        ],
        startedAt: future,
        updatedAt: future,
        completedAt: future,
      },
    })
    const router = createAppRouter()
    await router.push(`/projects/${projectId}/analysis`)
    await router.isReady()

    const wrapper = mount(AnalysisView, { global: { plugins: [pinia, router] } })
    await flushPromises()

    expect(wrapper.text()).toContain('NukeGeng/Portfolio')
    expect(wrapper.text()).toContain('Unsupported')
    expect(wrapper.text()).toContain('TypeScript')
    expect(wrapper.text()).toContain('ANALYSIS001')
    expect(wrapper.text()).toContain('No task was created')
    expect(wrapper.text()).toContain('No analyzed task evidence')
    wrapper.unmount()
  })
})
