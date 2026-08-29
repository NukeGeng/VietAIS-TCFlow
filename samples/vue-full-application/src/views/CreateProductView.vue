<template>
  <form v-if="hasPermission('product.create')" @submit.prevent="submit">
    <input v-model="name" type="text" required maxlength="120" />
    <input v-model="price" type="number" required min="0" />
    <select v-model="categoryId" required>
      <option value="">Select a category</option>
    </select>
    <input v-model="supplierCode" type="text" required maxlength="32" />
    <input v-model="liveAcceptanceCode" type="text" required maxlength="40" />
    <button type="submit" :disabled="loading">Create product</button>
    <p v-if="error">{{ error }}</p>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { api } from '@/services/api'
import { hasPermission } from '@/security/permissions'

interface CreateProductRequest {
  name: string
  price: number
  categoryId: string
  supplierCode: string
  liveAcceptanceCode: string
}

interface CreateProductResponse {
  id: string
  name: string
  price: number
  categoryId: string
}

const props = defineProps<{
  projectId: string
  initialCategoryId?: string
}>()

const emit = defineEmits<{
  (event: 'saved', productId: string): void
}>()

const name = ref('')
const price = ref(0)
const categoryId = ref(props.initialCategoryId ?? '')
const supplierCode = ref('')
const liveAcceptanceCode = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

async function submit() {
  const request: CreateProductRequest = {
    name: name.value,
    price: price.value,
    categoryId: categoryId.value,
    supplierCode: supplierCode.value,
    liveAcceptanceCode: liveAcceptanceCode.value,
  }

  loading.value = true
  error.value = null
  try {
    const response = await api.post<CreateProductResponse>('/api/products', request)
    emit('saved', response.data.id)
  } catch {
    error.value = 'Unable to create product'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
form {
  display: grid;
  gap: 1rem;
}
</style>
