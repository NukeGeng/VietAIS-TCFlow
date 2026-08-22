<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'

const workspace = useWorkspaceStore()
const { tasks, tasksState, selectedProject } = storeToRefs(workspace)

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

workspace.loadTasks()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Analysis</h1>
      <p>
        Deterministic source facts already attached to tasks. Inference is never presented as
        confirmed truth.
      </p>
    </div>
    <span class="evidence-badge">CONFIRMED TASK TRACE</span>
  </section>
  <ResourceState
    :state="tasksState"
    empty-title="No analyzed task evidence"
    empty-message="Analyzer phases will populate source change, artifact, contract, and impact evidence."
    @retry="workspace.loadTasks()"
  >
    <div class="metric-grid metric-grid--three">
      <article>
        <span>Traced changes</span
        ><strong>{{ tasks.filter((task) => task.sourceTrace.sourceChangeId).length }}</strong
        ><small>source revisions</small>
      </article>
      <article>
        <span>Artifacts</span
        ><strong>{{ new Set(tasks.flatMap((task) => task.sourceTrace.artifactIds)).size }}</strong
        ><small>document identities</small>
      </article>
      <article>
        <span>Evidence</span><strong>{{ evidenceCount }}</strong
        ><small>auditable records</small>
      </article>
    </div>
    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Affected surface</span>
          <h2>Artifacts named by tasks</h2>
        </div>
      </div>
      <div v-if="artifacts.length" class="artifact-table">
        <div v-for="artifact in artifacts" :key="artifact.name">
          <code>{{ artifact.name }}</code
          ><span>{{ artifact.count }} {{ artifact.count === 1 ? 'task' : 'tasks' }}</span>
        </div>
      </div>
      <div v-else class="state-panel">
        <span class="state-icon">0</span>
        <div>
          <strong>No named artifacts</strong>
          <p>Tasks exist, but no affected artifact names have been recorded yet.</p>
        </div>
      </div>
    </section>
  </ResourceState>
</template>
