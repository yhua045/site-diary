import { useState, useEffect } from 'react'
import { usersApi } from '../../api/users'
import type { ConstructionSite } from '../../api/types'

export interface UseSitesByUserResult {
  sites: ConstructionSite[]
  isLoading: boolean
  error: string | null
}

export function useSitesByUser(userId: number | null): UseSitesByUserResult {
  const [sites, setSites] = useState<ConstructionSite[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (userId === null) {
      setSites([])
      setIsLoading(false)
      setError(null)
      return
    }

    let cancelled = false

    setIsLoading(true)
    setError(null)

    usersApi
      .getSitesByUser(userId)
      .then(data => {
        if (!cancelled) {
          setSites(data.filter(s => !s.isArchived))
          setIsLoading(false)
        }
      })
      .catch(err => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to fetch sites')
          setSites([])
          setIsLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [userId])

  return { sites, isLoading, error }
}
