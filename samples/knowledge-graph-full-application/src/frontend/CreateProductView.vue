<template>
  <form v-if="hasPermission('product.create')" @submit.prevent="submit">
    <input v-model="name" required />
    <input v-model="price" type="number" required />
    <input v-model="categoryId" required />
    <input v-model="supplierCode" required />
    <button type="submit">Create</button>
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
}

interface CreateProductResponse {
  id: string
  name: string
  price: number
  categoryId: string
}

const name = ref('')
const price = ref(0)
const categoryId = ref('')
const supplierCode = ref('')
const error = ref<string | null>(null)

async function submit() {
  const request: CreateProductRequest = {
    name: name.value,
    price: price.value,
    categoryId: categoryId.value,
    supplierCode: supplierCode.value,
  }

  try {
    await api.post<CreateProductResponse>('/api/v1/catalog/products', request)
  } catch {
    error.value = 'Unable to create product'
  }
}
</script>
