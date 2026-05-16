import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const userId = localStorage.getItem('selectedUserId')
  if (userId) {
    config.headers.set('X-User-Id', userId)
  }
  return config
})

export default api
