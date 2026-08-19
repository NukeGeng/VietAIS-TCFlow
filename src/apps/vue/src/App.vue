<script setup lang="ts">
const navigation = [
  { label: 'Dashboard', to: '/' },
  { label: 'Projects', to: '/projects' },
  { label: 'Repositories', to: '/repositories' },
  { label: 'Tasks', to: '/tasks' },
]
</script>

<template>
  <a class="skip-link" href="#main-content">Skip to content</a>

  <div class="app-shell">
    <aside class="sidebar">
      <RouterLink class="brand" to="/" aria-label="VietAIS TCFlow home">
        <span class="brand-mark" aria-hidden="true">TC</span>
        <span>
          <strong>VietAIS</strong>
          <small>TCFlow</small>
        </span>
      </RouterLink>

      <nav aria-label="Primary navigation">
        <RouterLink v-for="item in navigation" :key="item.to" :to="item.to">
          {{ item.label }}
        </RouterLink>
      </nav>

      <div class="sidebar-note">
        <span class="status-dot" aria-hidden="true"></span>
        <span>
          <strong>Local-first</strong>
          <small>Aspire workspace</small>
        </span>
      </div>
    </aside>

    <div class="workspace">
      <header class="topbar">
        <div>
          <span class="eyebrow">Engineering intelligence</span>
          <strong>Source-aware planner</strong>
        </div>
        <span class="environment-badge">Development</span>
      </header>

      <main id="main-content">
        <RouterView />
      </main>
    </div>
  </div>
</template>
