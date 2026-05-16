import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useUserList } from '../users/useUserList'
import { useSitesByUser } from './useSitesByUser'
import type { User, ConstructionSite } from '../../api/types'

export function SitesScreen() {
  const navigate = useNavigate()
  const { users, isLoading: usersLoading } = useUserList()
  const [selectedUser, setSelectedUser] = useState<User | null>(null)
  const [selectedSite, setSelectedSite] = useState<ConstructionSite | null>(null)

  const { sites, isLoading: sitesLoading } = useSitesByUser(selectedUser?.id ?? null)

  const isSiteDropdownDisabled = selectedUser === null || sitesLoading
  const isViewDiaryDisabled = selectedSite === null

  function handleUserChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const id = Number(e.target.value)
    if (id) {
      localStorage.setItem('selectedUserId', String(id))
      setSelectedUser(users.find(u => u.id === id) ?? null)
    } else {
      localStorage.removeItem('selectedUserId')
      setSelectedUser(null)
    }
    setSelectedSite(null)
  }

  function handleSiteChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const id = Number(e.target.value)
    setSelectedSite(id ? (sites.find(s => s.id === id) ?? null) : null)
  }

  function handleViewDiary() {
    if (!selectedSite || !selectedUser) return
    navigate(`/sites/${selectedSite.id}/diary`, {
      state: {
        siteName: selectedSite.name,
        userId: selectedUser.id,
        userName: `${selectedUser.firstName} ${selectedUser.lastName}`,
      },
    })
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white shadow">
        <div className="max-w-3xl mx-auto px-4 py-4 sm:px-6 lg:px-8">
          <h1 className="text-xl font-bold tracking-tight text-slate-900">Site Diary</h1>
        </div>
      </header>
      <main>
        <div className="max-w-md mx-auto mt-16 px-4">
          <div className="bg-white rounded-2xl shadow-sm p-8">
            <h2 className="text-lg font-semibold text-slate-800 mb-6">Select Site</h2>

            <div className="mb-5">
              <label
                htmlFor="user-select"
                className="block text-sm font-medium text-gray-700 mb-1.5"
              >
                User
              </label>
              <select
                id="user-select"
                aria-label="User"
                value={selectedUser?.id ?? ''}
                onChange={handleUserChange}
                disabled={usersLoading}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <option value="">— Select user —</option>
                {users.map(user => (
                  <option key={user.id} value={user.id}>
                    {user.firstName} {user.lastName}
                  </option>
                ))}
              </select>
            </div>

            <div className="mb-5">
              <label
                htmlFor="site-select"
                className="block text-sm font-medium text-gray-700 mb-1.5"
              >
                Site
              </label>
              <select
                id="site-select"
                aria-label="Site"
                value={selectedSite?.id ?? ''}
                onChange={handleSiteChange}
                disabled={isSiteDropdownDisabled}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <option value="">— Select site —</option>
                {sites.map(site => (
                  <option key={site.id} value={site.id}>
                    {site.name}
                  </option>
                ))}
              </select>
            </div>

            <button
              onClick={handleViewDiary}
              disabled={isViewDiaryDisabled}
              className="w-full py-2.5 px-4 rounded-lg text-sm font-semibold bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors mt-2"
            >
              View Diary
            </button>
          </div>
        </div>
      </main>
    </div>
  )
}
