# Audit Log Page — Design Plan

**Feature:** Server-rendered MVC page listing audit log entries, oldest → newest  
**Date:** 17 May 2026  
**Reviewed with:** `mobile-ui` agent (UI component alignment — updated: Tailwind CSS replaces Bootstrap)

---

## 1. Problem Statement

`AuditSaveChangesInterceptor` currently emits structured audit events only to `ILogger`. The `AuditHistory` entity and `AuditService` both exist and the `AuditHistories` DbSet is mapped in `ApplicationDbContext`, but the interceptor never persists rows to the database. As a result there is nothing to query for a display page.

This design resolves the gap in two steps:

1. **Persist**: modify the interceptor to write `AuditHistory` rows directly into the same EF `SaveChanges` transaction.
2. **Display**: expose a server-rendered ASP.NET Core MVC page that queries and renders those rows ordered oldest → newest, with paging.

---

## 2. Current Architecture — Gap Analysis

| Component | Location | Current state |
|---|---|---|
| `AuditHistory` entity | `Domain/Entities/AuditHistory.cs` | Complete — has all required fields |
| `ApplicationDbContext.AuditHistories` | `Infrastructure/Data/ApplicationDbContext.cs` | Mapped, no query filter (correct — audits are never soft-deleted) |
| `IUnitOfWork.AuditHistories` | `Domain/Interfaces/IUnitOfWork.cs` | Exposed, repository available |
| `AuditService` | `Infrastructure/Services/AuditService.cs` | Writes to DB via UoW — but never called by interceptor |
| `AuditSaveChangesInterceptor` | `Infrastructure/Interceptors/` | Only calls `ILogger` — **gap** |
| MVC views | `Web/` | None exist yet — `Program.cs` calls `AddControllers()` only |

---

## 3. Chosen Approach — Direct Context Write in Interceptor

**Why not call `AuditService.RecordAsync()`?**  
`AuditService` calls `uow.SaveChangesAsync()`, which would recursively invoke the interceptor, causing an infinite loop.

**Chosen solution**: During `SavingChanges`/`SavingChangesAsync`, build `AuditHistory` instances and call `context.Set<AuditHistory>().Add(entry)` directly on the same `DbContext` that is mid-save. EF Core includes entities added to the context during a `SavingChanges` interceptor in the same database commit — no second `SaveChanges` call is needed and no loop occurs.

**Self-auditing guard**: the interceptor loop must skip any entry whose entity type is `AuditHistory` to prevent auditing audit records.

The `ILogger` calls are **retained** alongside the DB write (useful for real-time log streaming and diagnostics).

---

## 4. Layers Changed / Added

### 4.1 Infrastructure — `AuditSaveChangesInterceptor` (modified)

**File:** `src/SiteDiary.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`

**Change summary:**
- Add `ApplicationDbContext` constructor parameter (resolved via DI; already scoped).
- In `LogAuditEntries`, after the existing `logger.LogInformation(...)` call, also call `context.Set<AuditHistory>().Add(auditRow)`.
- Filter out `AuditHistory` entities from the entries loop to prevent recursion.
- Build `ChangedByUserId` by parsing `currentUserService.AuthenticatedUserId` (fall back to `0` / a sentinel user if anonymous, so the FK is satisfied).
- Serialize change diffs to JSON for `AuditHistory.Changes` (mirrors `AuditService` serialisation pattern).

**Dependency note:** The interceptor is already `AddScoped<AuditSaveChangesInterceptor>()` and already owns a scoped lifetime. No circular registration issues.

```
Constructor signature (after change):
AuditSaveChangesInterceptor(
    ICurrentUserService currentUserService,
    ILogger<AuditSaveChangesInterceptor> logger,
    ApplicationDbContext db)           // ← new
```

> **Design note:** Injecting `ApplicationDbContext` directly into the interceptor is safe because EF Core resolves interceptors from the service provider at `AddDbContext` time; the same DbContext instance is passed via `eventData.Context` and via DI. Prefer using `eventData.Context` cast to `ApplicationDbContext` rather than the injected field to avoid any mismatch in multi-context scenarios.

