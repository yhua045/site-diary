import { useState, useEffect } from 'react'
import { useParams, useLocation, Link } from 'react-router-dom'
import type { User, DiaryTimelineEntry, DiaryTemplate } from '../../api/types'
import { diariesApi } from '../../api/diaries'
import { usersApi } from '../../api/users'
import { useUserList } from '../users/useUserList'
import { DiaryTimeline } from './DiaryTimeline'
import { DiaryCreateForm } from '../../components/diary/DiaryCreateForm'

export function DiaryScreen() {
  const { siteId: siteIdParam } = useParams<{ siteId: string }>()
  const location = useLocation()
  const { siteName = 'Unknown Site', userId } = (location.state ?? {}) as {
    siteName?: string
    userId?: number
    userName?: string
  }
  const siteId = Number(siteIdParam)

  const { users, isLoading: usersLoading } = useUserList()
  const [selectedUser, setSelectedUser] = useState<User | null>(null)
  const [entries, setEntries] = useState<DiaryTimelineEntry[]>([])
  const [timelineLoading, setTimelineLoading] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [template, setTemplate] = useState<DiaryTemplate | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Pre-select the user passed from the Sites screen via router state
  useEffect(() => {
    if (userId && users.length > 0) {
      setSelectedUser(users.find(u => u.id === userId) ?? null)
    }
  }, [userId, users])

  // Re-fetch the timeline whenever the site or selected user changes
  useEffect(() => {
    setTimelineLoading(true)
    diariesApi
      .getTimeline(siteId, { userId: selectedUser?.id })
      .then(setEntries)
      .catch(() => setEntries([]))
      .finally(() => setTimelineLoading(false))
  }, [siteId, selectedUser])

  function handleUserChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const id = e.target.value
    setSelectedUser(id ? (users.find(u => u.id === Number(id)) ?? null) : null)
  }

  async function handleOpenCreate() {
    if (!selectedUser) return
    try {
      const t = await usersApi.getDiaryTemplate(selectedUser.id)
      setTemplate(t)
    } catch {
      setTemplate(null)
    }
    setShowCreate(true)
  }

  async function handleSubmit(data: { payload: Record<string, unknown>; date: string }) {
    if (!template) return
    setIsSubmitting(true)
    try {
      await diariesApi.create(
        siteId,
        {
          title: `Diary ${data.date}`,
          date: data.date,
          diaryTemplateId: template.id,
          payload: data.payload,
        },
        { userId: selectedUser?.id },
      )
      setShowCreate(false)
      const updated = await diariesApi.getTimeline(siteId, { userId: selectedUser?.id })
      setEntries(updated)
    } catch {
      // submission errors are silent for now; a toast can be wired in later
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="relative">
      {/* ── Back navigation ───────────────────────────────────────────── */}
      <div className="px-4 py-2 bg-white border-b border-gray-100">
        <Link to="/" className="text-sm text-blue-600 hover:underline">
          ← Sites
        </Link>
      </div>

      {/* ── User Switcher ─────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 px-4 py-3 bg-white border-b border-gray-200">
        <label htmlFor="user-select" className="text-sm font-medium text-gray-700 shrink-0">
          User:
        </label>
        <select
          id="user-select"
          aria-label="User"
          value={selectedUser?.id ?? ''}
          onChange={handleUserChange}
          disabled={usersLoading}
          className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
        >
          <option value="">— Select user —</option>
          {users.map(user => (
            <option key={user.id} value={user.id}>
              {user.firstName} {user.lastName}
            </option>
          ))}
        </select>
      </div>

      {/* ── Timeline ──────────────────────────────────────────────────── */}
      <DiaryTimeline
        entries={entries}
        isLoading={timelineLoading}
        onOpenCreate={handleOpenCreate}
        siteName={siteName}
      />

      {/* ── Create Modal ──────────────────────────────────────────────── */}
      {showCreate && template && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <DiaryCreateForm
            template={template}
            onSubmit={handleSubmit}
            onClose={() => setShowCreate(false)}
            isSubmitting={isSubmitting}
          />
        </div>
      )}
    </div>
  )
}

