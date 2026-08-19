import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type DeliveryState = 'ready' | 'planned'

export interface CapabilityStatus {
  name: string
  description: string
  state: DeliveryState
}

export const useWorkspaceStore = defineStore('workspace', () => {
  const capabilities = ref<CapabilityStatus[]>([
    {
      name: 'Product shell',
      description: 'Vue 3, TypeScript, Router, Pinia, and Vite',
      state: 'ready',
    },
    {
      name: 'Repository analysis',
      description: 'Deterministic source analyzers and evidence graph',
      state: 'planned',
    },
    {
      name: 'Source-aware tasks',
      description: 'Impact-driven planning with reconciliation',
      state: 'planned',
    },
  ])

  const readyCount = computed(
    () => capabilities.value.filter((capability) => capability.state === 'ready').length,
  )

  return { capabilities, readyCount }
})
