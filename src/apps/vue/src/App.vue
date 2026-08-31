<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useLocaleStore } from './stores/locale'
import { useSessionStore } from './stores/session'
import { useWorkspaceStore } from './stores/workspace'
import { projectNavigationFor } from './modules/navigation'

const route = useRoute()
const router = useRouter()
const localeStore = useLocaleStore()
const session = useSessionStore()
const workspace = useWorkspaceStore()
const { profile, isAuthenticated } = storeToRefs(session)
const { locale: selectedLocale } = storeToRefs(localeStore)
const { projects, selectedProjectId, selectedProject } = storeToRefs(workspace)
const isPublic = computed(() => Boolean(route.meta.public))
const isSidebarOpen = ref(false)

const projectNavigation = computed(() => {
  const projectId = selectedProjectId.value
  if (!projectId) return []
  return projectNavigationFor(projectId).map((item) => ({ ...item, to: item.path }))
})

function canNavigate(item: { permission: string; additionalPermissions?: string[] }): boolean {
  return [item.permission, ...(item.additionalPermissions ?? [])].some((permission) =>
    workspace.hasPermission(permission),
  )
}

async function changeProject(event: Event): Promise<void> {
  const projectId = (event.target as HTMLSelectElement).value
  if (projectId && profile.value) {
    await workspace.activateProject(projectId, profile.value.id)
    await router.push(`/projects/${projectId}/tasks`)
    return
  }

  workspace.selectProject(null)
}

function changeLocale(event: Event): void {
  localeStore.setLocale((event.target as HTMLSelectElement).value)
}

function logout(): void {
  session.logout()
  workspace.selectProject(null)
  router.replace('/login')
}

const mobileNavigation = computed(() => {
  const projectId = selectedProjectId.value
  return [
    { label: localeStore.t('nav.dashboard'), to: '/', icon: '⌂' },
    { label: localeStore.t('nav.projects'), to: '/projects', icon: '▦' },
    ...(projectId
      ? [
          { label: localeStore.t('nav.taskBoard'), to: `/projects/${projectId}/tasks`, icon: '✓' },
          {
            label: localeStore.t('nav.repositories'),
            to: `/projects/${projectId}/repositories`,
            icon: '◈',
          },
        ]
      : []),
  ]
})

async function syncProjectContext(routeProjectId: unknown): Promise<void> {
  if (!isAuthenticated.value) return

  const requestedProjectId = typeof routeProjectId === 'string' ? routeProjectId : undefined
  if (requestedProjectId && requestedProjectId !== selectedProjectId.value) {
    // Set the route context immediately so the sidebar can render while the
    // project list and permissions are being hydrated.
    workspace.selectProject(requestedProjectId)
  }

  // The shell owns project hydration so every project route gets the same
  // selected project and permission lifecycle, including a direct deep-link.
  if (
    !projects.value.length ||
    (requestedProjectId && !projects.value.some((project) => project.id === requestedProjectId))
  ) {
    await workspace.loadProjects()
  }

  if (
    requestedProjectId &&
    projects.value.some((project) => project.id === requestedProjectId) &&
    requestedProjectId !== selectedProjectId.value
  ) {
    workspace.selectProject(requestedProjectId)
  }

  if (workspace.selectedProjectId && profile.value) {
    await workspace.activateProject(workspace.selectedProjectId, profile.value.id)
  }
}

