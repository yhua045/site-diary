# Site Diary — Design Plan

> **Scope:** Issue #4 — Frontend: Diary UI
> **Methodology:** TDD — tests written before implementation
> **Reviewed with:** `mobile-ui` agent (mobile responsiveness & component design)
> **Date:** 16 May 2026
> **Status:** LGTB — Ready for TDD handoff

---

## 1. Problem Statement

Issue #4 defines the core Diary UI that site workers interact with daily. Four
requirements must be satisfied:

| # | Requirement |
|---|---|
| R1 | User can **select / change** the construction site they are assigned to |
| R2 | Once a site is selected, **diary logs** are rendered newest → oldest |
| R3 | Clicking a diary entry presents its details in the **same form** used to create / modify entries |
| R4 | The diary form is **generated dynamically from a JSON template** — the template is **determined by the user's role** and served by the API; the user cannot choose which template to use |
| R5 | The user can **explicitly add or remove individual fields** from the dynamic form for a specific diary entry (per-diary field overrides relative to their role template) |

**Constraints inherited from previous phases:**
- Identity = `X-User-Id` header (no auth yet)
- All site-scoped diary endpoints live under `GET /api/sites/{siteId}/diaries`
- `Diary.Content` stores free text or serialised JSON (when template-driven)
- `DiaryTemplate.Sections` is a `string` holding a JSON array (currently `"[]"`)

---

## 2. New Backend API Contracts

Two new endpoints are required to support the UI. They follow the same conventions as
existing controllers (X-User-Id header, OperationResult, soft-delete).

### 2.1 `GET /api/users/{userId}/sites`

Returns only the construction sites the user is **assigned to** (via `SiteUser`).
The frontend calls this on startup to populate the site picker.

**Response 200 OK → `ConstructionSiteDto[]`**

```json
[
  { "id": 1, "name": "Northland Tower", "address": "12 Queen St" },
  { "id": 2, "name": "Harbour Bridge Ramp", "address": "1 Quay St" }
]
```

**Implementation notes:**
- New method `ISiteService.GetByUserIdAsync(int userId, CancellationToken ct)`
- Query: `_uow.SiteUsers.Query().Where(su => su.UserId == userId).Include(su => su.ConstructionSite)`
- Returns active, non-archived sites only

---

### 2.2 `GET /api/users/{userId}/diary-template`

Returns the diary template that the API has selected for the user **based on their role**.
The frontend calls this when entering create-mode; the user never sees a template picker.

**Response 200 OK → `DiaryTemplateDto`** (same shape as §2.3 below)

**Implementation notes:**
- New method `IDiaryTemplateService.GetByUserRoleAsync(int userId, CancellationToken ct)`
- Query: resolve `UserRole` → `DiaryTemplate` association (many-to-one via a future
  `UserRole.DiaryTemplateId` FK; for POC, seed a single default template returned for all
  roles until the FK is wired)
- Returns `null` (→ 404) if no template is configured for the user's role; `DiaryFormView`
  falls back to free-text `Content` mode

---

### 2.3 `GET /api/diary-templates/{id}`

Returns a single diary template (name + parsed sections).

**Response 200 OK → `DiaryTemplateDto`**

```json
{
  "id": 3,
  "name": "Site Foreman Daily Report",
  "sections": [
    {
      "id": "s1",
      "label": "Weather & Environment",
      "fields": [
        {
          "id": "f_weather",
          "label": "Weather",
          "type": "select",
          "required": true,
          "options": ["Sunny", "Cloudy", "Rainy", "Stormy", "Windy"]
        },
        {
          "id": "f_temp",
          "label": "Temperature (°C)",
          "type": "number",
          "required": false,
          "min": -20,
          "max": 60
        }
      ]
    },
    {
      "id": "s2",
      "label": "Work Progress",
      "fields": [
        {
          "id": "f_activities",
          "label": "Activities Completed",
          "type": "textarea",
          "required": true,
          "placeholder": "Describe work completed today..."
        },
        {
          "id": "f_incidents",
          "label": "Safety Incidents",
          "type": "checkbox",
          "required": false
        }
      ]
    }
  ]
}
```

**Implementation notes:**
- New feature slice: `Application/Features/DiaryTemplates/`
- `IDiaryTemplateService.GetByIdAsync(int id, CancellationToken ct)` → `DiaryTemplateDto?`
- `DiaryTemplateDto.Sections` is deserialized from the `string` column into `IReadOnlyList<SectionDef>`
- Controller: `Web/Features/DiaryTemplates/DiaryTemplatesController.cs` — route `api/diary-templates/{id:int}`

