<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import PermissionNotice from '../components/PermissionNotice.vue'
import ResourceState from '../components/ResourceState.vue'
import { useWorkspaceStore } from '../stores/workspace'
import type { ProjectFeature } from '../types/contracts'

const workspace = useWorkspaceStore()
const { tasks, features, featuresState, selectedProject } = storeToRefs(workspace)
const name = ref('')
const description = ref('')
const formError = ref('')
const successMessage = ref('')
const editingFeatureId = ref('')
const editName = ref('')
const editDescription = ref('')

const taskFeatures = computed(() => {
  const groups = new Map<string, number>()
  for (const task of tasks.value) {
    if (task.featureId) groups.set(task.featureId, (groups.get(task.featureId) ?? 0) + 1)
  }
  return [...groups.entries()].map(([id, count]) => ({ id, count }))
})

async function createFeature(): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.createFeature(name.value, description.value || undefined)
    name.value = ''
    description.value = ''
    successMessage.value = 'Feature created and audited.'
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to create feature.'
  }
}

function startFeatureEdit(feature: ProjectFeature): void {
  editingFeatureId.value = feature.id
  editName.value = feature.name
  editDescription.value = feature.description ?? ''
  formError.value = ''
  successMessage.value = ''
}

async function saveFeature(): Promise<void> {
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.updateFeature(
      editingFeatureId.value,
      editName.value,
      editDescription.value || undefined,
    )
    editingFeatureId.value = ''
    successMessage.value = 'Feature updated and audited.'
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to update feature.'
  }
}

async function deleteFeature(feature: ProjectFeature): Promise<void> {
  if (!window.confirm(`Delete ${feature.name}? Referenced features cannot be deleted.`)) return
  formError.value = ''
  successMessage.value = ''
  try {
    await workspace.deleteFeature(feature.id)
    successMessage.value = `${feature.name} deleted.`
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to delete feature.'
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
  <div v-if="successMessage" class="success-alert" role="status">{{ successMessage }}</div>
  <div v-if="formError" class="inline-alert page-alert" role="alert">{{ formError }}</div>
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
            <form
              v-if="editingFeatureId === feature.id"
              class="resource-editor"
              @submit.prevent="saveFeature"
            >
              <label>Name<input v-model="editName" required maxlength="150" /></label>
              <label>Description<textarea v-model="editDescription" rows="4"></textarea></label>
              <span class="lifecycle-actions">
                <button class="primary-button" type="submit">Save</button>
                <button class="secondary-button" type="button" @click="editingFeatureId = ''">
                  Cancel
                </button>
              </span>
            </form>
            <template v-else>
              <span class="eyebrow">Persisted capability</span>
              <h2>{{ feature.name }}</h2>
              <p>{{ feature.description || 'No description.' }}</p>
              <small>
                {{ taskFeatures.find((item) => item.id === feature.id)?.count || 0 }} linked tasks
              </small>
              <span class="lifecycle-actions">
                <button
                  v-if="workspace.hasPermission('feature.update')"
                  class="secondary-button"
                  type="button"
                  @click="startFeatureEdit(feature)"
                >
                  Edit
                </button>
                <button
                  v-if="workspace.hasPermission('feature.delete')"
                  class="danger-button"
                  type="button"
                  @click="deleteFeature(feature)"
                >
                  Delete
                </button>
              </span>
            </template>
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
        <button class="primary-button" type="submit">Create feature</button>
      </form>
    </PermissionNotice>
  </div>
</template>
