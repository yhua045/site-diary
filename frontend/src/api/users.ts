import api from './client'
import type { User, ConstructionSite, CreateUserRequest, UpdateUserRequest, DiaryTemplate } from './types'

export const usersApi = {
  getAll: () => api.get<User[]>('/users').then(r => r.data),
  getById: (id: number) => api.get<User>(`/users/${id}`).then(r => r.data),
  create: (data: CreateUserRequest) => api.post<User>('/users', data).then(r => r.data),
  update: (id: number, data: UpdateUserRequest) => api.put<User>(`/users/${id}`, data).then(r => r.data),
  getDiaryTemplate: (userId: number) =>
    api.get<DiaryTemplate>(`/users/${userId}/diary-template`).then(r => r.data),
  getSitesByUser: (userId: number) =>
    api.get<ConstructionSite[]>(`/users/${userId}/sites`).then(r => r.data),
}