---

### 4.2 Application — New `IAuditLogService` + `AuditLogDto`

**Files:**
- `src/SiteDiary.Application/Features/AuditLogs/AuditLogDto.cs`
- `src/SiteDiary.Application/Features/AuditLogs/IAuditLogService.cs`
- `src/SiteDiary.Application/Features/AuditLogs/AuditLogService.cs`

#### `AuditLogDto`

```csharp
public sealed record AuditLogDto(
    int    Id,
    string EntityName,
    int    EntityId,
    string Action,           // "Insert" | "Update" | "Delete"
    int    ChangedByUserId,
    string ChangedByUserName, // "{FirstName} {LastName}" via navigation
    string? Changes,          // JSON diff; null for Insert/Delete
    DateTime Timestamp);
```

#### `IAuditLogService`

```csharp
public interface IAuditLogService
{
    /// <summary>
    /// Returns a page of audit log entries ordered oldest → newest (ascending Timestamp).
    /// </summary>
    Task<AuditLogPageDto> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
}

public sealed record AuditLogPageDto(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

#### `AuditLogService` (implementation)

Query strategy:
```
uow.AuditHistories
   .Query()                            // IQueryable<AuditHistory>
   .Include(a => a.ChangedBy)          // eager-load User for display name
   .OrderBy(a => a.Timestamp)          // oldest → newest
   .Skip((page - 1) * pageSize)
   .Take(pageSize)
```

The total count is retrieved via a separate `CountAsync()` call on the unsliced query to populate `TotalCount` for pagination metadata.

---

### 4.3 Web — MVC Controller + Razor View

#### 4.3.1 `Program.cs` changes

Replace:
```csharp
builder.Services.AddControllers();
```
With:
```csharp
builder.Services.AddControllers();          // keep — used by API controllers
builder.Services.AddControllersWithViews(); // add — enables Razor view engine
```

Register service:
```csharp
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
```

Add conventional MVC route (alongside existing attribute routing):
```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLogs}/{action=Index}/{id?}");
```

#### 4.3.2 `AuditLogsController`

**File:** `src/SiteDiary.Web/Features/AuditLogs/AuditLogsController.cs`

```
Route:   GET /AuditLogs
         GET /AuditLogs?page=2&pageSize=50
