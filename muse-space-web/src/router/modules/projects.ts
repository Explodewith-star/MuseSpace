import type { RouteRecordRaw } from 'vue-router'

const projectRoutes: RouteRecordRaw[] = [
  {
    path: '/projects',
    name: 'ProjectList',
    component: () => import('@/views/home/index.vue'),
  },
  {
    path: '/projects/:id',
    name: 'ProjectWorkspace',
    component: () => import('@/views/projects/index.vue'),
    redirect: (to) => `/projects/${to.params.id}/overview`,
    children: [
      {
        path: 'overview',
        name: 'ProjectOverview',
        component: () => import('@/views/projects/overview/index.vue'),
      },
      {
        path: 'chapters',
        name: 'ProjectChapters',
        component: () => import('@/views/projects/chapters/index.vue'),
      },
      {
        path: 'chapters/:chapterId',
        name: 'ChapterDetail',
        component: () => import('@/views/projects/chapters/detail/index.vue'),
      },
      {
        path: 'characters',
        name: 'ProjectCharacters',
        component: () => import('@/views/projects/characters/index.vue'),
      },
      {
        path: 'world-rules',
        name: 'ProjectWorldRules',
        component: () => import('@/views/projects/world-rules/index.vue'),
      },
      {
        path: 'style-profile',
        name: 'ProjectStyleProfile',
        component: () => import('@/views/projects/style-profile/index.vue'),
      },
      {
        path: 'draft',
        name: 'ProjectDraft',
        component: () => import('@/views/projects/draft/index.vue'),
      },
      {
        path: 'novels',
        name: 'ProjectNovels',
        component: () => import('@/views/projects/novels/index.vue'),
      },
      {
        path: 'suggestions',
        name: 'ProjectSuggestions',
        component: () => import('@/views/projects/suggestions/index.vue'),
      },
      {
        path: 'suggestions/outline/:suggestionId',
        name: 'OutlineDetail',
        component: () => import('@/views/projects/suggestions/OutlineDetail.vue'),
      },
    ],
  },
]

export default projectRoutes
