<script setup lang="ts">
defineProps<{
  allowed: boolean
  permission: string
}>()
</script>

<template>
  <slot v-if="allowed" />
  <div v-else class="permission-notice">
    <strong>Action unavailable</strong>
    <span
      >Requires <code>{{ permission }}</code
      >. Backend authorization remains authoritative.</span
    >
  </div>
</template>