Controller type: Controller (not ControllerBase — needs View() support)
```

```csharp
public class AuditLogsController(IAuditLogService auditLogService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var result = await auditLogService.GetPageAsync(page, pageSize, ct);
        return View(result);
    }
}
```

Default `pageSize` of **50** is chosen to keep the page readable on a single screen while avoiding excessive DB load.

#### 4.3.3 View Model

The `AuditLogPageDto` returned by the service is passed directly as the view model (no additional wrapper needed at this stage).

#### 4.3.4 Razor View

**File:** `src/SiteDiary.Web/Views/AuditLogs/Index.cshtml`

**Layout alignment:**  
A minimal `_Layout.cshtml` is added at `src/SiteDiary.Web/Views/Shared/_Layout.cshtml` using **Tailwind CSS**, consistent with the React + Vite + Tailwind frontend. For the MVC Razor views, Tailwind is loaded via the [Tailwind Play CDN](https://cdn.tailwindcss.com) (`<script src="https://cdn.tailwindcss.com"></script>`) during development. For production, a dedicated `tailwindcss` CLI build step in the `Web` project generates `wwwroot/css/tailwind.min.css` from a minimal `tailwind.config.js` that scans `Views/**/*.cshtml`. This approach keeps the MVC and React styling vocabularies identical with no additional CDN dependencies.

**Table columns:**

| # | Timestamp (UTC) | Entity | Entity ID | Action | Changed By | Changes |
|---|---|---|---|---|---|---|

- **Timestamp** formatted as `yyyy-MM-dd HH:mm:ss UTC` — unambiguous for an audit log.
- **Action** rendered with a colour-coded pill badge using Tailwind utility classes:
  - `Insert` → `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800`
  - `Update` → `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800`
  - `Delete` → `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800`
  - A Razor partial `_ActionBadge.cshtml` encapsulates the conditional class logic to keep `Index.cshtml` clean.
- **Changes** column shows a collapsible `<details><summary>View diff</summary>…</details>` element rendering the raw JSON. This avoids wide columns on the initial load.
- Rows use Tailwind alternating-row shading: `odd:bg-white even:bg-gray-50` on `<tr>` elements, with `divide-y divide-gray-200` on `<tbody>` for row separators.
- The table is wrapped in `<div class="overflow-x-auto shadow-sm ring-1 ring-black ring-opacity-5 rounded-lg">` for horizontal scroll on narrow viewports and a subtle card-style frame consistent with the React SPA's card metaphor.
- A simple previous / next page navigation is rendered at the bottom using `<a>` links with `?page=N` query strings, styled with Tailwind button utilities: `px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50`.

**No JavaScript required** — the page is fully server-rendered HTML.

---

### 4.4 Shared Layout (`_Layout.cshtml`)

**File:** `src/SiteDiary.Web/Views/Shared/_Layout.cshtml`

Because this application is primarily a React SPA, the Razor layout is intentionally minimal:
- **Tailwind CSS** loaded via Play CDN script (`<script src="https://cdn.tailwindcss.com"></script>`) in development; replaced by `<link rel="stylesheet" href="~/css/tailwind.min.css">` in production (built by `tailwindcss` CLI as an MSBuild `Exec` task in `SiteDiary.Web.csproj`).
- A thin top navigation bar (`<nav class="bg-white shadow-sm">`) with the application name ("Site Diary") and a link to `/AuditLogs`, using flex layout: `flex items-center justify-between h-16 px-6`.
- `@RenderBody()` inside a `<main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">` container — matches the max-width and padding rhythm used in the React layout.
- A `_ViewImports.cshtml` enabling tag helpers.

**`tailwind.config.js` (Web project root):**
```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./Views/**/*.cshtml'],
  theme: { extend: {} },
  plugins: [],
}
```

**MSBuild task in `SiteDiary.Web.csproj`** (runs before Publish):
```xml
<Target Name="BuildTailwind" BeforeTargets="Build" Condition="'$(Configuration)' == 'Release'">
  <Exec Command="npx tailwindcss -i ./Styles/site.css -o ./wwwroot/css/tailwind.min.css --minify" />
</Target>
```

This layout is **only** used by the MVC Razor views (audit log page). The React SPA continues to be served from `/` and is unaffected.

---

## 5. Data Flow (end-to-end)

```
User performs any mutation (e.g., creates a Diary)
    │
    ▼
EF Core SaveChangesAsync()
    │
    ├─► AuditSaveChangesInterceptor.SavingChangesAsync()
    │       ├─ Emit ILogger.LogInformation(...)   [retained]
    │       └─ context.Set<AuditHistory>().Add(auditRow)  [NEW]
    │
    └─► DB commit (Diary + AuditHistory rows in one transaction)

Browser navigates to GET /AuditLogs?page=1&pageSize=50
    │
    ▼
AuditLogsController.Index(page=1, pageSize=50)
    │
    ▼
AuditLogService.GetPageAsync(1, 50)
    │
    ├─ COUNT query  →  TotalCount
    └─ SELECT ... ORDER BY Timestamp ASC OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
           └─ Include User (ChangedBy)
    │
    ▼
Razor View: Index.cshtml renders HTML table
```

---

## 6. Database — No Migration Required

`AuditHistory` is already in the schema (created in `20260515201001_InitialCreate.cs`). The `ChangedBy` FK to `Users` with `int ChangedByUserId` is already defined. No new migration is needed for the display feature.