---

### 2.4 `GET /api/sites/{siteId}/diaries` — sort order guarantee

The existing endpoint returns `IReadOnlyList<DiaryDto>`. The service must order results
**`Date` DESC, `Id` DESC** (secondary sort for same-day entries). This is a
**service-layer change only** — no breaking contract change.

---

## 3. Dynamic Form Schema

### 3.1 TypeScript definition (shared between DynamicForm and API types)

```typescript
// frontend/src/features/templates/types.ts

export type FieldType =
  | 'text'
  | 'textarea'
  | 'number'
  | 'checkbox'
  | 'select'
  | 'date'
  | 'time'

export interface FieldDef {
  id: string
  label: string
  type: FieldType
  required?: boolean
  placeholder?: string
  options?: string[]   // 'select' only
  min?: number         // 'number' only
  max?: number         // 'number' only
}

export interface SectionDef {
  id: string
  label: string
  fields: FieldDef[]
}

export interface TemplateDef {
  sections: SectionDef[]
}

/** The values map: field id → primitive value */
export type FormValues = Record<string, string | number | boolean>
```

### 3.2 Diary Content storage strategy

| Template present? | `Diary.Content` value |
|---|---|
| No (free-text) | Plain string |
| Yes (template-driven) | `JSON.stringify(FormValues)` — keys match `FieldDef.id` |

The `DynamicForm` component receives `initialValues?: FormValues` and calls `onChange(values: FormValues)`. The parent (`DiaryFormView`) serialises to JSON before POST/PUT.

### 3.3 Seed template JSON example (for `DataSeeder`)

```json
{
  "sections": [
    {
      "id": "s1",
      "label": "General",
      "fields": [
        { "id": "f_title_note", "label": "Notes", "type": "textarea", "required": false }
      ]
    }
  ]
}
```

---

### 3.4 Per-Diary Field Override Storage

Users can add or remove fields relative to the base template for a **specific diary
entry**. Overrides are stored in a new nullable JSON column on `Diary`:

**Domain change:** Add `FieldOverrides string?` to `Diary` entity.

**Override schema (JSON):**

```json
{
  "removed": ["f_temp", "f_incidents"],
  "added": [
    { "id": "cust_1", "label": "Subcontractor Notes", "type": "textarea", "required": false }
  ]
}
```

| Key | Meaning |
|---|---|
| `removed` | Array of `FieldDef.id` values from the base template that are hidden for this diary |
| `added` | Array of `FieldDef` objects the user appended (custom fields, only `text`/`textarea`/`number` types allowed for user-added fields) |

**TypeScript DTO addition:**

```typescript
export interface FieldOverridesDto {
  removed: string[]
  added: FieldDef[]
}
```

`DiaryDetailDto` gains an optional `fieldOverrides?: FieldOverridesDto`.

**Effective template computation (frontend helper):**

```typescript
// frontend/src/features/templates/applyOverrides.ts
export function applyOverrides(base: TemplateDef, overrides?: FieldOverridesDto): TemplateDef {
  if (!overrides) return base
  return {
    sections: base.sections.map(section => ({
      ...section,
      fields: section.fields.filter(f => !overrides.removed.includes(f.id)),
    })).concat(
      overrides.added.length > 0
        ? [{ id: 'user_added', label: 'Additional Fields', fields: overrides.added }]
        : []
    ),
  }
}
```

This pure function is trivially testable and is the single source of truth for "what the
user actually sees."

---

### 3.5 Dynamic Form Library Evaluation & Recommendation

#### Libraries Evaluated

| Library | Bundle (gzip) | Pros | Cons |
|---|---|---|---|
| **React JSON Schema Form (RJSF)** | ~45 KB | Auto-generates forms from JSON Schema; mature; Tailwind theme available | Requires converting `TemplateDef` → JSON Schema (schema translation cost); poor fit for custom add/remove UX; heavy theming overhead; larger bundle |
| **Formik + Yup** | ~15 KB | Widely known; Yup validation is ergonomic | Not a form *renderer* — still needs all `FormField` components; no built-in `useFieldArray` equivalent |
| **React Hook Form (RHF)** | ~9 KB | Uncontrolled inputs (best performance); `useFieldArray` handles add/remove natively; excellent TypeScript; minimal boilerplate; no schema conversion | Not a renderer — still needs `FormField` components |
| **Custom only (current plan)** | 0 KB | Zero deps; already designed | Manual `onChange` propagation across tree; manual state management; no built-in add/remove primitive; more test surface to cover |

