import { useState, useEffect } from 'react'
import { usersApi } from '../../api/users'
import type { User } from '../../api/types'

export interface UseUserListResult {
  users: User[]
  isLoading: boolean
  error: string | null
}

export function useUserList(): UseUserListResult {
  const [users, setUsers] = useState<User[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setIsLoading(true)
    usersApi
      .getAll()
      .then(all => {
        setUsers(all.filter(u => !u.isArchived))
        setError(null)
      })
      .catch(err => {
        setError(err instanceof Error ? err.message : 'Failed to fetch users')
      })
      .finally(() => setIsLoading(false))
  }, [])

  return { users, isLoading, error }
}
