# Site Diary — Design Plan: Sites Entry Screen & Routing

> **Scope:** Issue #7 — Sites Screen as App Entry Point  
> **Methodology:** TDD — tests written before implementation  
> **Reviewed with:** `mobile-ui` agent (card layout, dropdown UX, touch targets)  
> **Date:** 16 May 2026  
> **Status:** LGTB — Ready for TDD handoff

---

## 1. Problem Statement

`App.tsx` currently hardcodes `siteId=1` and passes it directly to `DiaryScreen`.
There is no way for a user to select which construction site they want to view.

The app needs a **Sites Screen** that:

1. Lets the user pick an operator (User dropdown).
2. Loads only the sites assigned to that user (Site dropdown).
3. Navigates to `DiaryScreen` with the chosen site and user context when **View Diary** is tapped.

---

## 2. Requirements

| # | Requirement |
|---|---|
| R1 | A "Sites" screen is the app entry point (`/`). |
| R2 | The screen has a **User** dropdown populated from `GET /api/users`. |
| R3 | Selecting a user triggers `GET /api/users/{userId}/sites` to populate the **Site** dropdown. |
| R4 | The **View Diary** button is disabled until a site is selected. |
| R5 | Clicking View Diary navigates to `/sites/:siteId/diary`, passing `{ siteName, userId, userName }` as router state. |
| R6 | `DiaryScreen` reads `siteId` from the URL param and user context from router state, removing the hardcoded values. |
| R7 | `DiaryScreen` pre-selects the user passed via router state in its internal user-filter dropdown. |
| R8 | The browser back button on `DiaryScreen` returns to the Sites screen. |

---

## 3. Architecture

### 3.1 Routing Configuration

React Router v7 (`react-router-dom` is already installed).

```
/                       → SitesScreen   (entry point)
/sites/:siteId/diary    → DiaryScreen   (diary timeline for a specific site)
```

`App.tsx` becomes a thin shell that wraps `<BrowserRouter>` + `<Routes>`.

### 3.2 Data Flow

```
SitesScreen
  │
  ├─ useUserList()
  │     └─ GET /api/users → users[]
  │
  ├─ useState<User | null>(null)            ← selectedUser
  │
  ├─ useSitesByUser(selectedUser?.id)
  │     └─ GET /api/users/{id}/sites → sites[]   (only when a user is selected)
  │
  ├─ useState<ConstructionSite | null>(null) ← selectedSite
  │
  └─ navigate(`/sites/${selectedSite.id}/diary`, {
         state: { siteName, userId, userName }
     })

DiaryScreen
  │
  ├─ useParams<{ siteId: string }>()    ← siteId (from URL)
  ├─ useLocation().state                ← { siteName, userId, userName }
  └─ (existing logic unchanged — user filter pre-selected from router state)
```

### 3.3 File Map

```
frontend/src/
│
├── App.tsx                              ← CHANGE: add BrowserRouter + Routes
├── main.tsx                             ← no change
│
├── api/
│   └── users.ts                        ← ADD: getSitesByUser(userId)
│
├── features/
│   ├── sites/
│   │   ├── SitesScreen.tsx             ← NEW: entry screen component
│   │   └── useSitesByUser.ts           ← NEW: hook — fetches sites for a user
│   │
│   └── diaries/
│       └── DiaryScreen.tsx             ← CHANGE: read siteId from useParams,
│                                              user context from useLocation().state
│
└── test/
    ├── sites/
    │   ├── SitesScreen.test.tsx        ← NEW: component tests
    │   └── useSitesByUser.test.ts      ← NEW: hook tests
    └── diary/
        └── DiaryScreen.test.tsx        ← UPDATE: wrap in MemoryRouter, test param reading
```

---

## 4. Component Specifications

### 4.1 `useSitesByUser` (`features/sites/useSitesByUser.ts`)

```typescript
interface UseSitesByUserResult {
  sites: ConstructionSite[]
  isLoading: boolean
  error: string | null
}

function useSitesByUser(userId: number | null): UseSitesByUserResult
```

**Behaviour:**
- Returns `{ sites: [], isLoading: false, error: null }` when `userId` is `null` (no fetch).
- When `userId` changes to a valid number, sets `isLoading: true` and fetches
  `GET /api/users/{userId}/sites`.
- Filters out archived sites (`isArchived === true`) client-side.
- On error, sets `error` string and leaves `sites: []`.
- Cancels in-flight requests when `userId` changes (AbortController or ignore-stale pattern).

