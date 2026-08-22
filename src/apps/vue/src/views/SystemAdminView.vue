<script setup lang="ts">
import { ref } from 'vue'
import ResourceState from '../components/ResourceState.vue'
import { ApiError } from '../services/http'
import { tcflowApi } from '../services/tcflow-api'
import { useSessionStore } from '../stores/session'
import type { ResourceState as State, UserProfile } from '../types/contracts'

const session = useSessionStore()
const users = ref<UserProfile[]>([])
const state = ref<State>({ status: 'idle' })

async function load(): Promise<void> {
  if (!session.hasSystemPermission('Permissions.Users.View')) {
    state.value = { status: 'forbidden', message: 'Requires Permissions.Users.View.' }
    return
  }
  state.value = { status: 'loading' }
  try {
    users.value = await tcflowApi.users()
    state.value = { status: users.value.length ? 'ready' : 'empty' }
  } catch (error) {
    state.value = {
      status: error instanceof ApiError && error.status === 403 ? 'forbidden' : 'error',
      message: error instanceof Error ? error.message : 'Unable to load users.',
    }
  }
}

load()
</script>

<template>
  <section class="page-heading">
    <div>
      <span class="eyebrow">Platform boundary</span>
      <h1>System administration</h1>
      <p>Platform identity administration stays separate from Project Owner authority.</p>
    </div>
  </section>
  <ResourceState
    :state="state"
    empty-title="No users found"
    empty-message="The platform identity store returned no users."
    @retry="load"
  >
    <section class="panel">
      <div class="section-heading section-heading--compact">
        <div>
          <span class="eyebrow">Identity</span>
          <h2>Platform users</h2>
        </div>
        <span class="count-badge">{{ users.length }}</span>
      </div>
      <div class="user-table">
        <article v-for="user in users" :key="user.id">
          <span class="avatar-mark">{{
            (user.firstName || user.userName || user.email || 'U').slice(0, 2).toUpperCase()
          }}</span
          ><span
            ><strong>{{
              [user.firstName, user.lastName].filter(Boolean).join(' ') || user.userName
            }}</strong
            ><small>{{ user.email }}</small></span
          ><span
            :class="['state-pill', user.isActive ? 'state-pill--ready' : 'state-pill--planned']"
            >{{ user.isActive ? 'active' : 'inactive' }}</span
          >
        </article>
      </div>
    </section>
  </ResourceState>
</template>
