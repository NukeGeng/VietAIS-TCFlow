import { createRouter, createWebHistory } from 'vue-router'
import { authSession } from '../services/auth-session'
import AnalysisView from '../views/AnalysisView.vue'
import DashboardView from '../views/DashboardView.vue'
import FeaturesView from '../views/FeaturesView.vue'
import GitHubCallbackView from '../views/GitHubCallbackView.vue'
import ImpactGraphView from '../views/ImpactGraphView.vue'
import LoginView from '../views/LoginView.vue'
import ProjectAdminView from '../views/ProjectAdminView.vue'
import ProjectsView from '../views/ProjectsView.vue'
import RepositoriesView from '../views/RepositoriesView.vue'
import SystemAdminView from '../views/SystemAdminView.vue'
import TaskBoardView from '../views/TaskBoardView.vue'
import TaskDetailView from '../views/TaskDetailView.vue'

export function createAppRouter() {
  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
      { path: '/login', name: 'login', component: LoginView, meta: { public: true } },
      { path: '/', name: 'dashboard', component: DashboardView },
      { path: '/projects', name: 'projects', component: ProjectsView },
      { path: '/github/callback', name: 'github-callback', component: GitHubCallbackView },
      {
        path: '/projects/:projectId/repositories',
        name: 'repositories',
        component: RepositoriesView,
        meta: { boundedContext: 'repository-intelligence' },
      },
      {
        path: '/projects/:projectId/analysis',
        name: 'analysis',
        component: AnalysisView,
        meta: { boundedContext: 'repository-intelligence' },
      },
      {
        path: '/projects/:projectId/impacts',
        name: 'impacts',
        component: ImpactGraphView,
        meta: { boundedContext: 'architecture' },
      },
      {
        path: '/projects/:projectId/features',
        name: 'features',
        component: FeaturesView,
        meta: { boundedContext: 'planning' },
      },
      {
        path: '/projects/:projectId/tasks',
        name: 'tasks',
        component: TaskBoardView,
        meta: { boundedContext: 'task-flow' },
      },
      {
        path: '/projects/:projectId/tasks/:taskId',
        name: 'task-detail',
        component: TaskDetailView,
        meta: { boundedContext: 'task-flow' },
      },
      {
        path: '/projects/:projectId/admin',
        name: 'project-admin',
        component: ProjectAdminView,
        meta: { boundedContext: 'access-control' },
      },
      { path: '/system', name: 'system-admin', component: SystemAdminView },
      { path: '/:pathMatch(.*)*', redirect: '/' },
    ],
  })

  router.beforeEach((to) => {
    if (to.meta.public && authSession.exists()) return { name: 'dashboard' }
    if (!to.meta.public && !authSession.exists()) {
      return { name: 'login', query: { redirect: to.fullPath } }
    }
    return true
  })

  return router
}

const router = createAppRouter()
export default router