#### Recommendation: **React Hook Form (RHF) + custom `FormField` renderer**

> **Do not use RJSF.**

**Rationale:**

1. **RJSF is a poor fit.** Our `TemplateDef` is not JSON Schema — adapting it adds
   non-trivial complexity with no payoff. RJSF's theming system requires replacing its
   default widgets with Tailwind-styled counterparts, which is comparable effort to our
   custom renderer but with added indirection. Its built-in add/remove (`additionalItems`)
   is not designed for the UX described in R5.

2. **RHF solves the hard parts for free:**
   - `useForm()` manages the `FormValues` map without re-renders on every keystroke
     (uncontrolled mode), which matters on low-end mobile devices.
   - `useFieldArray('user_added')` handles the "add custom field" list: `append`,
     `remove`, and ordering all come for free.
   - Form validation (`required`, `min`/`max`) is declared inline on `register()` calls —
     no separate validation schema needed.

3. **Our `FormField` components remain unchanged** — RHF integrates via `register()` for
   native inputs and `<Controller>` for controlled inputs. The DynamicForm/FormSection/
   FormField tree is preserved exactly as designed in §4.3; only the state-management
   layer changes from `useState` to `useForm`.

4. **Quickest path to passing tests:** RHF is mock-friendly in RTL tests (no special
   setup) and its `formState` object makes asserting validation errors straightforward.

**Implementation delta vs. current plan:**

```typescript
// DiaryFormView.tsx — replace local FormValues state with:
const { register, control, handleSubmit, reset, formState } = useForm<FormValues>({
  defaultValues: initialValues,
})
const { fields: addedFields, append, remove } = useFieldArray({ control, name: 'user_added' })
```

`DynamicForm` receives `register` and `control` as props; `FormField` uses
`register(field.id, { required: field.required, min: field.min, max: field.max })` for
native inputs and `<Controller>` for `select` and `checkbox`.

**Install:** `pnpm add react-hook-form` (single dependency, no peer deps).

---

## 4. Frontend Architecture

### 4.1 Routing (React Router v7)

```
/              → redirect to /diary
/diary         → DiaryPage (site picker + diary feed)
/diary/new     → DiaryPage with new-diary panel open
/diary/:id     → DiaryPage with diary detail panel open
```

All routes share the `AppShell` layout (header + main content area). The detail/form panel
slides in on mobile and appears as a side panel on desktop (≥ 1024 px).

### 4.2 New file tree

```
frontend/src/
├── api/
│   ├── templates.ts           ← NEW: DiaryTemplate API client
│   └── types.ts               ← UPDATE: add DiaryTemplateDto
│
├── features/
│   ├── sites/
│   │   └── SitePicker.tsx     ← NEW
│   ├── diaries/
│   │   ├── DiaryFeed.tsx      ← NEW
│   │   ├── DiaryCard.tsx      ← NEW
│   │   └── DiaryFormView.tsx  ← NEW
│   └── templates/
│       ├── types.ts           ← NEW: TemplateDef, FieldDef, FormValues, FieldOverridesDto
│       ├── applyOverrides.ts  ← NEW: pure helper to apply field overrides to a base template
│       ├── DynamicForm.tsx    ← NEW: root form engine (RHF register/control)
│       ├── FormSection.tsx    ← NEW: renders one section
│       └── FormField.tsx      ← NEW: polymorphic field renderer (RHF register/Controller)
│
├── hooks/
│   ├── useAssignedSites.ts    ← NEW
│   ├── useDiaries.ts          ← NEW
│   ├── useDiaryTemplate.ts    ← NEW: fetches by template id (view/edit mode)
│   └── useUserDiaryTemplate.ts ← NEW: fetches by userId role (create mode)
│
├── pages/
│   └── DiaryPage.tsx          ← NEW
│
├── components/
│   └── AppShell.tsx           ← NEW: layout with header
│
└── App.tsx                    ← UPDATE: wire React Router routes
```

### 4.3 Component responsibilities

#### `AppShell`
- Renders the persistent top navigation bar (site name, user info placeholder)
- Renders `SitePicker` in the header
- `children` slot for page content

