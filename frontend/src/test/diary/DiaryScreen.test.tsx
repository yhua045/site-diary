import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { DiaryScreen } from '../../features/diaries/DiaryScreen'
import * as useUserListModule from '../../features/users/useUserList'
import * as diariesApiModule from '../../api/diaries'
import type { User } from '../../api/types'

vi.mock('../../features/users/useUserList')
vi.mock('../../api/diaries')

const mockUsers: User[] = [
  {
    id: 1,
    firstName: 'John',
    lastName: 'Smith',
    email: 'john@example.com',
    isActive: true,
    isArchived: false,
    createdAt: '',
    updatedAt: '',
  },
  {
    id: 2,
    firstName: 'Jane',
    lastName: 'Doe',
    email: 'jane@example.com',
    isActive: true,
    isArchived: false,
    createdAt: '',
    updatedAt: '',
  },
]

interface RenderOptions {
  siteId?: number
  state?: Record<string, unknown>
}

function renderDiaryScreen({
  siteId = 10,
  state = { siteName: 'Test Site' },
}: RenderOptions = {}) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: `/sites/${siteId}/diary`, state }]}>
      <Routes>
        <Route path="/sites/:siteId/diary" element={<DiaryScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('DiaryScreen — user dropdown', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useUserListModule.useUserList).mockReturnValue({
      users: mockUsers,
      isLoading: false,
      error: null,
    })
    vi.mocked(diariesApiModule.diariesApi.getTimeline).mockResolvedValue([])
  })

  it('renders a user <select> on screen', () => {
    renderDiaryScreen()
    expect(screen.getByRole('combobox', { name: /user/i })).toBeInTheDocument()
  })

  it('dropdown lists all non-archived users plus placeholder', () => {
    renderDiaryScreen()
    const select = screen.getByRole('combobox', { name: /user/i })
    expect(select.querySelectorAll('option')).toHaveLength(3) // 2 users + 1 placeholder
  })

  it("shows user's full name in each option", () => {
    renderDiaryScreen()
    expect(screen.getByText('John Smith')).toBeInTheDocument()
    expect(screen.getByText('Jane Doe')).toBeInTheDocument()
  })

  it('dropdown is disabled while users are loading', () => {
    vi.mocked(useUserListModule.useUserList).mockReturnValue({
      users: [],
      isLoading: true,
      error: null,
    })
    renderDiaryScreen()
    expect(screen.getByRole('combobox', { name: /user/i })).toBeDisabled()
  })

  it('selecting a user updates the selected option', async () => {
    renderDiaryScreen()
    const select = screen.getByRole('combobox', { name: /user/i }) as HTMLSelectElement
    await userEvent.selectOptions(select, '2')
    expect(select.value).toBe('2')
  })

  it('fetching timeline with a user selected sends X-User-Id', async () => {
    renderDiaryScreen()
    const select = screen.getByRole('combobox', { name: /user/i }) as HTMLSelectElement
    await userEvent.selectOptions(select, '2')
    await waitFor(() => {
      expect(diariesApiModule.diariesApi.getTimeline).toHaveBeenCalledWith(
        10,
        expect.objectContaining({ userId: 2 }),
      )
    })
  })

  it('fetching timeline with no user selected omits X-User-Id', async () => {
    renderDiaryScreen()
    await waitFor(() => {
      expect(diariesApiModule.diariesApi.getTimeline).toHaveBeenCalledWith(
        10,
        expect.objectContaining({ userId: undefined }),
      )
    })
  })
})

describe('DiaryScreen — router integration', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useUserListModule.useUserList).mockReturnValue({
      users: mockUsers,
      isLoading: false,
      error: null,
    })
    vi.mocked(diariesApiModule.diariesApi.getTimeline).mockResolvedValue([])
  })

  it('reads siteId from URL params and uses it for timeline fetch', async () => {
    renderDiaryScreen({ siteId: 42 })
    await waitFor(() => {
      expect(diariesApiModule.diariesApi.getTimeline).toHaveBeenCalledWith(
        42,
        expect.anything(),
      )
    })
  })

  it('reads siteName from location state', async () => {
    renderDiaryScreen({ state: { siteName: 'My Custom Site' } })
    await waitFor(() => {
      expect(screen.getByText('My Custom Site')).toBeInTheDocument()
    })
  })

  it('shows back link to /', () => {
    renderDiaryScreen()
    const backLink = screen.getByRole('link', { name: /← sites/i })
    expect(backLink).toBeInTheDocument()
    expect(backLink).toHaveAttribute('href', '/')
  })

  it('pre-selects user from location state userId', async () => {
    renderDiaryScreen({ state: { siteName: 'Test Site', userId: 2, userName: 'Jane Doe' } })
    const select = screen.getByRole('combobox', { name: /user/i }) as HTMLSelectElement
    await waitFor(() => {
      expect(select.value).toBe('2')
    })
  })

  it('falls back gracefully when router state is empty', () => {
    renderDiaryScreen({ state: {} })
    expect(screen.getByRole('combobox', { name: /user/i })).toBeInTheDocument()
  })
})
