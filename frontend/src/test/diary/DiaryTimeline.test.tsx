import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DiaryTimeline } from '../../features/diaries/DiaryTimeline'
import type { DiaryTimelineEntry } from '../../api/types'

const entry1: DiaryTimelineEntry = {
  id: 1,
  constructionSiteId: 10,
  authorUserId: 5,
  authorName: 'John Smith',
  authorRole: 'Foreman',
  date: '2026-05-15',
  payload: { weather: 'Sunny' },
  templateSnapshot: [{ key: 'weather', label: 'Weather', type: 'text', displayOrder: 1 }],
  attachments: [],
}

const entry2: DiaryTimelineEntry = {
  id: 2,
  constructionSiteId: 10,
  authorUserId: 6,
  authorName: 'Jane Doe',
  authorRole: 'Site Manager',
  date: '2026-04-20',
  payload: {},
  templateSnapshot: [],
  attachments: [],
}

describe('DiaryTimeline', () => {
  const onOpenCreate = vi.fn()

  beforeEach(() => {
    onOpenCreate.mockReset()
  })

  it('renders empty state when no entries', () => {
    render(<DiaryTimeline entries={[]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Test Site" />)
    expect(screen.getByText(/No diary entries yet/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Add First Entry/i })).toBeInTheDocument()
  })

  it('renders all diary entries', () => {
    render(<DiaryTimeline entries={[entry1, entry2]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Site A" />)
    expect(screen.getByText(/John Smith/)).toBeInTheDocument()
    expect(screen.getByText(/Jane Doe/)).toBeInTheDocument()
  })

  it('renders sticky page header with site name', () => {
    render(<DiaryTimeline entries={[entry1]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Harbour Bridge" />)
    expect(screen.getByText('Harbour Bridge')).toBeInTheDocument()
    expect(screen.getByText(/Site Diary/i)).toBeInTheDocument()
  })

  it('renders month separators when entries span different months', () => {
    render(<DiaryTimeline entries={[entry1, entry2]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Site" />)
    // entry1 is May 2026, entry2 is April 2026 — two month separators
    expect(screen.getByText(/May 2026/i)).toBeInTheDocument()
    expect(screen.getByText(/April 2026/i)).toBeInTheDocument()
  })

  it('shows loading skeleton when isLoading is true', () => {
    render(<DiaryTimeline entries={[]} isLoading={true} onOpenCreate={onOpenCreate} siteName="Site" />)
    expect(screen.getByTestId('timeline-loading')).toBeInTheDocument()
  })

  it('FAB button calls onOpenCreate', async () => {
    const { getByLabelText } = render(
      <DiaryTimeline entries={[entry1]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Site" />
    )
    const fab = getByLabelText(/Add diary entry/i)
    fab.click()
    expect(onOpenCreate).toHaveBeenCalledOnce()
  })

  it('header [+] button calls onOpenCreate', async () => {
    render(<DiaryTimeline entries={[entry1]} isLoading={false} onOpenCreate={onOpenCreate} siteName="Site" />)
    const addBtn = screen.getByRole('button', { name: /Add diary entry/i })
    addBtn.click()
    expect(onOpenCreate).toHaveBeenCalled()
  })
})
