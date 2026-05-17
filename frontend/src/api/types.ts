export interface ConstructionSite {
  id: number
  name: string
  description?: string
  address: string
  isArchived: boolean
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
  isArchived: boolean
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
}

// ── Diary Template ────────────────────────────────────────────────────────────

export interface SelectOption {
  value: string
  label: string
}

export interface FieldDef {
  id: string
  label: string
  type: string
  required: boolean
  placeholder?: string
  options?: string[]
  min?: number
  max?: number
}

export interface SectionDef {
  id: string
  label: string
  fields: FieldDef[]
}

export interface DiaryTemplate {
  id: number
  name: string
  sections: SectionDef[]
}

// ── Diary Timeline ────────────────────────────────────────────────────────────

/**
 * A single field descriptor embedded in the TemplateSnapshot of a saved diary entry.
 * Used by the card renderer to format field values.
 */
export interface FieldDescriptor {
  key: string
  label: string
  type: 'text' | 'number' | 'boolean' | 'date' | 'select' | string
  displayOrder: number
  options?: SelectOption[]
}

export interface AttachmentDto {
  id: number
  diaryId: number
  fileName: string
  fileUrl: string
  contentType: string
}

/**
 * Full timeline entry returned by GET /api/sites/{siteId}/diaries/timeline.
 */
export interface DiaryTimelineEntry {
  id: number
  constructionSiteId: number
  authorUserId: number
  authorName: string
  authorRole?: string
  date: string
  payload: Record<string, unknown>
  templateSnapshot: FieldDescriptor[]
  attachments: AttachmentDto[]
}

// ── Diary (legacy / mutation DTOs) ────────────────────────────────────────────

export interface Diary {
  id: number
  constructionSiteId: number
  authorUserId: number
  diaryTemplateId?: number
  title: string
  content?: string
  date: string
}

export interface FieldOverrides {
  removed: string[]
  added: FieldDef[]
}

export interface CreateDiaryRequest {
  title: string
  content?: string
  date: string
  diaryTemplateId?: number
  fieldOverrides?: FieldOverrides
  payload?: Record<string, unknown>
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
