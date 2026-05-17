import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { SitesScreen } from './features/sites/SitesScreen'
import { DiaryScreen } from './features/diaries/DiaryScreen'

function App() {
  return (
    <div className="max-w-md mx-auto min-h-screen bg-slate-50 relative shadow-2xl overflow-x-hidden">
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<SitesScreen />} />
          <Route path="/sites/:siteId/diary" element={<DiaryScreen />} />
        </Routes>
      </BrowserRouter>
    </div>
  )
}

export default App
