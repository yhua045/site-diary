import api from './client'
import type { Diary, DiaryTimelineEntry, CreateDiaryRequest, UpdateDiaryRequest, AttachmentDto } from './types'

export const diariesApi = {
  // Timeline (Phase 3) — full card data incl. attachments and template snapshots
  getTimeline: (siteId: number) =>
    api
      .get<DiaryTimelineEntry[]>(`/sites/${siteId}/diaries/timeline`)
      .then(r => r.data),

  // Legacy list (lean DTOs)
  getAll: (siteId: number) =>
    api.get<Diary[]>(`/sites/${siteId}/diaries`).then(r => r.data),

  getById: (siteId: number, id: number) =>
    api.get<Diary>(`/sites/${siteId}/diaries/${id}`).then(r => r.data),

  create: (siteId: number, data: CreateDiaryRequest) =>
    api
      .post<Diary>(`/sites/${siteId}/diaries`, data)
      .then(r => r.data),

  update: (siteId: number, id: number, data: UpdateDiaryRequest) =>
    api.put<Diary>(`/sites/${siteId}/diaries/${id}`, data).then(r => r.data),

  archive: (siteId: number, id: number) =>
    api.delete(`/sites/${siteId}/diaries/${id}`),

  // Attachment upload — multipart/form-data
  uploadAttachment: (diaryId: number, file: File): Promise<AttachmentDto> => {
    const form = new FormData()
    form.append('file', file)
    return api.post<AttachmentDto>(`/diaries/${diaryId}/attachments`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data)
  },
}