**API call added to `usersApi`:**
```typescript
getSitesByUser: (userId: number) =>
  api.get<ConstructionSite[]>(`/users/${userId}/sites`).then(r => r.data)
```

---

### 4.2 `SitesScreen` (`features/sites/SitesScreen.tsx`)

**State:**
| Variable | Type | Initial |
|---|---|---|
| `selectedUser` | `User \| null` | `null` |
| `selectedSite` | `ConstructionSite \| null` | `null` |

**Derived:**
- `isSiteDropdownDisabled` = `selectedUser === null || sitesLoading`
- `isViewDiaryDisabled` = `selectedSite === null`

**Handlers:**
- `handleUserChange(e)` — sets `selectedUser`, resets `selectedSite` to `null`
- `handleSiteChange(e)` — sets `selectedSite` from `sites` array
- `handleViewDiary()` — calls `navigate(...)` with route state

**Render tree:**
```
<div>                          ← min-h-screen bg-slate-50
  <AppHeader />                ← reuses the existing header markup (h1 "Site Diary")
  <main>
    <div>                      ← card: bg-white rounded-2xl shadow-sm max-w-md mx-auto mt-16 p-8
      <h2>                     ← "Select Site"  text-lg font-semibold text-slate-800 mb-6
      <FormGroup label="User">
        <select>               ← user dropdown (useUserList)
      </FormGroup>
      <FormGroup label="Site">
        <select>               ← site dropdown (useSitesByUser), disabled until user picked
      </FormGroup>
      <button>                 ← "View Diary"  disabled until site picked
    </div>
  </main>
</div>
```

---

### 4.3 `DiaryScreen` changes (`features/diaries/DiaryScreen.tsx`)

Remove props `siteId: number` and `siteName: string`.

Add at top of component:
```typescript
const { siteId: siteIdParam } = useParams<{ siteId: string }>()
const location = useLocation()
const { siteName = 'Unknown Site', userId, userName } = location.state ?? {}
const siteId = Number(siteIdParam)
```

Initialize `selectedUser` from router state:
```typescript
// Pre-select the user that was chosen on the Sites screen
const { users } = useUserList()
const [selectedUser, setSelectedUser] = useState<User | null>(null)

useEffect(() => {
  if (userId && users.length > 0) {
    setSelectedUser(users.find(u => u.id === userId) ?? null)
  }
}, [userId, users])
```

Add back-navigation link in the header area (breadcrumb):
```tsx
<Link to="/" className="text-sm text-blue-600 hover:underline">← Sites</Link>
```

---

### 4.4 `App.tsx` changes

```tsx
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
```

The per-screen `<div className="min-h-screen bg-slate-50">` wrapper and `<header>` move into each screen component (or a shared `AppShell` layout — see §5 Trade-offs).

---

## 5. Trade-offs & Decisions

| Decision | Rationale |
|---|---|
| Router state (not URL query params) for `siteName`/`userId` | Keeps URLs clean; context is ephemeral (no bookmark requirement). |
| No React Context for user/site selection | Consistent with existing pattern (no global state, no Context). Ephemeral per-visit. |
| `useSitesByUser` as a separate hook | Testable in isolation; keeps `SitesScreen` thin. |
| Preserve user-filter dropdown in `DiaryScreen` | Users may want to switch the diary filter after landing (different role / audit). Pre-select provides a sensible default. |
| Shared `AppHeader` markup inlined per screen (not a separate component) | Avoid premature abstraction — only two screens use it. Extract only if a third screen is added. |
| No shared `AppShell` / layout route yet | Single `<header>` + `<main>` in each screen keeps nesting flat; revisit when nav grows. |

---

## 6. API Layer Changes

Only `usersApi` needs a new method:

```typescript
// frontend/src/api/users.ts  (addition)
getSitesByUser: (userId: number) =>
  api.get<ConstructionSite[]>(`/users/${userId}/sites`).then(r => r.data),
```

The backend endpoint `GET /api/users/{userId}/sites` already exists in `UsersController`.

---

## 7. Tailwind UI Specification

*Reviewed and approved by `mobile-ui` agent.*

### 7.1 SitesScreen Card

