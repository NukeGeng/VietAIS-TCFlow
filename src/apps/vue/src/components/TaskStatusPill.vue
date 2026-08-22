<script setup lang="ts">
import { TaskLifecycleStatus, taskStatusLabel } from '../types/contracts'

defineProps<{ status: TaskLifecycleStatus }>()
</script>

<template>
  <span :class="['task-status', `task-status--${TaskLifecycleStatus[status].toLowerCase()}`]">
    {{ taskStatusLabel[status] }}
  </span>
</template>