**Anonymous writes**: if the interceptor fires outside an HTTP context (e.g., seeding), `AuthenticatedUserId` will be `null`. In this case `ChangedByUserId` is set to `0` and the FK constraint will fail unless a sentinel "System" user (Id = 0) exists, or the FK is made nullable. The design opts to **make `ChangedByUserId` nullable** (`int?`) in `AuditHistory` to handle anonymous/system mutations gracefully. This requires a migration:

```
Migration name: AddAuditHistoryNullableUser
Change: AuditHistory.ChangedByUserId  int  →  int?
```

---

## 7. TDD Test Plan

Tests are written **before** implementation, per project convention.

### 7.1 Unit — `AuditSaveChangesInterceptorTests` (extend existing)

| Test | Expectation |
|---|---|
| `SavingChanges_AddsAuditHistoryRow_ForAddedEntity` | `context.Set<AuditHistory>()` contains one entry after interception |
| `SavingChanges_SkipsAuditHistoryEntities` | Intercepting an `AuditHistory` add does not create a second `AuditHistory` row (no recursion) |
| `SavingChanges_SetsNullChangedByUserId_WhenUserIsAnonymous` | `ChangedByUserId` is `null` when `ICurrentUserService.AuthenticatedUserId` is `null` |
| `SavingChanges_SerializesChangedProperties_ForModifiedEntity` | `AuditHistory.Changes` is non-null JSON for `Modified` state |

### 7.2 Unit — `AuditLogServiceTests` (new)

| Test | Expectation |
|---|---|
| `GetPageAsync_ReturnsItemsOrderedByTimestampAscending` | Items[0].Timestamp ≤ Items[1].Timestamp |
| `GetPageAsync_ReturnsCorrectPage` | Page 2, pageSize 2 skips first 2 rows |
| `GetPageAsync_ReturnsTotalCount` | `TotalCount` reflects all rows regardless of page |
| `GetPageAsync_IncludesChangedByUserName` | `ChangedByUserName` is populated from navigation property |

### 7.3 Integration — `AuditLogsControllerIntegrationTests` (new)

| Test | Expectation |
|---|---|
| `GET_AuditLogs_Returns200` | Status 200 for `/AuditLogs` |
| `GET_AuditLogs_RendersTableRows` | Response HTML contains `<td>` rows matching seeded `AuditHistory` data |
| `GET_AuditLogs_Page2_ReturnsCorrectSubset` | `/AuditLogs?page=2&pageSize=1` returns second row |

---

## 8. File Inventory

| File | Status |
|---|---|
| `src/SiteDiary.Domain/Entities/AuditHistory.cs` | Modify: `ChangedByUserId` → `int?` |
| `src/SiteDiary.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs` | Modify: add DB write, self-audit guard |
| `src/SiteDiary.Infrastructure/Data/Migrations/<timestamp>_AddAuditHistoryNullableUser.cs` | New migration |
| `src/SiteDiary.Application/Features/AuditLogs/AuditLogDto.cs` | New |
| `src/SiteDiary.Application/Features/AuditLogs/IAuditLogService.cs` | New |
| `src/SiteDiary.Application/Features/AuditLogs/AuditLogService.cs` | New |
| `src/SiteDiary.Web/Features/AuditLogs/AuditLogsController.cs` | New |
| `src/SiteDiary.Web/Views/AuditLogs/Index.cshtml` | New |
| `src/SiteDiary.Web/Views/AuditLogs/_ActionBadge.cshtml` | New — Tailwind pill badge partial |
| `src/SiteDiary.Web/Views/Shared/_Layout.cshtml` | New |
| `src/SiteDiary.Web/Views/_ViewImports.cshtml` | New |
| `src/SiteDiary.Web/Views/_ViewStart.cshtml` | New |
| `src/SiteDiary.Web/tailwind.config.js` | New — scans `Views/**/*.cshtml` |
| `src/SiteDiary.Web/Styles/site.css` | New — Tailwind directives entry point (`@tailwind base/components/utilities`) |
| `src/SiteDiary.Web/Program.cs` | Modify: add `AddControllersWithViews()`, register service, add `MapControllerRoute` |
| `src/SiteDiary.Tests/Unit/Application/AuditLogServiceTests.cs` | New |
| `src/SiteDiary.Tests/Unit/Infrastructure/AuditSaveChangesInterceptorTests.cs` | Extend |
| `src/SiteDiary.Tests/Integration/AuditLogsControllerIntegrationTests.cs` | New |

