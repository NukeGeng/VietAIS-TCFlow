<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'

const workspace = useWorkspaceStore()
const { tasks, features, featuresState, selectedProject } = storeToRefs(workspace)
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

Promise.all([workspace.loadTasks(), workspace.loadFeatures()])
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
        :state="featuresState"
        empty-title="No features"
        empty-message="Create a feature to group source-aware delivery work."
        @retry="workspace.loadFeatures()"
      >
        <div class="feature-grid">
          <article v-for="feature in features" :key="feature.id">
            <span class="eyebrow">Persisted capability</span>
            <h2>{{ feature.name }}</h2>
            <p>{{ feature.description || 'No description.' }}</p>
            <small>
              {{ taskFeatures.find((item) => item.id === feature.id)?.count || 0 }} linked tasks
            </small>
          </article>
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
