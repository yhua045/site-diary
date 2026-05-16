# Site Diary — Design Plan

> **Scope:** Issue #6 — User Switcher (Dev-Mode Identity Simulation)
> **Methodology:** TDD — tests written before implementation
> **Reviewed with:** `mobile-ui` agent (dropdown styling)
> **Date:** 16 May 2026
> **Status:** LGTB — Ready for TDD handoff

---

## 1. Problem Statement

The backend already reads the `X-User-Id` HTTP header via `XUserIdMiddleware` and
stores the parsed integer in `HttpContext.Items[HttpContextKeys.UserId]`. There is
**no real authentication** yet — the frontend currently sends **no** `X-User-Id`
header, so all API calls are anonymous.

To enable end-to-end testing of user-scoped flows (diary authorship, role-derived
templates, site assignments), the frontend needs a way to **simulate different
users** without a login screen.

### Requirements

| # | Requirement |
|---|---|
| R1 | A "User" dropdown lives directly on the Diary screen (not in the app header) |
| R2 | On screen load, the dropdown fetches the list of users from the API |
| R3 | When making API requests (fetch timeline, create diary), the selected user's id is passed as `X-User-Id` |
| R4 | No Context, localStorage, or global AppShell integration |
| R5 | When no user is selected, `X-User-Id` is **absent** from requests (anonymous) |

---

## 2. Architecture

### 2.1 Data Flow

```
DiaryScreen
  │
  ├─ useState<User | null>(null)  ← selectedUser (local state only)
  ├─ useUserList()                ← fetches users from API on mount
  │
  └─ on user interaction (fetch timeline / create diary):
        passes selectedUser?.id to the relevant API call as X-User-Id header
```

Everything is self-contained inside `DiaryScreen`. No provider, no context, no
`localStorage`. The selection is intentionally ephemeral — it resets on refresh,
which is fine for a dev-mode placeholder.

---

## 3. File Changes

```
frontend/src/
│
├── features/users/
│   └── useUserList.ts           ← NEW
│       - calls usersApi.getAll()
│       - returns { users, isLoading, error }
│
└── features/diary/
    └── DiaryScreen.tsx          ← UPDATE
        - add useState<User | null> for selectedUser
        - render <UserSelect> inline (or inline <select>)
        - pass selectedUser?.id into API calls via X-User-Id header
```

No changes to `client.ts`, `AppShell`, `App.tsx`, or `context/`.

---

## 4. Component Specifications

### 4.1 `useUserList` (`features/users/useUserList.ts`)

```typescript
interface UseUserListResult {
  users: User[]
  isLoading: boolean
  error: string | null
}

export function useUserList(): UseUserListResult
```

- Calls `usersApi.getAll()` on mount.
- Returns active, non-archived users only (filter client-side: `!u.isArchived`).

---

### 4.2 User dropdown in `DiaryScreen`

The dropdown is rendered inline on the Diary screen — no separate component file
needed. It is a plain native `<select>` bound to local `useState`.

**UI sketch:**

```
┌──────────────────────────────────────────────────┐
│  Diary                                           │
│                                                  │
│  User: [ Jane Doe             ▼ ]                │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │  Timeline entries …                      │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────────────────────────────────┘
```

**Local state:**

```typescript
const [selectedUser, setSelectedUser] = useState<User | null>(null)
const { users, isLoading } = useUserList()
```

**Passing the selected user to API calls:**

When making a request, the caller supplies the user id as an explicit argument;
each API function accepts an optional `userId?: number` parameter and adds the
header only when provided:

```typescript
// Example: fetching the timeline
const entries = await diaryApi.getTimeline(siteId, { userId: selectedUser?.id })

// Inside diaryApi.getTimeline:
const headers: Record<string, string> = {}
if (userId !== undefined) headers['X-User-Id'] = String(userId)
const response = await api.get('/diary/timeline', { params: { siteId }, headers })
```

No global interceptor is needed — the header is applied per-call.

---

## 5. Interfaces / Abstractions Summary

| Abstraction | Type | File | Purpose |
|---|---|---|---|
| `useUserList()` | hook | `features/users/useUserList.ts` | Fetch and filter user list |
| `selectedUser` | local state | `DiaryScreen.tsx` | Currently selected user |
| `userId?` param | API option | per API function | Passes `X-User-Id` header per request |

---

## 6. TDD Test Plan

### 6.1 `useUserList.test.ts` (unit — mock `usersApi`)

| Test | Assertion |
|---|---|
| Returns `isLoading: true` initially | hook state before resolve |
| Returns users after fetch resolves | `users` array matches mock data |
| Filters out archived users | `isArchived: true` user absent from `users` |
| Returns `error` string on fetch failure | `error` is non-null |

---

### 6.2 `DiaryScreen.test.tsx` (integration — RTL + mocked hooks/API)

| Test | Assertion |
|---|---|
| Renders a user `<select>` on screen | `getByRole('combobox', { name: /user/i })` present |
| Dropdown lists all non-archived users | options count = users.length + 1 (placeholder) |
| Shows user's full name in each option | `"Jane Doe"` in DOM |
| Dropdown is disabled while users are loading | `select` has `disabled` attribute |
| Selecting a user updates the selected option | `select.value` equals `String(user.id)` |
| Fetching timeline with a user selected sends `X-User-Id` | mock API spy called with header `X-User-Id: "2"` |
| Fetching timeline with no user selected omits `X-User-Id` | mock API spy called without that header |

---

## 7. Implementation Notes

### 7.1 No backend changes required

The backend (`XUserIdMiddleware`) already handles the header. This feature is
purely a frontend concern.

### 7.2 Not a security boundary

This switcher is **explicitly a development / testing tool**. A real login screen
will replace it in a future issue. The dropdown selection is ephemeral (resets on
refresh) and is not persisted.

### 7.3 Dependencies

No new packages required. `axios` is already installed.

---

## 8. Acceptance Criteria

- [ ] All TDD tests (§6) pass before implementation begins.
- [ ] Selecting "Jane Doe" (id=2) in the dropdown causes the next API call to include `X-User-Id: 2`.
- [ ] With no user selected, API calls carry no `X-User-Id` header.
- [ ] Refreshing the page resets the dropdown to the placeholder (no persistence).
- [ ] No TypeScript errors (`tsc --noEmit` clean).
- [ ] No new lint warnings.
