<script setup lang="ts">
defineProps<{
  title: string
  description: string
}>()
</script>

<template>
  <section class="placeholder-panel">
    <span class="eyebrow">Planned capability</span>
    <h1>{{ title }}</h1>
    <p>{{ description }}</p>
    <RouterLink to="/">Return to delivery map</RouterLink>
  </section>
</template>