---

## 9. UI Design Alignment

The audit log page is an **admin/ops utility view**, deliberately separate from the React + Tailwind SPA used by site managers. Key alignment decisions:

- **Tailwind CSS (not Bootstrap)**: the React frontend already uses Tailwind CSS via Vite. Using the same utility framework for MVC Razor views means a single consistent design token vocabulary — no Bootstrap CDN, no class-name collisions, no dual-stylesheet maintenance. The `mobile-ui` agent reviewed and confirmed this choice.
- **Table layout over card/timeline**: the `diary-app-screens.md` design uses a card/timeline metaphor for diary entries. For audit log data (entity, action, timestamp), a compact table grid is more scannable for admin users. Tailwind's `divide-y`, `ring`, and `rounded-lg` utilities provide the same elevated-card visual framing as the React diary cards.
- **Consistent brand & spacing**: the `<title>` and nav bar read "Site Diary — Audit Log". The `max-w-7xl mx-auto` container and `px-4 sm:px-6 lg:px-8` padding rhythm match the React SPA's layout constants, creating a seamless visual handoff if both views are open side-by-side.
- **Colour coding for Action badges**: `bg-green-100 text-green-800` / `bg-amber-100 text-amber-800` / `bg-red-100 text-red-800` pill badges mirror the green/amber/red status indicators used in the existing React diary cards — same Tailwind colour palette, same semantic meaning.
- **Responsive but desktop-first**: audit logs are an admin tool consumed on desktop. The `overflow-x-auto` wrapper provides horizontal scroll on narrow viewports without the complexity of a card-per-row mobile layout. The `mobile-ui` agent confirmed this trade-off is appropriate for admin-only tooling.
- **No JavaScript required**: all interactivity (collapsible diff, pagination) is server-rendered HTML using `<details>`/`<summary>` and `<a>` links. This keeps the Razor views dependency-free and instantly loadable.

**`mobile-ui` agent review notes (17 May 2026):**
- ✅ Tailwind utility classes align with React frontend palette; no conflicting design tokens.
- ✅ `overflow-x-auto` + `ring` card wrapper is the correct Tailwind idiom for responsive tables (equivalent to Bootstrap `table-responsive` + `card`).
- ✅ Pill badge pattern (`rounded-full`, `text-xs font-medium`) is consistent with React SPA badge usage.
- ✅ `max-w-7xl` container and responsive padding match the React layout wrapper — approved for visual consistency.
- ℹ️ For future enhancements: if the audit log is ever exposed to non-admin users on mobile, replace the `<table>` with a stacked card list (`flex flex-col gap-4`) using the same Tailwind tokens.

---

## 10. Open Questions / Risks

| # | Item | Decision |
|---|---|---|
| 1 | `ChangedByUserId` nullable FK | Make nullable (see §6); requires migration |
| 2 | Should `AuditHistory` rows be excluded from the soft-delete global query filter? | Yes — they must always be visible; no `HasQueryFilter` applied (already the case) |
| 3 | Should the audit log page require authentication? | Deferred — add `[Authorize]` attribute in a follow-up once auth middleware is wired for MVC |
| 4 | Long `Changes` JSON in table cells | Solved via `<details>` collapse (see §4.3.4) |
| 5 | Infinite pagination performance | Initial design uses OFFSET/FETCH; switch to keyset pagination if > 100 k rows |

---

## 11. Handoff

**Next step:** [Start TDD] → `developer` agent  
**Prompt:** "Plan approved. Write failing tests for these requirements."  
**Reference this document:** `design/audit-log-page-design.md`
