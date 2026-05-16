# Diary UI Screens Design Plan (Phase 3)

> **Revision note (16 May 2026):** Redesigned from a List + Detail two-page model to a single **Timeline Page** following user feedback. The separate Diary Detail Page has been removed. Reviewed with the `mobile-ui` agent for mobile layout best practices.

---

## 1. Overview

This document outlines the UI screens and interactions for the Diary Management features tracked under Phase 3 (Issue #5). The design integrates seamlessly with the existing layout and Tailwind CSS style of the application.

The core interaction model is a **single scrollable timeline page** per construction site. All diary log entries are rendered inline in full, ordered **newest → oldest**. There is no separate detail page — users simply scroll to read any log. Infinite-scroll pagination will be added in a later phase.

---

## 2. Screens to Implement

### 2.1 Diary Timeline Page (replaces former List + Detail pages)

**Purpose:** Provide a chronological, reverse-ordered feed of all diary log entries for a given construction site. Users read entries by scrolling; they create new entries via two entry points in the same page.

#### 2.1.1 Page Header (sticky)

| Element | Detail |
|---|---|
| Back / breadcrumb | Returns to Site list |
| Title | Site name (e.g., "Harbour Bridge Extension") |
| Subtitle | "Site Diary" |
| **`[+]` icon button** | Right-aligned in the header. Tapping opens the **Diary Create Form** (modal/sheet). This is the primary desktop-friendly entry point. |

The header is **sticky** so the `[+]` button is always reachable without scrolling.

#### 2.1.2 Timeline Feed

- Entries are ordered **newest first** (descending by `DiaryDate`).
- Each entry is a self-contained **Diary Log Card** (see §2.1.3) rendered at full detail — no tap-to-expand needed.
- A thin **vertical timeline connector line** runs along the left edge between cards to reinforce the chronological narrative.
- Date-group separators (e.g., a floating pill label `"May 2026"`) appear when the month changes as the user scrolls, providing orientation.

#### 2.1.3 Diary Log Card

Each card contains:

1. **Card Header**
   - Date badge (e.g., `Wed 15 May 2026`) — prominent, left-aligned.
   - Author chip: avatar initial + full name + role (e.g., `J · John Smith — Foreman`).

2. **Dynamic Fields Area**
   - Fields are rendered from the **template descriptor snapshot** stored alongside the diary entry (see §6 for the full rendering strategy).
   - Fields appear in their declared `displayOrder`.
   - Supported display types: `text`, `number`, `boolean` → *Yes / No*, `date` → locale-formatted string, `select` → chosen label.
   - Unknown field types fall back to a plain `key: value` string row.
   - Fields present in the template but absent from the saved payload display as `—` (not hidden), preserving layout consistency across cards.
   - Read-only display; no interaction required.

3. **Attachments** *(omitted if no attachments)*
   - **Image files** are rendered **inline** directly inside the card, below the dynamic fields area. Each image is displayed as a full-width (or capped-height) `<img>` tag so the user sees it immediately without any tap. Up to 3 images are shown at full size; if more exist, the 4th slot becomes a `+N more` overlay tapping into a lightbox gallery.
   - A tap/click on any inline image still opens the full-screen lightbox for a better view, but this is an *enhancement*, not the primary access path.
   - **Non-image attachments** (PDFs, spreadsheets, etc.) appear below the image block as a compact chip row: file-type icon + file name + download arrow. They do **not** render inline.

#### 2.1.4 Empty State

Shown when no diary entries exist for the site:

- Centered illustration (construction icon or generic empty-list icon).
- Heading: *"No diary entries yet"*
- Body: *"Tap the + button to log the first entry for this site."*
- A prominent **"Add First Entry"** button below (equivalent to pressing `[+]`).

#### 2.1.5 FAB (Floating Action Button)

- Fixed position: **bottom-right**, above the mobile nav bar / safe-area inset.
- Style: circular, primary brand colour, white `+` icon, elevation shadow.
- Tapping opens the same **Diary Create Form** as the header `[+]` button.
- The FAB is the primary mobile entry point; the header `[+]` serves desktop / tablet layouts.

---

### 2.2 Diary Create Form (modal / bottom sheet)

**Purpose:** Submit a new diary log entry based on the role-derived template. Opened from either the header `[+]` button or the FAB.

**Presentation:**
- On **mobile**: slides up as a **bottom sheet** (≥80 % viewport height, draggable dismiss).
- On **desktop**: opens as a **centred modal dialog**.

**Components:**

| Component | Detail |
|---|---|
| Form Header | Title "New Diary Entry", `×` close / cancel, `Save` / `Submit` primary action |
| Dynamic Form Area | Fetches template from `/api/users/{userId}/diary-template`. Renders fields in declared order with appropriate input controls (text input, number input, toggle, date picker). |
| Inline validation | Per-field error messages below each input, driven by `react-hook-form` + `zod`. |
| Field Overrides | `+ Add` / `− Remove` controls adjacent to optional/repeatable sections. |
| Upload Area | Drop-zone / file-picker for photos and PDFs. Thumbnail preview, per-file progress bar, remove button. |

On successful submit the sheet/modal closes and the new entry is **prepended** at the top of the timeline feed without a full page reload.

---

## 3. UI Mockup (ASCII)

```
┌──────────────────────────────────────────────────────┐
│  ←  Harbour Bridge Extension              Site Diary [+] │  ← Sticky header
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌── MAY 2026 ──────────────────────────────────┐   │  ← Month pill separator
│                                                      │
│  │  ┌─────────────────────────────────────────┐ │   │
│  │  │ 📅  Wed, 15 May 2026                    │ │   │
│  │  │ 👤  J · John Smith — Foreman            │ │   │
│  │  ├─────────────────────────────────────────┤ │   │
│  │  │ Weather:   Sunny, 22 °C                 │ │   │
│  │  │ Workers on site:  12                    │ │   │
│  │  │ Notes:  Poured west concrete slab.      │ │   │
│  │  │         Crane inspection completed.     │ │   │
│  │  ├─────────────────────────────────────────┤ │   │
│  │  │ ┌─────────────┐ ┌─────────────┐         │ │   │  ← Inline images
│  │  │ │ [photo 1]   │ │ [photo 2]   │         │ │   │
│  │  │ └─────────────┘ └─────────────┘         │ │   │
│  │  │ 📎 report.pdf  ↓                        │ │   │  ← Doc chip
│  │  └─────────────────────────────────────────┘ │   │
│  │                    │                          │   │  ← Timeline connector
│  │  ┌─────────────────────────────────────────┐ │   │
│  │  │ 📅  Tue, 14 May 2026                    │ │   │
│  │  │ 👤  J · Jane Doe — Site Manager         │ │   │
│  │  ├─────────────────────────────────────────┤ │   │
│  │  │ Weather:   Rainy, 16 °C                 │ │   │
│  │  │ Workers on site:  8                     │ │   │
│  │  │ Notes:  Work delayed due to rain.       │ │   │
│  │  │         Safety briefing conducted.      │ │   │
│  │  │ Safety Check:  —                        │ │   │  ← missing field → dash
│  │  └─────────────────────────────────────────┘ │   │
│  │                    │                          │   │
│  │  ┌─────────────────────────────────────────┐ │   │
│  │  │ 📅  Mon, 13 May 2026          ...        │ │   │
│  │  └─────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────┘   │
│                                                      │
│                                         ┌───┐        │
│                                         │ + │  ← FAB │
│                                         └───┘        │
└──────────────────────────────────────────────────────┘

  ── On tapping [+] header button OR FAB ──────────────

┌──────────────────────────────────────────────────────┐
│░░░░░░░░░░░░░░ (dimmed timeline behind) ░░░░░░░░░░░░░░│
├──────────────────────────────────────────────────────┤  ← Bottom sheet / modal
│  ╔══════════════════════════════════════════════╗    │
│  ║  New Diary Entry                          [×] ║    │
│  ╠══════════════════════════════════════════════╣    │
│  ║  Date:      [ 16 May 2026        ▾ ]         ║    │
│  ║  Weather:   [ _________________ ]            ║    │
│  ║  Workers:   [ __ ]                           ║    │
│  ║  Notes:     [ _______________________________ ║    │
│  ║             |                               ] ║    │
│  ║                                             ║    │
│  ║  Attachments:  [ + Add Photo / File ]        ║    │
│  ║  ┌──────┐ ┌──────┐                          ║    │
│  ║  │img 1 │ │ pdf  │                          ║    │
│  ║  └──────┘ └──────┘                          ║    │
│  ║                                             ║    │
│  ║              [ Cancel ]  [ Save Entry ]      ║    │
│  ╚══════════════════════════════════════════════╝    │
└──────────────────────────────────────────────────────┘
```

---

## 4. Interaction & Navigation Summary

| User Action | Result |
|---|---|
| Tap `[+]` in page header | Open Diary Create Form (modal / sheet) |
| Tap FAB `+` | Open Diary Create Form (modal / sheet) |
| Scroll down | Browse older diary entries (newest → oldest) |
| View inline image in card | Images are visible immediately; no tap required |
| Tap inline image | Full-screen lightbox (zoom / swipe gallery) |
| Tap document chip | Download / in-app preview |
| Submit new entry | Sheet closes; new entry prepended to top of timeline |
| Cancel form | Sheet dismissed; no changes |

There is **no separate Diary Detail page**. All content is read inline in the timeline.

---

## 5. Technical & Styling Notes

> **See §6 for the dedicated dynamic field rendering strategy.**

- **Styling**: Tailwind CSS utility classes consistent with the rest of the repository. The FAB uses `fixed bottom-6 right-6 z-50` with a `shadow-lg` elevation. The sticky header uses `sticky top-0 z-40 bg-white/90 backdrop-blur`.
- **Form State**: `react-hook-form` with `zod` schema validation.
- **Data Fetching**: `GET /api/sites/{siteId}/diaries` returns entries ordered by `DiaryDate DESC`. The component renders them in that order; infinite-scroll pagination (`?page=&pageSize=`) to be wired in a later iteration.
- **Optimistic UI**: On create-submit, the new entry is prepended to the local list immediately; rolled back on API error.
- **Accessibility**: FAB includes `aria-label="Add diary entry"`. Header `[+]` button carries the same label. Focus is trapped in the modal/sheet when open.
- **Responsive breakpoints**: Bottom-sheet on `< md`, centred modal on `>= md`.

---

## 6. Dynamic Field Rendering Strategy

The diary payload is inherently dynamic: fields vary by the submitting user's role and by any per-entry field overrides the user made at creation time (using react-hook-form's `useFieldArray`). The card renderer must faithfully reconstruct a human-readable view from raw JSON without hard-coding field names.

