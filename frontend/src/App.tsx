import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { SitesScreen } from './features/sites/SitesScreen'
import { DiaryScreen } from './features/diaries/DiaryScreen'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<SitesScreen />} />
        <Route path="/sites/:siteId/diary" element={<DiaryScreen />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
