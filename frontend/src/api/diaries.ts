import api from './client'
import type { Diary, CreateDiaryRequest, UpdateDiaryRequest } from './types'

export const diariesApi = {
  getAll: () => api.get<Diary[]>('/diaries').then(r => r.data),
  getById: (id: number) => api.get<Diary>(`/diaries/${id}`).then(r => r.data),
  create: (data: CreateDiaryRequest) => api.post<Diary>('/diaries', data).then(r => r.data),
  update: (id: number, data: UpdateDiaryRequest) => api.put<Diary>(`/diaries/${id}`, data).then(r => r.data),
  archive: (id: number) => api.delete(`/diaries/${id}`),
  publish: (id: number) => api.post(`/diaries/${id}/publish`),
}