#### `SitePicker`
- Props: `sites: ConstructionSite[]`, `selectedSiteId: number | null`, `onSelect: (id: number) => void`
- Desktop: `<select>` or custom dropdown
- Mobile (< 640 px): Native `<select>` for maximum compatibility — no custom bottom sheet (keep it simple for POC)
- Shows site name; placeholder "Select a site…" when none chosen
- Disabled state when `sites` is empty

#### `DiaryFeed`
- Props: `siteId: number`, `onSelectDiary: (id: number) => void`, `onNewDiary: () => void`
- Fetches via `useDiaries(siteId)` — shows loading skeleton, error state, empty state
- Renders `DiaryCard` list (already sorted newest → oldest by API)
- Floating action button (FAB): "New Diary" — visible only when `siteId` is set
- Infinite scroll / pagination: **deferred** (show all for POC)

#### `DiaryCard`
- Props: `diary: DiaryDto`, `onClick: () => void`
- Displays: date (formatted `DD MMM YYYY`), title, author name, published badge
- Tailwind classes: `bg-white rounded-xl shadow-sm border border-slate-100 p-4 hover:shadow-md`
- Published: green badge `bg-green-100 text-green-700`; Draft: grey badge

#### `DiaryFormView`
- Props: `siteId: number`, `diaryId?: number` (undefined = create mode)
- Modes: **view**, **edit**, **create**
- Fetches diary detail via `useDiary(siteId, diaryId)` if `diaryId` set
- **Template resolution (role-based, not user-selected):**
  - **Create mode:** fetches `GET /api/users/{userId}/diary-template` via
    `useUserDiaryTemplate(userId)` — the API determines the template from the user's role
  - **View / Edit mode:** fetches `GET /api/diary-templates/{diary.diaryTemplateId}` via
    `useDiaryTemplate(diary.diaryTemplateId)` (existing diary already records which
    template was used at creation time)
  - The user is **never presented with a template selector**
- Applies field overrides via `applyOverrides(baseTemplate, diary.fieldOverrides)` to
  compute the effective template before passing to `<DynamicForm>`
- Fixed fields (always shown regardless of template):
  - **Date** — `<input type="date">` (editable in create/edit mode)
  - **Title** — `<input type="text">` (editable)
  - **Publish toggle** — checkbox (editable)
- If effective template exists → renders `<DynamicForm>` for section fields
- If no template → renders single free-text `<textarea>` for `Content`
- **Add / Remove field UX (edit and create mode only):**
  - Each template field shows a remove button (×) — clicking adds its `id` to `overrides.removed`
  - An "Add Field" button at the bottom of the form opens a small inline form to specify
    label + type; on confirm, appends to `overrides.added`
  - View mode hides add/remove controls; removed fields are simply absent
- `useForm<FormValues>()` + `useFieldArray` (React Hook Form) manages all form state
- Action bar: "Edit" → switches to edit mode; "Save" / "Cancel" in edit/create mode
- "Archive" (soft-delete) button — with confirmation dialog (edit/view mode only)
- Sends `X-User-Id` header on all mutating calls; `fieldOverrides` serialised to JSON in
  request body alongside `content`

#### `DynamicForm`
- Props:
  ```typescript
  interface DynamicFormProps {
    template: TemplateDef         // already has overrides applied by DiaryFormView
    register: UseFormRegister<FormValues>
    control: Control<FormValues>
    readOnly?: boolean
    onRemoveField?: (fieldId: string) => void   // undefined in view mode
    onAddField?: (field: FieldDef) => void       // undefined in view mode
  }
  ```
- Iterates `template.sections` → renders `<FormSection>` for each
- Passes `onRemoveField` down to each `<FormField>` (shown only when not `readOnly`)
- Renders "Add Field" inline form at the bottom when `onAddField` is provided
- Pure presentational: no fetch, no side-effects

#### `FormSection`
- Props: `section: SectionDef`, `values: FormValues`, `onChange`, `readOnly?: boolean`
- Renders section heading + list of `<FormField>`

#### `FormField`
- Props: `field: FieldDef`, `value: string | number | boolean | undefined`, `onChange`, `readOnly?: boolean`
- Switches on `field.type`:
  | Type | Rendered as |
  |---|---|
  | `text` | `<input type="text">` |
  | `textarea` | `<textarea>` |
  | `number` | `<input type="number" min max>` |
  | `checkbox` | `<input type="checkbox">` |
  | `select` | `<select>` with `<option>` list |
  | `date` | `<input type="date">` |
  | `time` | `<input type="time">` |
