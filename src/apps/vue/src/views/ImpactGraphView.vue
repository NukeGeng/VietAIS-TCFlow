<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'

const workspace = useWorkspaceStore()
const { tasks, tasksState, selectedProject } = storeToRefs(workspace)
const impacted = computed(() => tasks.value.filter((task) => task.sourceTrace.impactIds.length > 0))
const totalImpacts = computed(() =>
  impacted.value.reduce((total, task) => total + task.sourceTrace.impactIds.length, 0),
)

workspace.loadTasks()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Impact graph</h1>
      <p>Explore confirmed links from source change to impacted delivery work.</p>
    </div>
  </section>
  <ResourceState
    :state="tasksState"
    empty-title="No impact graph yet"
    empty-message="Source analyzers will add impact evidence without guessing from cosmetic changes."
    @retry="workspace.loadTasks()"
  >
    <section v-if="impacted.length" class="impact-canvas" aria-label="Impact relationship graph">
      <div class="graph-origin">
        <span>CHANGE</span><strong>{{ impacted.length }} traced sources</strong>
      </div>
      <div class="graph-line" aria-hidden="true"></div>
      <div class="graph-nodes">
        <RouterLink
          v-for="task in impacted"
          :key="task.id"
          :to="`/projects/${task.projectId}/tasks/${task.id}`"
          class="graph-node"
        >
          <span>{{ task.sourceTrace.impactIds.length }} impacts</span
          ><strong>{{ task.title }}</strong
          ><small>{{
            task.affectedArtifacts.slice(0, 2).join(', ') || 'Document identities attached'
          }}</small>
        </RouterLink>
      </div>
      <aside>
        <span class="eyebrow">Graph summary</span><strong>{{ totalImpacts }}</strong>
        <p>confirmed impact references across {{ impacted.length }} tasks</p>
      </aside>
    </section>
    <div v-else class="state-panel">
      <span class="state-icon">0</span>
      <div>
        <strong>No confirmed impacts</strong>
        <p>Tasks are available, but none contains a source impact identity.</p>
      </div>
    </div>
  </ResourceState>
</template>
