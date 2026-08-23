<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute } from 'vue-router'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { externalNavigation } from '../services/external-navigation'
import { tcflowApi } from '../services/tcflow-api'
import { useWorkspaceStore } from '../stores/workspace'
import type {
  GitHubAppInstallation,
  GitHubRepositorySummary,
  ProjectRepository,
} from '../types/contracts'
import { RepositoryLifecycleStatus, RepositoryProviderKind } from '../types/contracts'

const route = useRoute()
const workspace = useWorkspaceStore()
const { repositories, repositoriesState, selectedProject, selectedProjectId } =
  storeToRefs(workspace)
const localName = ref('')
const localPath = ref('')
const defaultBranch = ref('main')
const installations = ref<GitHubAppInstallation[]>([])
const availableRepositories = ref<GitHubRepositorySummary[]>([])
const selectedInstallationId = ref<number | null>(null)
const selectedRepositoryId = ref<number | null>(null)
const loadingGitHub = ref(false)
const connectingRepository = ref(false)
const formError = ref('')
const successMessage = ref(route.query.github === 'connected' ? 'GitHub account connected.' : '')
const editingRepositoryId = ref('')
const editName = ref('')
const editLocation = ref('')
const editDefaultBranch = ref('')

const canManageGitHub = computed(
  () =>
    workspace.hasPermission('repository.access.manage') &&
    workspace.hasPermission('repository.create'),
)

async function createLocalRepository(): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.createRepository({
      name: localName.value,
      provider: RepositoryProviderKind.Local,
      localPath: localPath.value,
      defaultBranch: defaultBranch.value,
    })
    localName.value = ''
    localPath.value = ''
    successMessage.value = 'Local repository added.'
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to add local repository.'
  }
}

function startRepositoryEdit(repository: ProjectRepository): void {
  editingRepositoryId.value = repository.id
  editName.value = repository.name
  editLocation.value = repository.remoteUrl ?? repository.localPath ?? ''
  editDefaultBranch.value = repository.defaultBranch
  formError.value = ''
  successMessage.value = ''
}

function cancelRepositoryEdit(): void {
  editingRepositoryId.value = ''
}

async function saveRepository(repository: ProjectRepository): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.updateRepository(repository.id, {
      name: editName.value,
      localPath:
        repository.provider === RepositoryProviderKind.Local ? editLocation.value : undefined,
      remoteUrl:
        repository.provider === RepositoryProviderKind.GitHub ? editLocation.value : undefined,
      defaultBranch: editDefaultBranch.value,
      status: repository.status,
    })
    editingRepositoryId.value = ''
    successMessage.value = `${editName.value.trim()} updated.`
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to update repository.'
  }
}

async function disableRepository(repository: ProjectRepository): Promise<void> {
  if (!window.confirm(`Disable ${repository.name}? Existing source trace will be preserved.`))
    return
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.disableRepository(repository.id)
    successMessage.value = `${repository.name} disabled.`
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to disable repository.'
  }
}

async function startGitHubConnection(): Promise<void> {
  if (!selectedProjectId.value) return
  formError.value = ''
  loadingGitHub.value = true
  try {
    const result = await tcflowApi.startGitHubConnection(selectedProjectId.value)
    externalNavigation.assign(result.installationUrl)
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to start GitHub connection.'
    loadingGitHub.value = false
  }
}

async function loadGitHubInstallations(): Promise<void> {
  if (!selectedProjectId.value || !canManageGitHub.value) return
  loadingGitHub.value = true
  formError.value = ''
  try {
    installations.value = await tcflowApi.gitHubInstallations(selectedProjectId.value)
    const requestedInstallation = Number(route.query.installation)
    const selected = installations.value.find(
      (item) => item.installationId === requestedInstallation,
    )
    selectedInstallationId.value =
      selected?.installationId ?? installations.value[0]?.installationId ?? null
    if (selectedInstallationId.value) await loadAvailableRepositories()
  } catch (error) {
    formError.value =
      error instanceof Error ? error.message : 'Unable to load GitHub installations.'
  } finally {
    loadingGitHub.value = false
  }
}

async function loadAvailableRepositories(): Promise<void> {
  if (!selectedProjectId.value || !selectedInstallationId.value) {
    availableRepositories.value = []
    selectedRepositoryId.value = null
    return
  }
  loadingGitHub.value = true
  formError.value = ''
  try {
    availableRepositories.value = await tcflowApi.gitHubRepositories(
      selectedProjectId.value,
      selectedInstallationId.value,
    )
    const connectedIds = new Set(
      repositories.value
        .filter((repository) => repository.provider === RepositoryProviderKind.GitHub)
        .map((repository) => repository.remoteUrl),
    )
    selectedRepositoryId.value =
      availableRepositories.value.find((repository) => !connectedIds.has(repository.htmlUrl))?.id ??
      null
  } catch (error) {
    availableRepositories.value = []
    formError.value = error instanceof Error ? error.message : 'Unable to load GitHub repositories.'
  } finally {
    loadingGitHub.value = false
  }
}

async function connectSelectedRepository(): Promise<void> {
  if (!selectedProjectId.value || !selectedInstallationId.value || !selectedRepositoryId.value)
    return
  connectingRepository.value = true
  formError.value = ''
  successMessage.value = ''
  try {
    const connected = await tcflowApi.connectGitHubRepository(
      selectedProjectId.value,
      selectedInstallationId.value,
      selectedRepositoryId.value,
    )
    await workspace.loadRepositories()
    await loadAvailableRepositories()
    successMessage.value = `${connected.repository.name} connected.`
    if (workspace.hasPermission('source.analyze')) {
      try {
        await tcflowApi.triggerInitialGitHubScan(selectedProjectId.value, connected.repository.id)
        successMessage.value += ' Initial analysis queued.'
      } catch (error) {
        formError.value =
          error instanceof Error
            ? `Repository connected, but initial analysis was not queued: ${error.message}`
            : 'Repository connected, but initial analysis was not queued.'
      }
    }
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to connect repository.'
  } finally {
    connectingRepository.value = false
  }
}

