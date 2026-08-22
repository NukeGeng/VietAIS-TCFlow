import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/services/api'

interface Product {
  id: string
  name: string
}

interface ProductPage {
  items: Product[]
  total: number
  page: number
  pageSize: number
}

export const useProductsStore = defineStore('products', () => {
  const page = ref(1)
  const pageSize = ref(20)
  const search = ref('')

  async function loadProducts() {
    const result = await api.get<ProductPage>(
      `/api/products?page=${page.value}&pageSize=${pageSize.value}`,
    )
    return result.data.items
  }

  return { page, pageSize, search, loadProducts }
})
