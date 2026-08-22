<script setup lang="ts">
import type { ResourceState } from '../types/contracts'

defineProps<{
  state: ResourceState
  emptyTitle?: string
  emptyMessage?: string
}>()

defineEmits<{ retry: [] }>()
</script>

<template>
  <div v-if="state.status === 'loading'" class="state-panel" role="status" aria-live="polite">
    <span class="loader" aria-hidden="true"></span>
    <div>
      <strong>Loading verified data</strong>
      <p>Reading the current backend state…</p>
    </div>
  </div>

  <div
    v-else-if="state.status === 'forbidden'"
    class="state-panel state-panel--warning"
    role="alert"
  >
    <span class="state-icon" aria-hidden="true">403</span>
    <div>
      <strong>Access is not granted</strong>
      <p>{{ state.message || 'Your project permission does not cover this resource.' }}</p>
    </div>
  </div>

  <div v-else-if="state.status === 'error'" class="state-panel state-panel--danger" role="alert">
    <span class="state-icon" aria-hidden="true">!</span>
    <div>
      <strong>Unable to load this view</strong>
      <p>{{ state.message }}</p>
      <button class="text-button" type="button" @click="$emit('retry')">Try again</button>
    </div>
  </div>

  <div v-else-if="state.status === 'empty'" class="state-panel">
    <span class="state-icon" aria-hidden="true">0</span>
    <div>
      <strong>{{ emptyTitle || 'Nothing here yet' }}</strong>
      <p>{{ emptyMessage || 'Create the first item when you are ready.' }}</p>
    </div>
  </div>

  <slot v-else />
</template>
