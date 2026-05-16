import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DiaryLogCard } from '../../components/diary/DiaryLogCard'
import type { DiaryTimelineEntry } from '../../api/types'

const baseEntry: DiaryTimelineEntry = {
  id: 1,
  constructionSiteId: 10,
  authorUserId: 5,
  authorName: 'John Smith',
  authorRole: 'Foreman',
  date: '2026-05-15',
  isPublished: true,
  payload: { weather: 'Sunny, 22 °C', workers: 12 },
  templateSnapshot: [
    { key: 'weather', label: 'Weather', type: 'text', displayOrder: 1 },
    { key: 'workers', label: 'Workers on site', type: 'number', displayOrder: 2 },
  ],
  attachments: [],
}

describe('DiaryLogCard', () => {
  it('renders formatted date in card header', () => {
    render(<DiaryLogCard entry={baseEntry} />)
    // Date is 2026-05-15 — should appear formatted
    expect(screen.getByText(/15 May 2026|May 15, 2026|5\/15\/2026/i)).toBeInTheDocument()
  })

  it('renders author name and role', () => {
    render(<DiaryLogCard entry={baseEntry} />)
    expect(screen.getByText(/John Smith/)).toBeInTheDocument()
    expect(screen.getByText(/Foreman/)).toBeInTheDocument()
  })

  it('renders dynamic field labels from template snapshot', () => {
    render(<DiaryLogCard entry={baseEntry} />)
    expect(screen.getByText('Weather')).toBeInTheDocument()
    expect(screen.getByText('Workers on site')).toBeInTheDocument()
  })

  it('renders dynamic field values from payload', () => {
    render(<DiaryLogCard entry={baseEntry} />)
    expect(screen.getByText('Sunny, 22 °C')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('renders dash for template field absent from payload', () => {
    const entry: DiaryTimelineEntry = {
      ...baseEntry,
      payload: { weather: 'Rainy' },
      templateSnapshot: [
        { key: 'weather', label: 'Weather', type: 'text', displayOrder: 1 },
        { key: 'safetyCheck', label: 'Safety Check', type: 'boolean', displayOrder: 2 },
      ],
    }
    render(<DiaryLogCard entry={entry} />)
    expect(screen.getByText('Safety Check')).toBeInTheDocument()
    // Missing boolean → '—'
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('renders ad-hoc payload fields not in snapshot under Additional Fields', () => {
    const entry: DiaryTimelineEntry = {
      ...baseEntry,
      payload: { weather: 'Sunny', customNote: 'Extra info' },
      templateSnapshot: [
        { key: 'weather', label: 'Weather', type: 'text', displayOrder: 1 },
      ],
    }
    render(<DiaryLogCard entry={entry} />)
    expect(screen.getByText(/Additional Fields/i)).toBeInTheDocument()
    expect(screen.getByText(/customNote/i)).toBeInTheDocument()
    expect(screen.getByText('Extra info')).toBeInTheDocument()
  })

  it('renders inline images for image attachments', () => {
    const entry: DiaryTimelineEntry = {
      ...baseEntry,
      attachments: [
        { id: 1, diaryId: 1, fileName: 'photo.jpg', fileUrl: '/uploads/photo.jpg', contentType: 'image/jpeg' },
        { id: 2, diaryId: 1, fileName: 'snap.png', fileUrl: '/uploads/snap.png', contentType: 'image/png' },
      ],
    }
    render(<DiaryLogCard entry={entry} />)
    const images = screen.getAllByRole('img')
    expect(images.length).toBeGreaterThanOrEqual(2)
  })

  it('renders doc chip for non-image attachments', () => {
    const entry: DiaryTimelineEntry = {
      ...baseEntry,
      attachments: [
        { id: 3, diaryId: 1, fileName: 'report.pdf', fileUrl: '/uploads/report.pdf', contentType: 'application/pdf' },
      ],
    }
    render(<DiaryLogCard entry={entry} />)
    expect(screen.getByText('report.pdf')).toBeInTheDocument()
  })

  it('shows +N overlay when more than 3 images', () => {
    const entry: DiaryTimelineEntry = {
      ...baseEntry,
      attachments: [1, 2, 3, 4, 5].map(i => ({
        id: i, diaryId: 1, fileName: `photo${i}.jpg`,
        fileUrl: `/uploads/photo${i}.jpg`, contentType: 'image/jpeg',
      })),
    }
    render(<DiaryLogCard entry={entry} />)
    expect(screen.getByText(/\+2/)).toBeInTheDocument()
  })

  it('omits attachments section when no attachments', () => {
    render(<DiaryLogCard entry={baseEntry} />)
    // No images or doc chips should appear
    expect(screen.queryAllByRole('img')).toHaveLength(0)
  })
})
