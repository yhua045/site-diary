import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import { useSitesByUser } from '../../features/sites/useSitesByUser'
import * as usersApiModule from '../../api/users'
import type { ConstructionSite } from '../../api/types'

vi.mock('../../api/users')

const mockSites: ConstructionSite[] = [
  {
    id: 1,
    name: 'Site Alpha',
    address: '1 Main St',
    isArchived: false,
  },
  {
    id: 2,
    name: 'Site Beta',
    address: '2 Second St',
    isArchived: false,
  },
  {
    id: 3,
    name: 'Site Archived',
    address: '3 Third St',
    isArchived: true,
  },
]

describe('useSitesByUser', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('returns empty sites + not loading when userId is null', () => {
    const { result } = renderHook(() => useSitesByUser(null))
    expect(result.current.sites).toEqual([])
    expect(result.current.isLoading).toBe(false)
    expect(result.current.error).toBeNull()
  })

  it('sets isLoading=true on valid userId', () => {
    vi.mocked(usersApiModule.usersApi.getSitesByUser).mockReturnValue(new Promise(() => {}))
    const { result } = renderHook(() => useSitesByUser(1))
    expect(result.current.isLoading).toBe(true)
  })

  it('returns filtered (non-archived) sites on success', async () => {
    vi.mocked(usersApiModule.usersApi.getSitesByUser).mockResolvedValue(mockSites)
    const { result } = renderHook(() => useSitesByUser(1))
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.sites).toHaveLength(2)
    expect(result.current.sites.map(s => s.name)).not.toContain('Site Archived')
  })

  it('sets error string on fetch failure', async () => {
    vi.mocked(usersApiModule.usersApi.getSitesByUser).mockRejectedValue(new Error('Network error'))
    const { result } = renderHook(() => useSitesByUser(1))
    await waitFor(() => expect(result.current.error).not.toBeNull())
    expect(result.current.error).toContain('Network error')
    expect(result.current.sites).toEqual([])
  })

  it('re-fetches when userId changes', async () => {
    vi.mocked(usersApiModule.usersApi.getSitesByUser).mockResolvedValue(mockSites)
    const { result, rerender } = renderHook(({ userId }) => useSitesByUser(userId), {
      initialProps: { userId: 1 as number | null },
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(usersApiModule.usersApi.getSitesByUser).toHaveBeenCalledWith(1)

    rerender({ userId: 2 })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(usersApiModule.usersApi.getSitesByUser).toHaveBeenCalledWith(2)
    expect(usersApiModule.usersApi.getSitesByUser).toHaveBeenCalledTimes(2)
  })

  it('ignores stale response when userId changes during fetch', async () => {
    let resolveFirst!: (sites: ConstructionSite[]) => void
    const firstPromise = new Promise<ConstructionSite[]>(res => (resolveFirst = res))

    vi.mocked(usersApiModule.usersApi.getSitesByUser)
      .mockReturnValueOnce(firstPromise)
      .mockResolvedValueOnce([])

    const { result, rerender } = renderHook(({ userId }) => useSitesByUser(userId), {
      initialProps: { userId: 1 as number | null },
    })

    // Change userId before first fetch resolves — triggers cleanup (cancels first)
    rerender({ userId: 2 })

    // Wait for the second (current) fetch to resolve
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    // Now resolve the stale first request
    act(() => resolveFirst(mockSites))

    // State must still reflect user 2's empty result, not user 1's sites
    expect(result.current.sites).toEqual([])
  })
})