### 6.1 Template Snapshot — the source of truth

At the moment a diary entry is **created**, the backend should embed a **snapshot** of the effective field descriptor array in the saved record (e.g., as `DiaryEntry.TemplateSnapshot: JSON`). This decouples the card renderer from the live template and ensures cards remain accurate even if the template is later edited.

```jsonc
// DiaryEntry (simplified)
{
  "id": 42,
  "diaryDate": "2026-05-15",
  "payload": {
    "weather": "Sunny, 22 °C",
    "workersOnSite": 12,
    "notes": "Poured west concrete slab.",
    "safetyCheck": true
  },
  "templateSnapshot": [
    { "key": "weather",        "label": "Weather",          "type": "text",    "displayOrder": 1 },
    { "key": "workersOnSite",  "label": "Workers on site",  "type": "number",  "displayOrder": 2 },
    { "key": "notes",          "label": "Notes",            "type": "text",    "displayOrder": 3 },
    { "key": "safetyCheck",    "label": "Safety Check",     "type": "boolean", "displayOrder": 4 }
  ]
}
```

### 6.2 Field Renderer Map

On the frontend, define a `FIELD_RENDERERS` lookup keyed by `FieldDescriptor.type`. Each entry is a pure function `(value: unknown, descriptor: FieldDescriptor) => ReactNode`.

