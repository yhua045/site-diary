import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { SitesScreen } from '../../features/sites/SitesScreen'
import * as useUserListModule from '../../features/users/useUserList'
import * as useSitesByUserModule from '../../features/sites/useSitesByUser'
import type { User, ConstructionSite } from '../../api/types'

const mockNavigate = vi.fn()

vi.mock('react-router-dom', async importOriginal => {
  const actual = await importOriginal<typeof import('react-router-dom')>()
  return { ...actual, useNavigate: () => mockNavigate }
})

vi.mock('../../features/users/useUserList')
vi.mock('../../features/sites/useSitesByUser')

const mockUsers: User[] = [
  {
    id: 1,
    firstName: 'John',
    lastName: 'Smith',
    email: 'john@example.com',
    isArchived: false,
  },
  {
    id: 2,
    firstName: 'Jane',
    lastName: 'Doe',
    email: 'jane@example.com',
    isArchived: false,
  },
]

const mockSites: ConstructionSite[] = [
  {
    id: 10,
    name: 'Site Alpha',
    address: '1 Main St',
    isArchived: false,
  },
  {
    id: 20,
    name: 'Site Beta',
    address: '2 Main St',
    isArchived: false,
  },
]

function renderScreen() {
  return render(
    <MemoryRouter>
      <SitesScreen />
    </MemoryRouter>,
  )
}

describe('SitesScreen', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useUserListModule.useUserList).mockReturnValue({
      users: mockUsers,
      isLoading: false,
      error: null,
    })
    vi.mocked(useSitesByUserModule.useSitesByUser).mockReturnValue({
      sites: [],
      isLoading: false,
      error: null,
    })
  })

  it('renders User dropdown with options from API', () => {
    renderScreen()
    const userSelect = screen.getByRole('combobox', { name: /user/i })
    expect(userSelect.querySelectorAll('option')).toHaveLength(3) // placeholder + 2 users
    expect(screen.getByText('John Smith')).toBeInTheDocument()
    expect(screen.getByText('Jane Doe')).toBeInTheDocument()
  })

  it('site dropdown is disabled on mount (no user selected)', () => {
    renderScreen()
    expect(screen.getByRole('combobox', { name: /site/i })).toBeDisabled()
  })

  it('selecting a user passes userId to useSitesByUser and enables site dropdown', async () => {
    renderScreen()
    const userSelect = screen.getByRole('combobox', { name: /user/i })
    await userEvent.selectOptions(userSelect, '1')
    expect(useSitesByUserModule.useSitesByUser).toHaveBeenCalledWith(1)
    expect(screen.getByRole('combobox', { name: /site/i })).not.toBeDisabled()
  })

  it('site dropdown is populated with sites after user selected', async () => {
    vi.mocked(useSitesByUserModule.useSitesByUser).mockReturnValue({
      sites: mockSites,
      isLoading: false,
      error: null,
    })
    renderScreen()
    const userSelect = screen.getByRole('combobox', { name: /user/i })
    await userEvent.selectOptions(userSelect, '1')
    expect(screen.getByText('Site Alpha')).toBeInTheDocument()
    expect(screen.getByText('Site Beta')).toBeInTheDocument()
  })

  it('View Diary button is disabled when no site selected', () => {
    renderScreen()
    expect(screen.getByRole('button', { name: /view diary/i })).toBeDisabled()
  })

  it('View Diary button is enabled after selecting a site', async () => {
    vi.mocked(useSitesByUserModule.useSitesByUser).mockReturnValue({
      sites: mockSites,
      isLoading: false,
      error: null,
    })
    renderScreen()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /user/i }), '1')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /site/i }), '10')
    expect(screen.getByRole('button', { name: /view diary/i })).not.toBeDisabled()
  })

  it('clicking View Diary navigates to /sites/:id/diary with state', async () => {
    vi.mocked(useSitesByUserModule.useSitesByUser).mockReturnValue({
      sites: mockSites,
      isLoading: false,
      error: null,
    })
    renderScreen()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /user/i }), '1')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /site/i }), '10')
    await userEvent.click(screen.getByRole('button', { name: /view diary/i }))
    expect(mockNavigate).toHaveBeenCalledWith(
      '/sites/10/diary',
      expect.objectContaining({
        state: expect.objectContaining({
          siteName: 'Site Alpha',
          userId: 1,
          userName: 'John Smith',
        }),
      }),
    )
  })

  it('changing user resets site selection and disables View Diary', async () => {
    vi.mocked(useSitesByUserModule.useSitesByUser).mockReturnValue({
      sites: mockSites,
      isLoading: false,
      error: null,
    })
    renderScreen()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /user/i }), '1')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /site/i }), '10')
    expect(screen.getByRole('button', { name: /view diary/i })).not.toBeDisabled()

    // Change user — should reset site and disable button
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /user/i }), '2')
    expect(screen.getByRole('button', { name: /view diary/i })).toBeDisabled()
  })
})
