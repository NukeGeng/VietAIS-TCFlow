<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'

const workspace = useWorkspaceStore()
const { tasks, tasksState, transientFeatures, selectedProject } = storeToRefs(workspace)
const name = ref('')
const description = ref('')
const formError = ref('')

const taskFeatures = computed(() => {
  const groups = new Map<string, number>()
  for (const task of tasks.value) {
    if (task.featureId) groups.set(task.featureId, (groups.get(task.featureId) ?? 0) + 1)
  }
  return [...groups.entries()].map(([id, count]) => ({ id, count }))
})

async function createFeature(): Promise<void> {
  formError.value = ''
  try {
    await workspace.createFeature(name.value, description.value || undefined)
    name.value = ''
    description.value = ''
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to create feature.'
  }
}

workspace.loadTasks()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">{{ selectedProject?.name }}</span>
      <h1>Features</h1>
      <p>Group delivery work by business capability without losing source trace.</p>
    </div>
  </section>
  <div class="content-split">
    <section class="panel">
      <ResourceState
        :state="tasksState"
        empty-title="No feature-linked tasks"
        empty-message="Create a feature, then link tasks through the verified task contract."
        @retry="workspace.loadTasks()"
      >
        <div class="feature-grid">
          <article v-for="feature in transientFeatures" :key="feature.id">
            <span class="eyebrow">New feature</span>
            <h2>{{ feature.name }}</h2>
            <p>{{ feature.description || 'No description.' }}</p>
            <code>{{ feature.id }}</code>
          </article>
          <article v-for="feature in taskFeatures" :key="feature.id">
            <span class="eyebrow">Confirmed task link</span>
            <h2>{{ feature.count }} linked {{ feature.count === 1 ? 'task' : 'tasks' }}</h2>
            <p>The current P3 read contract exposes this feature identity through its tasks.</p>
            <code>{{ feature.id }}</code>
          </article>
        </div>
        <div v-if="!transientFeatures.length && !taskFeatures.length" class="state-panel">
          <span class="state-icon">0</span>
          <div>
            <strong>No visible features</strong>
            <p>
              P3 has a create contract but no standalone feature-list contract; this view
              conservatively derives confirmed links from tasks.
            </p>
          </div>
        </div>
      </ResourceState>
    </section>
    <PermissionNotice
      :allowed="workspace.hasPermission('feature.create')"
      permission="feature.create"
    >
      <form class="form-card" @submit.prevent="createFeature">
        <span class="eyebrow">Capability model</span>
        <h2>Create feature</h2>
        <label>Name<input v-model="name" maxlength="150" required /></label
        ><label>Description<textarea v-model="description" rows="5"></textarea></label>
        <div v-if="formError" class="inline-alert" role="alert">{{ formError }}</div>
        <button class="primary-button" type="submit">Create feature</button>
      </form>
    </PermissionNotice>
  </div>
</template>
