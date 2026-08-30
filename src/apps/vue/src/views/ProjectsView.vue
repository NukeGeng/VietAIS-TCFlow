<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'
import ResourceState from '../components/ResourceState.vue'
import { useSessionStore } from '../stores/session'
import { useWorkspaceStore } from '../stores/workspace'

const session = useSessionStore()
const workspace = useWorkspaceStore()
const router = useRouter()
const { profile } = storeToRefs(session)
const { projects, selectedProjectId, projectsState } = storeToRefs(workspace)
const name = ref('')
const formError = ref('')

async function createProject(): Promise<void> {
  if (!profile.value) return
  formError.value = ''
  try {
    await workspace.createProject(name.value, profile.value.id)
    name.value = ''
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to create project.'
  }
}

async function openProject(projectId: string): Promise<void> {
  if (!profile.value) return
  await workspace.activateProject(projectId, profile.value.id)
  await router.push(`/projects/${projectId}/tasks`)
}

onMounted(() => {
  if (!projects.value.length) void workspace.loadProjects()
})
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">Workspace portfolio</span>
      <h1>Projects</h1>
      <p>Membership is the boundary for every repository, permission, and task.</p>
    </div>
  </section>

  <div class="content-split">
    <section class="panel" aria-labelledby="project-list-heading">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Visible memberships</span>
          <h2 id="project-list-heading">Your projects</h2>
        </div>
        <span class="count-badge">{{ projects.length }}</span>
      </div>
      <ResourceState
        :state="projectsState"
        empty-title="No project memberships"
        empty-message="Create a project to initialize its owner, authority, convention, AI policy, and audit state."
        @retry="workspace.loadProjects()"
      >
        <div class="item-list">
          <RouterLink
            v-for="project in projects"
            :key="project.id"
            :class="['list-row', { 'list-row--selected': project.id === selectedProjectId }]"
            :to="`/projects/${project.id}/tasks`"
            @click.prevent="openProject(project.id)"
          >
            <span class="avatar-mark">{{ project.name.slice(0, 2).toUpperCase() }}</span>
            <span
              ><strong>{{ project.name }}</strong
              ><small
                >Owner {{ project.primaryOwnerId.slice(0, 8) }} ·
                {{ new Date(project.createdAt).toLocaleDateString() }}</small
              ></span
            >
            <span aria-hidden="true">→</span>
          </RouterLink>
        </div>
      </ResourceState>
    </section>

    <form class="form-card" @submit.prevent="createProject">
      <span class="eyebrow">Atomic bootstrap</span>
      <h2>Create project</h2>
      <p>
        The creator becomes Project Owner. Default authority and AI policy are stored in the same
        transaction.
      </p>
      <label
        >Project name<input
          v-model="name"
          minlength="2"
          maxlength="150"
          required
          placeholder="Payments workspace"
      /></label>
      <div v-if="formError" class="inline-alert" role="alert">{{ formError }}</div>
      <button class="primary-button" type="submit">Create project</button>
    </form>
  </div>
</template>
