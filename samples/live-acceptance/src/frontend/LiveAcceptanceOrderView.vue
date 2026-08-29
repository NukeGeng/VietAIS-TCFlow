<template>
  <form @submit.prevent="submit">
    <input v-model="name" required />
    <input v-model="traceCode" required maxlength="48" />
    <button type="submit">Create order</button>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { api } from '@/services/api'

interface CreateLiveAcceptanceOrderRequest {
  name: string
  traceCode: string
}

interface CreateLiveAcceptanceOrderResponse {
  id: string
  name: string
}

const name = ref('')
const traceCode = ref('')

async function submit() {
  const request: CreateLiveAcceptanceOrderRequest = {
    name: name.value,
    traceCode: traceCode.value,
  }

  await api.post<CreateLiveAcceptanceOrderResponse>(
    '/api/v1/live-acceptance/orders',
    request,
  )
}
</script>