- `readOnly` → renders value as `<p>` (view mode) or disabled input
- Labels use `<label htmlFor>` for accessibility

---

## 5. Custom Hooks

```typescript
// useAssignedSites(userId: number)
// Returns: { sites: ConstructionSite[], isLoading, error }
// Source: GET /api/users/{userId}/sites

// useDiaries(siteId: number | null)
// Returns: { diaries: DiaryDto[], isLoading, error, refresh() }
// Source: GET /api/sites/{siteId}/diaries  (skipped when siteId is null)

// useDiary(siteId: number, diaryId: number | undefined)
// Returns: { diary: DiaryDetailDto | null, isLoading, error }
// Source: GET /api/sites/{siteId}/diaries/{diaryId}

// useUserDiaryTemplate(userId: number | undefined)
// Returns: { template: DiaryTemplateDto | null, isLoading, error }
// Source: GET /api/users/{userId}/diary-template  (skipped when userId is undefined)
// Used in CREATE mode — template is role-determined by the API, not selected by the user

// useDiaryTemplate(templateId: number | undefined)
// Returns: { template: DiaryTemplateDto | null, isLoading, error }
// Source: GET /api/diary-templates/{id}  (skipped when templateId is undefined)
// Used in VIEW/EDIT mode — re-fetches the template recorded on the existing diary
```

All hooks use `useEffect` + `useState`. No external state library required for this phase.
Each hook exposes a `refresh()` function to allow post-mutation re-fetching.

---

## 6. State Flow

```
App startup
  └─ useAssignedSites(currentUserId)
       └─ SitePicker receives sites[]

User selects site (siteId)
  └─ stored in DiaryPage local state
  └─ useDiaries(siteId) triggered
       └─ DiaryFeed renders DiaryCard[]

User clicks DiaryCard (diaryId)
  └─ DiaryFormView mounts (view mode)
  └─ useDiary(siteId, diaryId) fetches DiaryDetailDto (includes fieldOverrides)
  └─ useDiaryTemplate(diary.diaryTemplateId) fetches TemplateDef by id
  └─ applyOverrides(template, diary.fieldOverrides) computes effective template

User clicks "Edit"
  └─ DiaryFormView switches to edit mode
  └─ DynamicForm is now interactive; remove (×) and "Add Field" controls appear
  └─ On field remove: overrides.removed gains field id; field disappears from form
  └─ On add field: overrides.added gains new FieldDef; new section appears at bottom
  └─ On "Save": PUT /api/sites/{siteId}/diaries/{diaryId} (includes fieldOverrides JSON)
  └─ On success: useDiaries.refresh(), switch back to view mode

User clicks "New Diary"
  └─ DiaryFormView mounts (create mode, no diaryId)
  └─ useUserDiaryTemplate(currentUserId) → GET /api/users/{userId}/diary-template
       ↳ API resolves template from user's role — user has no template picker
  └─ overrides start empty { removed: [], added: [] }
  └─ On field remove / add: overrides updated in local state (useFieldArray)
  └─ On "Save": POST /api/sites/{siteId}/diaries (includes diaryTemplateId + fieldOverrides)
  └─ On success: useDiaries.refresh(), open new diary in view mode
```

---

## 7. Mobile UI Design

