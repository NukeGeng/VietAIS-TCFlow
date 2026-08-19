import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '../views/DashboardView.vue'
import PlaceholderView from '../views/PlaceholderView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'dashboard',
      component: DashboardView,
    },
    {
      path: '/projects',
      name: 'projects',
      component: PlaceholderView,
      props: {
        title: 'Projects',
        description: 'Project ownership and permission-aware workspaces arrive with the P2 API.',
      },
    },
    {
      path: '/repositories',
      name: 'repositories',
      component: PlaceholderView,
      props: {
        title: 'Repositories',
        description: 'Repository connections and analysis history arrive with the P3 API.',
      },
    },
    {
      path: '/tasks',
      name: 'tasks',
      component: PlaceholderView,
      props: {
        title: 'Tasks',
        description:
          'The source-aware task board arrives after task lifecycle contracts are verified.',
      },
    },
  ],
})

export default router
