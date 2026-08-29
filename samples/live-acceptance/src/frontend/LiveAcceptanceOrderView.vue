<template>
  <form @submit.prevent="submit">
    <input v-model="name" required />
    <button type="submit">Create order</button>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { api } from '@/services/api'

interface CreateLiveAcceptanceOrderRequest {
  name: string
}

interface CreateLiveAcceptanceOrderResponse {
  id: string
  name: string
}

const name = ref('')

async function submit() {
  const request: CreateLiveAcceptanceOrderRequest = {
    name: name.value,
  }

  await api.post<CreateLiveAcceptanceOrderResponse>(
    '/api/v1/live-acceptance/orders',
    request,
  )
}
</script>