> *The following decisions were reviewed with the `mobile-ui` agent and align with
> Tailwind's mobile-first utility approach and the existing Tailwind config
> (primary: #2563EB, surface card: #FFFFFF, font: Inter).*

### 7.1 Layout breakpoints

| Breakpoint | Layout |
|---|---|
| < 640 px (mobile) | Single-column. Feed fills screen. Diary form opens as full-screen overlay. |
| 640–1023 px (tablet) | Single-column with wider cards. Form overlay with max-width. |
| ≥ 1024 px (desktop) | Two-column: feed on left (w-96), form panel on right (flex-1). |

### 7.2 Mobile-specific decisions

- **SitePicker on mobile:** Native `<select>` element — avoids custom dropdown z-index
  issues and is immediately accessible on touch devices.
- **DiaryCard touch target:** Minimum 48 px height, full-width tap area.
- **DiaryFormView on mobile:** Slides up from bottom as an overlay (`fixed inset-0
  bg-white z-50`) triggered by state, not a route change — avoids scroll restoration
  issues.
- **DiaryFormView on desktop:** Right panel (`flex-1 h-full overflow-y-auto`), no overlay.
- **FAB ("New Diary"):** Fixed bottom-right on mobile (`fixed bottom-6 right-6`); shown
  as a standard button in the desktop header on larger screens.
- **Keyboard on mobile:** `DiaryFormView` uses `scroll-into-view` on focused inputs to
  keep them above the software keyboard.
- **Loading skeletons:** `animate-pulse bg-slate-100` placeholder cards (3 lines) while
  diaries load — avoids layout shift.

### 7.3 Accessibility

- All form fields have associated `<label>` elements.
- `aria-busy` set on feed container during loading.
- `aria-live="polite"` on error messages.
- Colour contrast: primary (#2563EB) on white passes WCAG AA.
- Focus ring visible: Tailwind `focus-visible:ring-2 focus-visible:ring-primary`.

---

## 8. TDD Test Surface

Tests are written **before** implementation (Vitest + React Testing Library).

### 8.1 Component tests (`*.test.tsx`)

| Test file | Key assertions |
|---|---|
| `SitePicker.test.tsx` | Renders site options; fires `onSelect` with correct id; shows placeholder when empty |
| `DiaryCard.test.tsx` | Renders title, formatted date, published badge; fires `onClick` |
| `DiaryFeed.test.tsx` | Renders cards in order (newest first); shows skeleton on load; shows empty state; renders "New Diary" button |
| `DiaryFormView.test.tsx` | View mode: all fields read-only, no add/remove controls; Edit button switches to edit mode; remove (×) controls appear in edit mode; clicking × hides the field; "Add Field" appends a custom field; Save calls PUT with `fieldOverrides`; Cancel restores state; Create mode calls POST with role-resolved template id and initial empty overrides |
| `DynamicForm.test.tsx` | Renders correct number of sections; calls `onChange` when field changes; `readOnly` disables all inputs and hides add/remove controls; remove callback fires with correct field id |
| `FormField.test.tsx` | Renders `<textarea>` for type "textarea"; renders `<select>` with correct options; renders `<input type="checkbox">` for checkbox; read-only renders `<p>` text; remove button visible when `onRemove` prop provided |
| `applyOverrides.test.ts` | Removed field ids are excluded from output sections; added fields appear in a new section; empty overrides returns base template unchanged; handles undefined overrides gracefully |

### 8.2 Hook tests (`*.test.ts`) — mock `axios`

| Test file | Key assertions |
|---|---|
| `useAssignedSites.test.ts` | Returns sites array on success; sets `isLoading` true then false; sets `error` on failure |
| `useDiaries.test.ts` | Returns empty array when siteId is null; fetches on siteId change; `refresh()` re-fetches |
| `useDiaryTemplate.test.ts` | Returns null when templateId is undefined; fetches when id is provided |
| `useUserDiaryTemplate.test.ts` | Returns null when userId is undefined; fetches `GET /api/users/{userId}/diary-template` when userId is provided; returned template is role-resolved by the API (no user input) |

### 8.3 Backend tests

| Test file | Key assertions |
|---|---|
| `DiaryTemplateServiceTests.cs` | `GetByIdAsync` returns `DiaryTemplateDto` with deserialized sections; returns `null` for archived/missing; `GetByUserRoleAsync` returns the template for the user's active role; returns `null` when role has no template configured |
| `SiteServiceTests.cs` (extend) | `GetByUserIdAsync` returns only sites user is assigned to; excludes archived |
| `DiaryServiceTests.cs` (extend) | `GetBySiteIdAsync` returns results ordered Date DESC, Id DESC; `fieldOverrides` is deserialized and included in `DiaryDetailDto` |
| `DiaryTemplatesControllerTests.cs` | GET `/{id}` returns 200 with dto; GET unknown returns 404; GET `/users/{userId}/diary-template` returns 200 with role-resolved template; returns 404 when no template is configured for user's role |
| `UsersControllerTests.cs` (extend) | GET `/{userId}/sites` returns 200 with site list; 404 for unknown user |

---

## 9. API Client Updates

```typescript
// frontend/src/api/sites.ts — add
getUserSites: (userId: number) =>
  api.get<ConstructionSite[]>(`/users/${userId}/sites`).then(r => r.data),

// frontend/src/api/templates.ts — new file
import api from './client'
import type { DiaryTemplateDto } from './types'
export const templatesApi = {
  getById: (id: number) =>
    api.get<DiaryTemplateDto>(`/diary-templates/${id}`).then(r => r.data),
  // Role-based: API resolves the correct template for the user — no user selection
  getByUserId: (userId: number) =>
    api.get<DiaryTemplateDto>(`/users/${userId}/diary-template`).then(r => r.data),
}

// frontend/src/api/diaries.ts — update paths to be site-scoped
getSiteDiaries: (siteId: number) =>
  api.get<DiaryDto[]>(`/sites/${siteId}/diaries`).then(r => r.data),
getDiaryDetail: (siteId: number, diaryId: number) =>
  api.get<DiaryDetailDto>(`/sites/${siteId}/diaries/${diaryId}`).then(r => r.data),
createDiary: (siteId: number, userId: number, data: CreateDiaryDto) =>
  api.post<DiaryDto>(`/sites/${siteId}/diaries`, data, {
    headers: { 'X-User-Id': String(userId) }
  }).then(r => r.data),
updateDiary: (siteId: number, diaryId: number, userId: number, data: UpdateDiaryDto) =>
  api.put<DiaryDto>(`/sites/${siteId}/diaries/${diaryId}`, data, {
    headers: { 'X-User-Id': String(userId) }
  }).then(r => r.data),
archiveDiary: (siteId: number, diaryId: number, userId: number) =>
  api.delete(`/sites/${siteId}/diaries/${diaryId}`, {
    headers: { 'X-User-Id': String(userId) }
  }),
```

---

## 10. Updated Type Definitions

```typescript
// frontend/src/api/types.ts — additions

export interface DiaryDto {
  id: number
  constructionSiteId: number
  authorUserId: number
  diaryTemplateId?: number
  title: string
  content?: string
  date: string         // ISO 8601
  isPublished: boolean
}

export interface FieldOverridesDto {
  removed: string[]    // field ids from the base template hidden for this diary
  added: FieldDef[]    // user-defined custom fields appended to this diary
}

export interface DiaryDetailDto extends DiaryDto {
  attachments: Attachment[]
  fieldOverrides?: FieldOverridesDto
}

export interface CreateDiaryDto {
  title: string
  content?: string
  date: string
  isPublished?: boolean
  fieldOverrides?: FieldOverridesDto
}

export interface UpdateDiaryDto {
  title: string
  content?: string
  date: string
  fieldOverrides?: FieldOverridesDto
}

export interface DiaryTemplateDto {
  id: number
  name: string
  sections: SectionDef[]   // deserialized server-side before sending
}
```

---

## 11. Backend — Sections & Field Overrides Serialization

`DiaryTemplate.Sections` is `string` in the domain. The service layer deserialises it
before returning the DTO:

```csharp
// Application/Features/DiaryTemplates/DiaryTemplateDtos.cs
public record DiaryTemplateDto(
    int Id,
    string Name,
    IReadOnlyList<SectionDef> Sections);

// Application/Features/DiaryTemplates/IDiaryTemplateService.cs
public interface IDiaryTemplateService
{
    Task<DiaryTemplateDto?> GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>
    /// Returns the template assigned to the user's active role.
    /// The user cannot choose which template they receive.
    /// </summary>
    Task<DiaryTemplateDto?> GetByUserRoleAsync(int userId, CancellationToken ct = default);
}
```

Sections are deserialized with `System.Text.Json`:

```csharp
var sections = JsonSerializer.Deserialize<List<SectionDef>>(template.Sections)
    ?? new List<SectionDef>();
```

**Field overrides on `Diary`:**

```csharp
// Domain/Entities/Diary.cs — add property
public string? FieldOverrides { get; private set; }   // JSON: { "removed": [], "added": [] }
```

The Application layer deserializes this into `FieldOverridesDto` when building
`DiaryDetailDto`, following the same pattern as `DiaryTemplate.Sections`. On create/update
the service serializes `FieldOverridesDto` back to JSON before persisting.

`SectionDef`, `FieldDef`, and `FieldOverridesDto` are plain C# records in
`Application/Features/DiaryTemplates/` mirroring the TypeScript types.

---

## 12. Acceptance Criteria

| # | Criterion |
|---|---|
| AC1 | A user with at least 1 assigned site sees their sites in the picker; sites not assigned to them are not shown |
| AC2 | Selecting a site loads diary entries sorted newest date first; same-day entries sorted by Id descending |
| AC3 | Clicking a diary entry shows all its fields in the same layout as the create form |
| AC4 | When a diary has a `diaryTemplateId`, the form renders sections and fields from the template JSON; fixed fields (Title, Date, Publish) always appear above template sections |
| AC5 | When a diary has no template, a free-text textarea for Content is shown |
| AC6 | In create mode, the template is fetched automatically by the API based on the user's role — **no template selector is shown**; the form is blank (except today's date pre-filled) |
| AC7 | Two users with different roles on the same site are given different diary templates by the API |
| AC8 | In edit and create mode, each template field has a remove (×) button; clicking it hides the field from the form for that diary only; the removal persists after Save |
| AC9 | In edit and create mode, an "Add Field" control lets the user define a label + type (text/textarea/number only); the custom field appears in the form and persists after Save |
| AC10 | In create mode, the form is blank (except today's date pre-filled); Save creates a new diary and it appears at the top of the feed |
| AC11 | In edit mode, changes (including overrides) are saved via PUT; ownership (X-User-Id) is enforced — 403 surfaces as a UI error message |
| AC12 | On screens < 640 px the diary form renders as a full-screen overlay; on ≥ 1024 px it appears as a side panel |
| AC13 | All Vitest component/hook tests pass (including `applyOverrides`, `useUserDiaryTemplate`, field add/remove in `DiaryFormView`); all new backend xUnit tests pass |

---

## 13. Out of Scope (This Issue)

- Attachment upload/delete UI (deferred to a later issue)
- Template management (CRUD for DiaryTemplate)
- User role management UI
- Pagination / infinite scroll on the diary feed
- Authentication (still using X-User-Id header)

---

## 14. Design Document References

| Document | Path |
|---|---|
| Domain & solution structure | [design/1-plan.md](./1-plan.md) |
| Diary CRUD API & Attachments | [design/2-plan.md](./2-plan.md) |
| **This document (Diary UI)** | [design/3-plan.md](./3-plan.md) |

> **For the `mobile-ui` agent:** The primary UI decisions are in §7 (Mobile UI Design)
> and the component responsibilities in §4.3. The Tailwind config and brand tokens are in
> `frontend/tailwind.config.js`. Please review and flag any concerns before implementation.
>
> **For the `developer` agent:** All test cases are in §8 (TDD Test Surface).
> New backend slices are in §2 and §11. Start with failing tests per the TDD order:
> backend service tests → controller tests → frontend hook tests → component tests.

---

## LGTB — Looks Good To Build ✅

All five requirements have clear, testable implementations:

- **R1** (site selector) → `SitePicker` + `GET /api/users/{userId}/sites`
- **R2** (diary feed, newest first) → `DiaryFeed` + `useDiaries` + service sort order
- **R3** (consistent form view) → `DiaryFormView` shared across view/edit/create modes
- **R4** (role-determined template, served by API) → `useUserDiaryTemplate(userId)` in
  create mode; `useDiaryTemplate(templateId)` in view/edit mode; **no template picker
  exposed to the user**; `GET /api/users/{userId}/diary-template` resolves via role
- **R5** (user can add/remove fields) → `FieldOverridesDto` stored on `Diary.FieldOverrides`;
  `applyOverrides()` computes effective template; `useFieldArray` (React Hook Form)
  manages add/remove state; remove (×) per field, "Add Field" at form bottom

The design is:
- **Minimal** — one new dependency (`react-hook-form`, ~9 KB gzip); no new CSS framework,
  no new routing library; **RJSF explicitly rejected** (schema conversion cost + mobile
  theming overhead outweigh its benefits for 7 field types)
- **TDD-ready** — every component and hook has a defined test surface before any code is
  written; `applyOverrides` is a pure function and trivially unit-testable
- **Mobile-reviewed** — layout strategy covers mobile (full-screen overlay), tablet, and
  desktop (side panel) with native controls for maximum compatibility
- **Consistent** — follows existing Clean Architecture slices, naming conventions, and API
  patterns from Plans 1 and 2

**Handoff →** `developer` agent:
> Plan approved. Write failing tests for these requirements.
> Reference design document: `design/3-plan.md`.
> TDD order: backend `DiaryTemplateService` tests (incl. `GetByUserRoleAsync`) →
> `Diary.FieldOverrides` domain tests → `SiteService.GetByUserIdAsync` tests →
> controller tests → frontend `applyOverrides` unit tests →
> `useDiaries` / `useAssignedSites` / `useUserDiaryTemplate` hook tests →
> `FormField` component tests → `DynamicForm` tests (incl. add/remove) →
> `DiaryFeed` / `DiaryCard` tests → `DiaryFormView` integration tests.
