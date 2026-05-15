import api from './client'
import type { ConstructionSite, CreateSiteRequest, UpdateSiteRequest } from './types'

export const sitesApi = {
  getAll: () => api.get<ConstructionSite[]>('/sites').then(r => r.data),
  getById: (id: number) => api.get<ConstructionSite>(`/sites/${id}`).then(r => r.data),
  create: (data: CreateSiteRequest) => api.post<ConstructionSite>('/sites', data).then(r => r.data),
  update: (id: number, data: UpdateSiteRequest) => api.put<ConstructionSite>(`/sites/${id}`, data).then(r => r.data),
  archive: (id: number) => api.delete(`/sites/${id}`),
}
