import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { useUserList } from '../../features/users/useUserList'
import * as usersApiModule from '../../api/users'

vi.mock('../../api/users')

const mockUsers = [
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
  {
    id: 3,
    firstName: 'Bob',
    lastName: 'Archived',
    email: 'bob@example.com',
    isActive: false,
    isArchived: true,
    createdAt: '',
    updatedAt: '',
  },
]

describe('useUserList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('returns isLoading: true initially', () => {
    vi.mocked(usersApiModule.usersApi.getAll).mockReturnValue(new Promise(() => {}))
    const { result } = renderHook(() => useUserList())
    expect(result.current.isLoading).toBe(true)
  })

  it('returns users after fetch resolves', async () => {
    vi.mocked(usersApiModule.usersApi.getAll).mockResolvedValue(mockUsers)
    const { result } = renderHook(() => useUserList())
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.users).toHaveLength(2)
  })

  it('filters out archived users', async () => {
    vi.mocked(usersApiModule.usersApi.getAll).mockResolvedValue(mockUsers)
    const { result } = renderHook(() => useUserList())
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    const names = result.current.users.map(u => u.firstName)
    expect(names).not.toContain('Bob')
  })

  it('returns error string on fetch failure', async () => {
    vi.mocked(usersApiModule.usersApi.getAll).mockRejectedValue(new Error('Network error'))
    const { result } = renderHook(() => useUserList())
    await waitFor(() => expect(result.current.error).not.toBeNull())
    expect(result.current.error).toContain('Network error')
  })
})
