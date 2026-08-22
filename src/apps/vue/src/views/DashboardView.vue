<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import ResourceState from '../components/ResourceState.vue'
import { useSessionStore } from '../stores/session'
import { useWorkspaceStore } from '../stores/workspace'
import { TaskLifecycleStatus } from '../types/contracts'

const session = useSessionStore()
const workspace = useWorkspaceStore()
const { profile } = storeToRefs(session)
const { projects, selectedProject, tasks, tasksState, repositories } = storeToRefs(workspace)

const activeTasks = computed(
  () => tasks.value.filter((task) => task.status === TaskLifecycleStatus.InProgress).length,
)
const tracedTasks = computed(
  () =>
    tasks.value.filter(
      (task) => task.sourceTrace.sourceChangeId || task.sourceTrace.artifactIds.length,
    ).length,
)

async function load(): Promise<void> {
  if (!projects.value.length) await workspace.loadProjects()
  if (workspace.selectedProjectId) {
    await Promise.all([workspace.loadTasks(), workspace.loadRepositories()])
  }
}

onMounted(load)
</script>

<template>
  <section class="page-heading page-heading--dashboard">
    <div>
      <span class="eyebrow">Engineering intelligence</span>
      <h1>
        Good {{ new Date().getHours() < 12 ? 'morning' : 'afternoon' }},
        {{ profile?.firstName || profile?.userName || 'builder' }}.
      </h1>
      <p>See what changed, what needs attention, and why each task exists.</p>
    </div>
    <RouterLink class="primary-button" to="/projects">Manage projects</RouterLink>
  </section>

  <div class="metric-grid" aria-label="Workspace summary">
    <article>
      <span>Projects</span><strong>{{ projects.length }}</strong
      ><small>visible memberships</small>
    </article>
    <article>
      <span>Repositories</span><strong>{{ repositories.length }}</strong
      ><small>in selected project</small>
    </article>
    <article>
      <span>In progress</span><strong>{{ activeTasks }}</strong
      ><small>tasks moving now</small>
    </article>
    <article>
      <span>Source traced</span><strong>{{ tracedTasks }}</strong
      ><small>tasks backed by evidence</small>
    </article>
  </div>

  <section class="dashboard-grid">
    <article class="panel panel--dark">
      <span class="eyebrow">Current project</span>
      <h2>{{ selectedProject?.name || 'Select a project' }}</h2>
      <p v-if="selectedProject">
        Project contracts, repositories, impact, and delivery state are scoped here.
      </p>
      <p v-else>Create or select a project to begin tracing delivery decisions.</p>
      <RouterLink :to="selectedProject ? `/projects/${selectedProject.id}/tasks` : '/projects'">
        {{ selectedProject ? 'Open task board' : 'Choose project' }}
      </RouterLink>
    </article>

    <article class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Evidence health</span>
          <h2>Trace coverage</h2>
        </div>
        <span class="evidence-badge">CONFIRMED</span>
      </div>
      <ResourceState
        :state="tasksState"
        empty-title="No tasks yet"
        empty-message="Create a task to start measuring evidence coverage."
        @retry="load"
      >
        <div class="coverage-meter">
          <strong>{{ tasks.length ? Math.round((tracedTasks / tasks.length) * 100) : 0 }}%</strong>
          <div>
            <span
              :style="{ width: `${tasks.length ? (tracedTasks / tasks.length) * 100 : 0}%` }"
            ></span>
          </div>
          <small>{{ tracedTasks }} of {{ tasks.length }} tasks include source evidence.</small>
        </div>
      </ResourceState>
    </article>
  </section>
</template>