watch(
  [selectedProjectId, canManageGitHub],
  async ([projectId, canManage]) => {
    if (!projectId) return
    await workspace.loadRepositories()
    if (canManage) await loadGitHubInstallations()
  },
  { immediate: true },
)
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Repositories</h1>
      <p>Connect GitHub through an authorized App installation, or add a local source path.</p>
    </div>
  </section>

  <div v-if="successMessage" class="success-alert" role="status">{{ successMessage }}</div>
  <div v-if="formError" class="inline-alert page-alert" role="alert">{{ formError }}</div>

  <div class="content-split">
    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Project sources</span>
          <h2>Connected repositories</h2>
        </div>
      </div>
      <ResourceState
        :state="repositoriesState"
        empty-title="No repositories connected"
        empty-message="Install the GitHub App or add a local path to establish a source boundary."
        @retry="workspace.loadRepositories()"
      >
        <div class="item-list">
          <article
            v-for="repository in repositories"
            :key="repository.id"
            class="list-row list-row--static"
          >
            <form
              v-if="editingRepositoryId === repository.id"
              class="resource-editor"
              @submit.prevent="saveRepository(repository)"
            >
              <label>Name<input v-model="editName" required maxlength="150" /></label>
              <label
                >{{
                  repository.provider === RepositoryProviderKind.GitHub
                    ? 'Remote URL'
                    : 'Local path'
                }}<input v-model="editLocation" required
              /></label>
              <label>Default branch<input v-model="editDefaultBranch" required /></label>
              <span class="lifecycle-actions">
                <button class="primary-button" type="submit">Save</button>
                <button class="secondary-button" type="button" @click="cancelRepositoryEdit">
                  Cancel
                </button>
              </span>
            </form>
            <template v-else>
              <span class="avatar-mark">{{
                repository.provider === RepositoryProviderKind.GitHub ? 'GH' : 'LO'
              }}</span>
              <span>
                <strong>{{ repository.name }}</strong>
                <small
                  >{{ repository.remoteUrl || repository.localPath }} ·
                  {{ repository.defaultBranch }}</small
                >
              </span>
              <span class="lifecycle-actions">
                <span class="state-pill state-pill--planned">{{
                  repository.status === RepositoryLifecycleStatus.Pending
                    ? 'pending'
                    : repository.status === RepositoryLifecycleStatus.Active
                      ? 'active'
                      : 'disabled'
                }}</span>
                <button
                  v-if="workspace.hasPermission('repository.update')"
                  class="secondary-button"
                  type="button"
                  @click="startRepositoryEdit(repository)"
                >
                  Edit
                </button>
                <button
                  v-if="
                    repository.status !== RepositoryLifecycleStatus.Disabled &&
                    workspace.hasPermission('repository.delete')
                  "
                  class="danger-button"
                  type="button"
                  @click="disableRepository(repository)"
                >
                  Disable
                </button>
              </span>
            </template>
          </article>
        </div>
      </ResourceState>
    </section>

    <div class="connection-stack">
      <PermissionNotice
        :allowed="canManageGitHub"
        permission="repository.access.manage + repository.create"
      >
        <section class="form-card">
          <span class="eyebrow">Private repositories</span>
          <h2>GitHub App</h2>
          <p>
            Install TCFlow on your GitHub account, then choose only repositories that installation
            can access.
          </p>
          <button
            class="secondary-button"
            type="button"
            :disabled="loadingGitHub"
            @click="startGitHubConnection"
          >
            {{ installations.length ? 'Connect another GitHub account' : 'Connect GitHub account' }}
          </button>

          <template v-if="installations.length">
            <label>
              GitHub account
              <select v-model="selectedInstallationId" @change="loadAvailableRepositories">
                <option
                  v-for="installation in installations"
                  :key="installation.id"
                  :value="installation.installationId"
                >
                  {{ installation.accountLogin }}
                </option>
              </select>
            </label>
            <label>
              Repository
              <select v-model="selectedRepositoryId" :disabled="loadingGitHub">
                <option :value="null">Select a repository</option>
                <option
                  v-for="repository in availableRepositories"
                  :key="repository.id"
                  :value="repository.id"
                >
                  {{ repository.fullName }}{{ repository.private ? ' · private' : '' }}
                </option>
              </select>
            </label>
            <button
              class="primary-button"
              type="button"
              :disabled="!selectedRepositoryId || connectingRepository"
              @click="connectSelectedRepository"
            >
              {{ connectingRepository ? 'Connecting…' : 'Add selected repository' }}
            </button>
          </template>
        </section>
      </PermissionNotice>

      <PermissionNotice
        :allowed="workspace.hasPermission('repository.create')"
        permission="repository.create"
      >
        <form class="form-card" @submit.prevent="createLocalRepository">
          <span class="eyebrow">Local source</span>
          <h2>Add local repository</h2>
          <label
            >Name<input v-model="localName" required maxlength="150" placeholder="web-platform"
          /></label>
          <label
            >Local path<input v-model="localPath" required placeholder="/workspace/repo"
          /></label>
          <label>Default branch<input v-model="defaultBranch" required /></label>
          <button class="primary-button" type="submit">Add local repository</button>
        </form>
      </PermissionNotice>
    </div>
  </div>
</template>