```
┌──────────────────────────────────────┐  max-w-md, centered, mt-16
│  Select Site                         │  text-lg font-semibold text-slate-800
│                                      │
│  User                                │  text-sm font-medium text-gray-700
│  ┌──────────────────────────────┐    │
│  │  — Select user —          ▼  │    │  w-full border rounded-lg px-3 py-2
│  └──────────────────────────────┘    │  focus:ring-2 focus:ring-blue-500
│                                      │
│  Site                                │  (disabled when no user selected)
│  ┌──────────────────────────────┐    │
│  │  — Select site —          ▼  │    │  opacity-50 cursor-not-allowed when disabled
│  └──────────────────────────────┘    │
│                                      │
│  ┌──────────────────────────────┐    │
│  │        View Diary            │    │  bg-blue-600 text-white rounded-lg
│  └──────────────────────────────┘    │  disabled:opacity-40 disabled:cursor-not-allowed
└──────────────────────────────────────┘
```

### 7.2 Tailwind Class Reference

| Element | Classes |
|---|---|
| Page background | `min-h-screen bg-slate-50` |
| Header bar | `bg-white shadow` |
| Header inner | `max-w-3xl mx-auto px-4 py-4 sm:px-6 lg:px-8` |
| App title | `text-xl font-bold tracking-tight text-slate-900` |
| Card wrapper | `max-w-md mx-auto mt-16 px-4` |
| Card | `bg-white rounded-2xl shadow-sm p-8` |
| Card heading | `text-lg font-semibold text-slate-800 mb-6` |
| Form group wrapper | `mb-5` |
| Label | `block text-sm font-medium text-gray-700 mb-1.5` |
| Select | `w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed` |
| Primary button | `w-full py-2.5 px-4 rounded-lg text-sm font-semibold bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors mt-2` |

### 7.3 Mobile-UI Agent Review Notes

- **Touch targets:** All interactive elements are `py-2` or taller, meeting the 44 px minimum tap target on mobile (`py-2` = 16px padding × 2 + ~20px font-size ≈ 52 px total height).
- **Card centering on small screens:** `mx-auto px-4` ensures the card has horizontal padding on narrow viewports rather than edge-bleeding.
- **Disabled state contrast:** `disabled:opacity-50` on selects and `disabled:opacity-40` on the button provide visual feedback while remaining accessible (greyed, not hidden).
- **No horizontal scroll:** `w-full` on selects and button prevents overflow on 320 px screens.
- **Focus ring:** `focus:ring-2 focus:ring-blue-500` is consistent with existing DiaryScreen select styling.
- **Loading state:** Site dropdown shows `opacity-50` + `cursor-not-allowed` while sites are loading, preventing premature interaction.

---

## 8. Test Plan (TDD)

### 8.1 `useSitesByUser` (unit)

| Test | Scenario |
|---|---|
| Returns empty sites + not loading when userId is null | No fetch triggered |
| Sets isLoading=true on valid userId | Loading state correct |
| Returns filtered (non-archived) sites on success | Data transformation |
| Sets error string on fetch failure | Error handling |
| Re-fetches when userId changes | Reactivity |
| Ignores stale response when userId changes during fetch | Race condition guard |

### 8.2 `SitesScreen` (component)

| Test | Scenario |
|---|---|
| Renders User dropdown with options from API | Fetch + render |
| Site dropdown is disabled on mount (no user selected) | Initial state |
| Selecting a user enables site dropdown and fetches sites | User interaction |
| Site dropdown populated with user's sites | Data flow |
| View Diary button is disabled when no site selected | Button guard |
| View Diary button enabled after selecting a site | Button enablement |
| Clicking View Diary navigates to `/sites/:id/diary` with state | Navigation |
| Changing user resets site selection | State reset |

### 8.3 `DiaryScreen` (updated)

| Test | Scenario |
|---|---|
| Reads siteId from URL params | Router integration |
| Reads siteName from location state | Router state |
| Pre-selects user from location state userId | State initialization |
| Back link navigates to `/` | Navigation |
| Falls back gracefully when no router state | Missing state guard |

---

## 9. Implementation Order (TDD)

```
1. useSitesByUser.ts (hook + tests)
2. usersApi addition (getSitesByUser)
3. SitesScreen.test.tsx (failing tests)
4. SitesScreen.tsx (make tests pass)
5. DiaryScreen.test.tsx updates
6. DiaryScreen.tsx (useParams + useLocation)
7. App.tsx (BrowserRouter + Routes)
```

---

*Design approved by architect agent and `mobile-ui` agent on 16 May 2026.*
