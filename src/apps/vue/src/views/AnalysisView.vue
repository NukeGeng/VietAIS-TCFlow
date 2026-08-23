<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import ResourceState from '../components/ResourceState.vue'
import { ApiError } from '../services/http'
import { tcflowApi } from '../services/tcflow-api'
import { useWorkspaceStore } from '../stores/workspace'
import {
  GitHubAnalysisRequestStatus,
  RepositoryAnalysisRunStatus,
  RepositoryProviderKind,
  type ProjectRepository,
  type RepositoryAnalysisDetails,
  type ResourceState as ViewState,
} from '../types/contracts'

const workspace = useWorkspaceStore()
const { repositories, selectedProject, selectedProjectId, tasks, tasksState } =
  storeToRefs(workspace)
const analysisState = ref<ViewState>({ status: 'idle' })
const analyses = ref<Record<string, RepositoryAnalysisDetails | null>>({})
let refreshTimer: ReturnType<typeof setTimeout> | undefined

const githubRepositories = computed(() =>
  repositories.value.filter((repository) => repository.provider === RepositoryProviderKind.GitHub),
)
const artifacts = computed(() => {
  const counts = new Map<string, number>()
  for (const task of tasks.value) {
    for (const artifact of task.affectedArtifacts)
      counts.set(artifact, (counts.get(artifact) ?? 0) + 1)
  }
  return [...counts.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((left, right) => right.count - left.count)
})
const evidenceCount = computed(() =>
  tasks.value.reduce((total, task) => total + task.sourceTrace.evidenceIds.length, 0),
)

function statusLabel(details: RepositoryAnalysisDetails | null): string {
  if (!details) return 'Not requested'
  if (!details.run) return GitHubAnalysisRequestStatus[details.request.status]
  return RepositoryAnalysisRunStatus[details.run.status]
}

function statusClass(details: RepositoryAnalysisDetails | null): string {
  const label = statusLabel(details).toLowerCase()
  if (label === 'completed') return 'analysis-status--completed'
  if (label === 'unsupported') return 'analysis-status--unsupported'
  if (label === 'failed') return 'analysis-status--failed'
  return 'analysis-status--pending'
}

function isInProgress(details: RepositoryAnalysisDetails | null): boolean {
  return (
    details?.request.status === GitHubAnalysisRequestStatus.Pending ||
    details?.request.status === GitHubAnalysisRequestStatus.Processing ||
    details?.run?.status === RepositoryAnalysisRunStatus.Processing
  )
}

function repositoryTitle(repository: ProjectRepository): string {
  return repository.name.includes('/') ? repository.name : repository.remoteUrl || repository.name
}

function scheduleRefresh(): void {
  if (refreshTimer) clearTimeout(refreshTimer)
  const shouldRefresh = Object.values(analyses.value).some(isInProgress)
  if (shouldRefresh) refreshTimer = setTimeout(() => loadAnalyses(false), 2_000)
}

async function loadAnalyses(showLoading = true): Promise<void> {
  if (!selectedProjectId.value) return
  if (!workspace.hasPermission('source.analyze')) {
    analysisState.value = {
      status: 'forbidden',
      message: 'The source.analyze permission is required to inspect repository analysis.',
    }
    return
  }
  if (showLoading) analysisState.value = { status: 'loading' }
  try {
    await workspace.loadRepositories()
    if (!githubRepositories.value.length) {
      analyses.value = {}
      analysisState.value = { status: 'empty' }
      return
    }

    const entries = await Promise.all(
      githubRepositories.value.map(async (repository) => {
        try {
          const details = await tcflowApi.latestRepositoryAnalysis(
            selectedProjectId.value!,
            repository.id,
          )
          return [repository.id, details] as const
        } catch (error) {
          if (error instanceof ApiError && error.status === 404)
            return [repository.id, null] as const
          throw error
        }
      }),
    )
    analyses.value = Object.fromEntries(entries)
    analysisState.value = { status: 'ready' }
    scheduleRefresh()
  } catch (error) {
    analysisState.value = {
      status: error instanceof ApiError && error.status === 403 ? 'forbidden' : 'error',
      message: error instanceof Error ? error.message : 'Unable to load repository analysis.',
    }
  }
}

watch(
  selectedProjectId,
  () => {
    analyses.value = {}
    void Promise.all([loadAnalyses(), workspace.loadTasks()])
  },
  { immediate: true },
)

onUnmounted(() => {
  if (refreshTimer) clearTimeout(refreshTimer)
})
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Analysis</h1>
      <p>
        Repository scan status and deterministic source facts. Unsupported source is reported
        explicitly and never converted into invented tasks.
      </p>
    </div>
    <span class="evidence-badge">SOURCE-AWARE</span>
  </section>

  <ResourceState
    :state="analysisState"
    empty-title="No GitHub repositories to analyze"
    empty-message="Connect a GitHub repository, then queue its initial analysis."
    @retry="loadAnalyses()"
  >
    <div class="analysis-list">
      <article
        v-for="repository in githubRepositories"
        :key="repository.id"
        class="panel analysis-card"
      >
        <div class="section-heading section-heading--compact">
          <div>
            <span class="eyebrow">GitHub repository</span>
            <h2>{{ repositoryTitle(repository) }}</h2>
          </div>
          <span :class="['analysis-status', statusClass(analyses[repository.id] ?? null)]">
            {{ statusLabel(analyses[repository.id] ?? null) }}
          </span>
        </div>

        <template v-if="analyses[repository.id]?.run">
          <div class="metric-grid analysis-metrics">
            <article>
              <span>Artifacts</span
              ><strong>{{ analyses[repository.id]!.run!.artifactCount }}</strong>
            </article>
            <article>
              <span>Dependencies</span
              ><strong>{{ analyses[repository.id]!.run!.dependencyCount }}</strong>
            </article>
            <article>
              <span>Contracts</span
              ><strong>{{ analyses[repository.id]!.run!.contractCount }}</strong>
            </article>
            <article>
              <span>Tasks created</span
              ><strong>{{ analyses[repository.id]!.run!.generatedTaskCount }}</strong>
            </article>
          </div>
          <p class="analysis-meta">
            Technologies:
            <strong>{{
              analyses[repository.id]!.run!.technologies.join(', ') || 'None detected'
            }}</strong>
            <template v-if="analyses[repository.id]!.run!.sourceRevision">
              · Revision
              <code>{{ analyses[repository.id]!.run!.sourceRevision!.slice(0, 12) }}</code>
            </template>
          </p>
          <div
            v-if="analyses[repository.id]!.run!.status === RepositoryAnalysisRunStatus.Unsupported"
            class="state-panel state-panel--warning analysis-message"
          >
            <span class="state-icon" aria-hidden="true">!</span>
            <div>
              <strong>Repository stack is outside analyzer V1</strong>
              <p>
                TCFlow V1 supports Vue, ASP.NET Core, and Marten source facts. No task was created
                because this repository did not contain supported facts.
              </p>
            </div>
          </div>
          <div
            v-else-if="analyses[repository.id]!.run!.status === RepositoryAnalysisRunStatus.Failed"
            class="state-panel state-panel--danger analysis-message"
            role="alert"
          >
            <span class="state-icon" aria-hidden="true">!</span>
            <div>
              <strong>{{ analyses[repository.id]!.run!.errorCode || 'Analysis failed' }}</strong>
              <p>{{ analyses[repository.id]!.run!.errorMessage }}</p>
            </div>
          </div>
          <div v-if="analyses[repository.id]!.run!.diagnostics.length" class="analysis-diagnostics">
            <div
              v-for="diagnostic in analyses[repository.id]!.run!.diagnostics"
              :key="`${diagnostic.code}-${diagnostic.path || ''}`"
            >
              <code>{{ diagnostic.code }}</code>
              <span>{{ diagnostic.message }}</span>
              <small>{{ diagnostic.evidenceLevel }}</small>
            </div>
          </div>
        </template>
        <div
          v-else-if="isInProgress(analyses[repository.id] ?? null)"
          class="state-panel analysis-message"
        >
          <span class="loader" aria-hidden="true"></span>
          <div>
            <strong
              >Analysis is {{ statusLabel(analyses[repository.id] ?? null).toLowerCase() }}</strong
            >
            <p>This view refreshes automatically while the source snapshot is processed.</p>
          </div>
        </div>
        <div v-else class="state-panel analysis-message">
          <span class="state-icon" aria-hidden="true">0</span>
          <div>
            <strong>No analysis has been requested</strong>
            <p>Reconnect or queue an initial scan from the repository workflow.</p>
          </div>
        </div>
      </article>
    </div>
  </ResourceState>

  <section class="panel task-evidence-panel">
    <div class="section-heading section-heading--compact">
      <div>
        <span class="eyebrow">Task trace</span>
        <h2>Confirmed evidence attached to tasks</h2>
      </div>
    </div>
    <ResourceState
      :state="tasksState"
      empty-title="No analyzed task evidence"
      empty-message="A completed scan does not necessarily create tasks. Tasks are created only for supported, actionable evidence."
      @retry="workspace.loadTasks()"
    >
      <div class="metric-grid metric-grid--three">
        <article>
          <span>Traced changes</span
          ><strong>{{ tasks.filter((task) => task.sourceTrace.sourceChangeId).length }}</strong
          ><small>source revisions</small>
        </article>
        <article>
          <span>Named artifacts</span><strong>{{ artifacts.length }}</strong
          ><small>task affected surface</small>
        </article>
        <article>
          <span>Evidence</span><strong>{{ evidenceCount }}</strong
          ><small>auditable records</small>
        </article>
      </div>
    </ResourceState>
  </section>
</template>
