<script setup lang="ts">
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from './stores/session'
import { useWorkspaceStore } from './stores/workspace'

const route = useRoute()
const router = useRouter()
const session = useSessionStore()
const workspace = useWorkspaceStore()
const { profile, isAuthenticated } = storeToRefs(session)
const { projects, selectedProjectId, selectedProject } = storeToRefs(workspace)
const isPublic = computed(() => Boolean(route.meta.public))

const projectNavigation = computed(() => {
  const projectId = selectedProjectId.value
  if (!projectId) return []
  return [
    {
      label: 'Repositories',
      to: `/projects/${projectId}/repositories`,
      permission: 'repository.view',
    },
    { label: 'Analysis', to: `/projects/${projectId}/analysis`, permission: 'analysis.view' },
    { label: 'Impact graph', to: `/projects/${projectId}/impacts`, permission: 'task.view' },
    { label: 'Features', to: `/projects/${projectId}/features`, permission: 'task.view' },
    { label: 'Task board', to: `/projects/${projectId}/tasks`, permission: 'task.view' },
    { label: 'Project admin', to: `/projects/${projectId}/admin`, permission: 'role.view' },
  ]
})

function changeProject(event: Event): void {
  const projectId = (event.target as HTMLSelectElement).value
  workspace.selectProject(projectId || null)
  if (projectId && profile.value) {
    workspace.loadPermissions(profile.value.id)
    router.push(`/projects/${projectId}/tasks`)
  }
}

function logout(): void {
  session.logout()
  workspace.selectProject(null)
  router.replace('/login')
}

watch(
  () => route.params.projectId,
  async (routeProjectId) => {
    if (!isAuthenticated.value) return
    const projectId = typeof routeProjectId === 'string' ? routeProjectId : selectedProjectId.value
    if (projectId && projectId !== selectedProjectId.value) workspace.selectProject(projectId)
    if (!projects.value.length) await workspace.loadProjects()
    if (workspace.selectedProjectId && profile.value)
      await workspace.loadPermissions(profile.value.id)
  },
  { immediate: true },
)

watch(isAuthenticated, (authenticated) => {
  if (!authenticated && !route.meta.public) {
    router.replace({ name: 'login', query: { redirect: route.fullPath } })
  }
})
</script>

<template>
  <RouterView v-if="isPublic" />

  <template v-else>
    <a class="skip-link" href="#main-content">Skip to content</a>
    <div class="app-shell">
      <aside class="sidebar">
        <RouterLink class="brand" to="/" aria-label="VietAIS TCFlow home">
          <span class="brand-mark" aria-hidden="true">TC</span>
          <span><strong>VietAIS</strong><small>TCFlow</small></span>
        </RouterLink>

        <nav aria-label="Primary navigation">
          <span class="nav-label">Workspace</span>
          <RouterLink to="/">Dashboard</RouterLink>
          <RouterLink to="/projects">Projects</RouterLink>

          <template v-if="selectedProjectId">
            <span class="nav-label">Project</span>
            <template v-for="item in projectNavigation" :key="item.to">
              <RouterLink v-if="workspace.hasPermission(item.permission)" :to="item.to">{{
                item.label
              }}</RouterLink>
              <span v-else class="nav-disabled" :title="`Requires ${item.permission}`"
                >{{ item.label }}<small>{{ item.permission }}</small></span
              >
            </template>
          </template>

          <template v-if="session.hasSystemPermission('Permissions.Users.View')">
            <span class="nav-label">Platform</span>
            <RouterLink to="/system">System admin</RouterLink>
          </template>
        </nav>

        <div class="sidebar-note">
          <span class="status-dot" aria-hidden="true"></span>
          <span><strong>Backend enforced</strong><small>Scoped authorization</small></span>
        </div>
      </aside>

      <div class="workspace">
        <header class="topbar">
          <label class="project-switcher">
            <span class="sr-only">Selected project</span>
            <select :value="selectedProjectId || ''" @change="changeProject">
              <option value="">Select a project</option>
              <option v-for="project in projects" :key="project.id" :value="project.id">
                {{ project.name }}
              </option>
            </select>
          </label>
          <div class="topbar-context">
            <span class="eyebrow">Source-aware planner</span
            ><strong>{{ selectedProject?.name || 'Portfolio' }}</strong>
          </div>
          <div class="account-menu">
            <span class="avatar-mark">{{
              (profile?.firstName || profile?.userName || 'U').slice(0, 2).toUpperCase()
            }}</span
            ><span
              ><strong>{{ profile?.firstName || profile?.userName }}</strong
              ><small>{{ profile?.email }}</small></span
            ><button type="button" @click="logout">Sign out</button>
          </div>
        </header>

        <main id="main-content"><RouterView /></main>
      </div>
    </div>
  </template>
</template>