```ts
// src/components/diary/fieldRenderers.tsx

export const FIELD_RENDERERS: Record<string, FieldRenderer> = {
  text:    (v) => <span className="text-gray-800">{String(v ?? '—')}</span>,
  number:  (v) => <span className="tabular-nums">{v != null ? String(v) : '—'}</span>,
  boolean: (v) => <span>{v == null ? '—' : v ? 'Yes' : 'No'}</span>,
  date:    (v) => <span>{v ? new Date(String(v)).toLocaleDateString() : '—'}</span>,
  select:  (v, d) => {
    const opt = d.options?.find(o => o.value === v);
    return <span>{opt?.label ?? String(v ?? '—')}</span>;
  },
};

// Fallback for unknown future types:
export function renderField(descriptor: FieldDescriptor, payload: Record<string, unknown>): ReactNode {
  const value = payload[descriptor.key] ?? null;
  const renderer = FIELD_RENDERERS[descriptor.type] ?? ((v) => <span>{String(v ?? '—')}</span>);
  return renderer(value, descriptor);
}
```

### 6.3 DiaryCard Field Rendering Loop

The `DiaryLogCard` component sorts the snapshot by `displayOrder` and maps each descriptor through `renderField`:

```tsx
// src/components/diary/DiaryLogCard.tsx (read-only fields section)

const sortedFields = [...entry.templateSnapshot].sort((a, b) => a.displayOrder - b.displayOrder);

return (
  <dl className="divide-y divide-gray-100">
    {sortedFields.map((descriptor) => (
      <div key={descriptor.key} className="grid grid-cols-3 gap-2 py-1.5 text-sm">
        <dt className="font-medium text-gray-500">{descriptor.label}</dt>
        <dd className="col-span-2">{renderField(descriptor, entry.payload)}</dd>
      </div>
    ))}
  </dl>
);
```