watch(
  [() => route.params.projectId, selectedProjectId, isAuthenticated],
  ([routeProjectId, , authenticated]) => {
    if (authenticated) void syncProjectContext(routeProjectId)
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
    <a class="skip-link" href="#main-content">{{ localeStore.t('common.skipToContent') }}</a>
    <div class="app-shell">
      <aside class="sidebar" :class="{ 'sidebar--open': isSidebarOpen }">
        <div class="sidebar-head">
          <RouterLink
            class="brand"
            to="/"
            :aria-label="localeStore.t('common.home')"
            @click="isSidebarOpen = false"
          >
            <span class="brand-mark" aria-hidden="true">TC</span>
            <span><strong>VietAIS</strong><small>TCFlow</small></span>
          </RouterLink>
          <span class="brand-status" :aria-label="localeStore.t('common.workspaceOnline')"></span>
        </div>

        <div v-if="selectedProject" class="sidebar-project">
          <span class="sidebar-project__label">{{ localeStore.t('common.activeProject') }}</span>
          <strong>{{ selectedProject.name }}</strong>
        </div>

        <nav :aria-label="localeStore.t('common.primaryNavigation')">
          <span class="nav-label">{{ localeStore.t('section.workspace') }}</span>
          <RouterLink to="/" @click="isSidebarOpen = false">
            <span class="nav-icon" aria-hidden="true">⌂</span>{{ localeStore.t('nav.dashboard') }}
          </RouterLink>
          <RouterLink to="/projects" @click="isSidebarOpen = false">
            <span class="nav-icon" aria-hidden="true">▦</span>{{ localeStore.t('nav.projects') }}
          </RouterLink>

          <template v-if="selectedProjectId">
            <span class="nav-label">{{ localeStore.t('section.project') }}</span>
            <template v-for="item in projectNavigation" :key="item.to">
              <RouterLink v-if="canNavigate(item)" :to="item.to" @click="isSidebarOpen = false">
                <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span>
                {{ localeStore.t(`nav.${item.key}`) }}
              </RouterLink>
              <span
                v-else
                class="nav-disabled"
                :title="`${localeStore.t('common.requires')} ${item.permission}`"
              >
                <span
                  ><span class="nav-icon" aria-hidden="true">·</span
                  >{{ localeStore.t(`nav.${item.key}`) }}</span
                >
                <small>{{ item.permission }}</small>
              </span>
            </template>
          </template>

          <template v-if="session.hasSystemPermission('Permissions.Users.View')">
            <span class="nav-label">{{ localeStore.t('section.platform') }}</span>
            <RouterLink to="/system" @click="isSidebarOpen = false">
              <span class="nav-icon" aria-hidden="true">◉</span
              >{{ localeStore.t('nav.systemAdmin') }}
            </RouterLink>
          </template>
        </nav>

        <div class="sidebar-note">
          <span class="status-dot" aria-hidden="true"></span>
          <span
            ><strong>{{ localeStore.t('common.backendEnforced') }}</strong
            ><small>{{ localeStore.t('common.scopedAuthorization') }}</small></span
          >
        </div>
      </aside>

      <div class="workspace">
        <header class="topbar">
          <button
            class="sidebar-toggle"
            type="button"
            :aria-label="localeStore.t('common.toggleNavigation')"
            :aria-expanded="isSidebarOpen"
            @click="isSidebarOpen = !isSidebarOpen"
          >
            ☰
          </button>
          <label class="project-switcher">
            <span class="sr-only">{{ localeStore.t('common.selectedProject') }}</span>
            <select :value="selectedProjectId || ''" @change="changeProject">
              <option value="">{{ localeStore.t('common.selectProject') }}</option>
              <option v-for="project in projects" :key="project.id" :value="project.id">
                {{ project.name }}
              </option>
            </select>
          </label>
          <label class="language-switcher">
            <span class="sr-only">{{ localeStore.t('common.language') }}</span>
            <select
              :value="selectedLocale"
              :aria-label="localeStore.t('common.language')"
              @change="changeLocale"
            >
              <option
                v-for="option in localeStore.localeOptions"
                :key="option.value"
                :value="option.value"
              >
                {{ option.label }}
              </option>
            </select>
          </label>
          <div class="topbar-context">
            <span class="eyebrow">{{ localeStore.t('common.sourceAwarePlanner') }}</span>
            <strong>{{ selectedProject?.name || localeStore.t('common.portfolio') }}</strong>
          </div>
          <div class="account-menu">
            <span class="avatar-mark">{{
              (profile?.firstName || profile?.userName || 'U').slice(0, 2).toUpperCase()
            }}</span>
            <span>
              <strong>{{ profile?.firstName || profile?.userName }}</strong>
              <small>{{ profile?.email }}</small>
            </span>
            <button type="button" @click="logout">{{ localeStore.t('common.signOut') }}</button>
          </div>
        </header>

        <main id="main-content"><RouterView /></main>
        <nav
          v-if="mobileNavigation.length"
          class="mobile-nav"
          :aria-label="localeStore.t('common.quickNavigation')"
        >
          <RouterLink
            v-for="item in mobileNavigation"
            :key="item.to"
            :to="item.to"
            class="mobile-nav__item"
          >
            <span aria-hidden="true">{{ item.icon }}</span>
            <small>{{ item.label }}</small>
          </RouterLink>
        </nav>
      </div>
    </div>
  </template>
</template>
