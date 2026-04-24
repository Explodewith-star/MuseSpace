import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import projectRoutes from './modules/projects'
import { useAuthStore } from '@/store/modules/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/projects',
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true },
  },
  ...projectRoutes,
  {
    path: '/admin/users',
    name: 'AdminUsers',
    component: () => import('@/views/admin/UsersView.vue'),
    meta: { requiresAdmin: true },
  },
  {
    path: '/admin/projects',
    name: 'AdminProjects',
    component: () => import('@/views/admin/ProjectsView.vue'),
    meta: { requiresAdmin: true },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/NotFound.vue'),
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// 全局路由守卫：管理员专属路由需要 Admin JWT
router.beforeEach((to) => {
  if (to.meta.requiresAdmin) {
    const authStore = useAuthStore()
    if (!authStore.isTokenValid() || !authStore.isAdmin) {
      return { path: '/login' }
    }
  }
})

export default router