### 6.4 Field Override Handling

When the user adds or removes optional fields in the create form (`useFieldArray`), the submitted `payload` will contain only the keys that were present. Because the **template snapshot already lists every possible field for the session**, absent keys naturally resolve to `null` → rendered as `—`. No special "override metadata" object is required.

If the user *added* a field that lives outside the base template (an ad-hoc override), that field will be present in `payload` but **absent from the snapshot**. The backend should append these ad-hoc descriptors to the snapshot at save time (type inferred as `text` if unknown) so the card can render them under an *"Additional Fields"* sub-heading.

### 6.5 Inline Image Rendering

Attachments are stored as a flat array on the entry. The card separates them by MIME type:

```ts
const images = entry.attachments.filter(a => a.mimeType.startsWith('image/'));
const docs   = entry.attachments.filter(a => !a.mimeType.startsWith('image/'));
```

Images are rendered into a responsive CSS grid (`grid-cols-2` or `grid-cols-3` depending on count). Images are capped at `max-h-48` to keep cards scannable. If `images.length > 3`, the 4th cell renders a `+N` overlay that opens a lightbox:

```tsx
{images.slice(0, 3).map(img => (
  <button key={img.id} onClick={() => openLightbox(img.id)} className="rounded overflow-hidden">
    <img src={img.thumbnailUrl} alt={img.fileName}
         className="w-full max-h-48 object-cover" loading="lazy" />
  </button>
))}
{images.length > 3 && (
  <button onClick={() => openLightbox(images[3].id)}
          className="relative rounded overflow-hidden">
    <img src={images[3].thumbnailUrl} className="w-full max-h-48 object-cover opacity-40" />
    <span className="absolute inset-0 flex items-center justify-center text-xl font-bold text-white">
      +{images.length - 3}
    </span>
  </button>
)}
```

Use `loading="lazy"` on all inline images to avoid blocking the initial render of cards further up the timeline.

### 6.6 Graceful Degradation Summary

| Scenario | Behaviour |
|---|---|
| Field in snapshot, value in payload | Rendered with typed formatter |
| Field in snapshot, value **missing** from payload | Rendered as `—` |
| Field in payload, **not in snapshot** (ad-hoc override) | Rendered under *Additional Fields* heading (type `text`) |
| Unknown `type` in snapshot | Falls back to plain string renderer |
| Image attachment | Shown **inline** in card, `loading="lazy"` |
| Non-image attachment | Rendered as downloadable chip below images |
| > 3 images | 3 shown inline; remainder behind `+N` lightbox trigger |

---

## 7. Next Steps

Please review this revised design plan. Once approved, the UI component implementation and testing (`vitest` + `testing-library`) tasks will be broken down and executed.