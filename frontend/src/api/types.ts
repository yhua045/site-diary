export interface ConstructionSite {
  id: number
  name: string
  description?: string
  address: string
  isArchived: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateSiteRequest {
  name: string
  description?: string
  address: string
}

export interface UpdateSiteRequest {
  name: string
  description?: string
  address: string
}

export interface User {
  id: number
  firstName: string
  lastName: string
  email: string
  isActive: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateUserRequest {
  firstName: string
  lastName: string
  email: string
}

export interface UpdateUserRequest {
  firstName: string
  lastName: string
  email: string
  isActive: boolean
}

export interface Diary {
  id: number
  constructionSiteId: number
  authorUserId: number
  diaryTemplateId?: number
  title: string
  content?: string
  date: string
  isPublished: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateDiaryRequest {
  constructionSiteId: number
  authorUserId: number
  diaryTemplateId?: number
  title: string
  content?: string
  date: string
}

export interface UpdateDiaryRequest {
  title: string
  content?: string
  date: string
}

export interface Attachment {
  id: number
  diaryId: number
  fileName: string
  fileUrl: string
  contentType: string
  sizeBytes: number
  uploadedByUserId: number
  uploadedAt: string
  storageProvider: string
}
