import { createRouter, createWebHistory } from 'vue-router'
import CreateProductView from '../views/CreateProductView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/products/create',
      name: 'products-create',
      component: CreateProductView,
    },
  ],
})
