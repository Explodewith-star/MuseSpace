import request from './http'

export interface LoginRequest {
  phoneNumber: string
}

export interface AdminLoginRequest {
  phoneNumber: string
  password: string
}

export interface LoginResponse {
  token: string
  role: string
  userId: string
  phoneNumber: string
  expiresAt: string
}

export interface UserResponse {
  id: string
  phoneNumber: string
  role: string
  createdAt: string
  lastLoginAt?: string
}

export interface CreateUserRequest {
  phoneNumber: string
}

// 普通用户登录（手机号白名单）
export function login(data: LoginRequest) {
  return request.post<LoginResponse>('/auth/login', data, { silent: true })
}

// 管理员登录（手机号 + 密码）
export function adminLogin(data: AdminLoginRequest) {
  return request.post<LoginResponse>('/auth/admin-login', data, { silent: true })
}

// 验证当前 token
export function getMe() {
  return request.get<UserResponse>('/auth/me')
}

// ── 管理员：用户管理 ─────────────────────────────────────────────────────────
export function getUsers() {
  return request.get<UserResponse[]>('/admin/users')
}

export function createUser(data: CreateUserRequest) {
  return request.post<UserResponse>('/admin/users', data)
}

export function deleteUser(userId: string) {
  return request.delete<void>(`/admin/users/${userId}`)
}

// ── 管理员：项目管理 ─────────────────────────────────────────────────────────
export interface AdminProject {
  id: string
  name: string
  description?: string
  genre?: string
  userId?: string
  createdAt: string
}

export function getAdminProjects() {
  return request.get<AdminProject[]>('/admin/projects')
}

export function assignProject(projectId: string, userId: string | null) {
  return request.put<void>(`/admin/projects/${projectId}/assign`, { userId })
}

export function deleteAdminProject(projectId: string) {
  return request.delete<void>(`/admin/projects/${projectId}`)
}
