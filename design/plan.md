# Site Diary — Design Plan

> **Scope:** Phase 1 — Issue #2: Diary CRUD API + Attachments (POC)
> **Architecture:** Vertical Slice (feature-first) within Clean Architecture
> **Methodology:** TDD — tests written first, then implementation

---

## 1. Problem Statement

Issue #2 defines the API contracts the React frontend client needs to interact with the
ASP.NET Core backend for Diary operations and file Attachments.

**Key constraints (POC phase):**
- No authentication — `X-User-Id: (int)` custom header acts as the user identity
- Routes are site-scoped: `api/sites/{siteId}/diaries`
- Server derives `Id`, `CreatedAt`, `UpdatedAt`, `AuthorUserId` (from header) — these are
  **not** accepted from the client body
- Soft-delete only (no hard deletes)
- Ownership check: `403 Forbidden` if `X-User-Id` ≠ `Diary.AuthorUserId` on PUT/DELETE

---

## 2. Architectural Decision: Vertical Slice

The current codebase uses a **horizontal-slice** (layer-first) layout:

```
Application/
  DTOs/         ← all DTOs mixed together
  Interfaces/   ← all interfaces mixed together
  Services/     ← all services mixed together
Web/
  Controllers/Api/  ← all controllers mixed together
```

For Issue #2 and beyond, we move to **vertical slice (feature-first)** within each project
layer. Each feature owns its DTOs, interface, service, and controller:

```
Application/
  Features/
    Diaries/          ← everything Diary-related
    Attachments/      ← everything Attachment-related
Web/
  Features/
    Diaries/          ← DiariesController
    Attachments/      ← AttachmentsController
```

The **Domain** layer remains entity-centric (entities are cross-cutting concerns).
The **Infrastructure** layer remains shared (EF Core, repositories, storage).

---

## 3. Current State vs. Required State

| Concern | Current | Required |
|---|---|---|
| Diary list route | `GET /api/diaries` | `GET /api/sites/{siteId}/diaries` |
| Diary detail route | `GET /api/diaries/{id}` | `GET /api/sites/{siteId}/diaries/{diaryId}` (includes attachments) |
| Create diary route | `POST /api/diaries` | `POST /api/sites/{siteId}/diaries` |
| Update diary route | `PUT /api/diaries/{id}` | `PUT /api/sites/{siteId}/diaries/{diaryId}` |
| Delete diary route | `DELETE /api/diaries/{id}` | `DELETE /api/sites/{siteId}/diaries/{diaryId}` |
| Attachment upload | ❌ missing | `POST /api/diaries/{diaryId}/attachments` |
| Attachment delete | ❌ missing | `DELETE /api/attachments/{attachmentId}` |
| `CreateDiaryRequest` includes `ConstructionSiteId`, `AuthorUserId` in body | Yes (wrong) | No — from route + header |
| Ownership check (403) | ❌ missing | Required for PUT/DELETE |
| `DiaryDto` includes `IsArchived`, `DiaryTemplateId`, `CreatedAt`, `UpdatedAt` | Yes | No — server-managed fields excluded |
| `IRepository<T>.Query()` (returns `IQueryable<T>`) | ❌ missing | Required — service applies site/archive filters inline |
| `IAttachmentService` | ❌ missing | Required |

---

## 4. API Contract (Source of Truth)

### 4.1 Diary Endpoints

#### `POST /api/sites/{siteId}/diaries`
- **Header:** `X-User-Id: (int)`
- **Request body** `CreateDiaryDto`:
  ```json
  {
    "title": "Foundation Inspection",
    "content": "Poured concrete for the north wing.",
    "date": "2026-05-16T10:00:00Z",
    "isPublished": false
  }
  ```
- **201 Created** → `DiaryDto`:
  ```json
  {
    "id": 1,
    "constructionSiteId": 100,
    "authorUserId": 42,
    "title": "Foundation Inspection",
    "content": "Poured concrete for the north wing.",
    "date": "2026-05-16T10:00:00Z",
    "isPublished": false
  }
  ```

#### `GET /api/sites/{siteId}/diaries`
- **200 OK** → `DiaryDto[]`

#### `GET /api/sites/{siteId}/diaries/{diaryId}`
- **200 OK** → `DiaryDetailDto`:
  ```json
  {
    "id": 1,
    "constructionSiteId": 100,
    "authorUserId": 42,
    "title": "Foundation Inspection",
    "content": "Poured concrete.",
    "date": "2026-05-16T10:00:00Z",
    "isPublished": false,
    "attachments": []
  }
  ```

#### `PUT /api/sites/{siteId}/diaries/{diaryId}`
- **Header:** `X-User-Id: (int)`
- **Request body** `UpdateDiaryDto`:
  ```json
  {
    "title": "Foundation Inspection - Updated",
    "content": "Found minor cracking, monitoring.",
    "date": "2026-05-16T10:00:00Z"
  }
  ```
- **200 OK** → updated `DiaryDto`
- **403 Forbidden** if `X-User-Id` ≠ `Diary.AuthorUserId`
- **404 Not Found** if diary not found or is archived

#### `DELETE /api/sites/{siteId}/diaries/{diaryId}`
- **Header:** `X-User-Id: (int)`
- **204 No Content** (soft-delete: sets `IsArchived = true`)
- **403 Forbidden** if `X-User-Id` ≠ `Diary.AuthorUserId`

### 4.2 Attachment Endpoints

#### `POST /api/diaries/{diaryId}/attachments`
- **Header:** `X-User-Id: (int)`
- **Request:** `multipart/form-data` (field: `file`)
- **201 Created** → `AttachmentDto`:
  ```json
  {
    "id": 10,
    "diaryId": 1,
    "fileName": "site_photo_1.jpg",
    "fileUrl": "/uploads/site_photo_1.jpg",
    "contentType": "image/jpeg"
  }
  ```
- **404 Not Found** if `diaryId` doesn't exist

#### `DELETE /api/attachments/{attachmentId}`
- **Header:** `X-User-Id: (int)`
- **204 No Content**
- **403 Forbidden** if `X-User-Id` ≠ `Attachment.UploadedByUserId`

---

## 5. Abstractions & Data Contracts

### 5.1 Shared Result Type

A lightweight `OperationResult<T>` is introduced to allow services to communicate
`Success`, `NotFound`, or `Forbidden` outcomes without exceptions:

```csharp
// Application/Shared/OperationResult.cs
public enum OperationStatus { Success, NotFound, Forbidden }

public record OperationResult<T>(OperationStatus Status, T? Value = default)
{
    public static OperationResult<T> Ok(T value) => new(OperationStatus.Success, value);
    public static OperationResult<T> NotFound() => new(OperationStatus.NotFound);
    public static OperationResult<T> Forbidden() => new(OperationStatus.Forbidden);
}
```

Controllers map this to HTTP responses:
- `Success` → `200 OK` / `201 Created` / `204 No Content`
- `NotFound` → `404 Not Found`
- `Forbidden` → `403 Forbidden`

### 5.2 DTOs — `Application/Features/Diaries/`

```csharp
// Lean list/mutation response — server-managed fields excluded
public record DiaryDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished);

// Detail response — adds attachment list
public record DiaryDetailDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished,
    IReadOnlyList<AttachmentDto> Attachments);

// Input: POST body — no server-managed fields
public record CreateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished = false);

// Input: PUT body
public record UpdateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date);
```

> **Note on `Date` type:** The issue sends `"2026-05-16T10:00:00Z"` (ISO 8601 datetime
> string). The domain model uses `DateOnly`. The DTO will use `DateTimeOffset` for
> JSON-friendly serialization; the service will strip the time component when persisting.

### 5.3 DTOs — `Application/Features/Attachments/`

```csharp
// Lean response — only fields the frontend needs
public record AttachmentDto(
    int Id,
    int DiaryId,
    string FileName,
    string FileUrl,
    string ContentType);
```

### 5.4 Updated Generic Repository Interface — `Domain/Interfaces/IRepository.cs`

Rather than introducing a dedicated `IDiaryRepository`, the generic `IRepository<T>` is
extended with a single `Query()` method that returns `IQueryable<T>`. The service layer
then applies all site-scoping, soft-delete exclusion, and eager-loading inline, keeping
the domain interface minimal and eliminating a separate concrete repository class:

```csharp
public interface IRepository<T> where T : class
{
    IQueryable<T> Query();   // ← NEW: allows service-level LINQ composition
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
```

`IUnitOfWork.Diaries` remains typed as `IRepository<Diary>` — **no change required**.

The `DiaryService` composes queries directly:

```csharp
// List — site-scoped, excludes archived
var diaries = await _uow.Diaries.Query()
    .Where(d => d.ConstructionSiteId == siteId && !d.IsArchived)
    .ToListAsync(ct);

// Detail — eager-loads Attachments
var diary = await _uow.Diaries.Query()
    .Include(d => d.Attachments)
    .FirstOrDefaultAsync(d => d.Id == diaryId && !d.IsArchived, ct);
```

> **Testability:** Unit test mocks return `List<Diary> { … }.AsQueryable()` from `Query()`.
> No in-memory EF setup is required for service-layer tests.

### 5.5 Service Interfaces

```csharp
// Application/Features/Diaries/IDiaryService.cs
public interface IDiaryService
{
    Task<IReadOnlyList<DiaryDto>> GetBySiteIdAsync(int siteId, CancellationToken ct = default);
    Task<DiaryDetailDto?> GetByIdWithAttachmentsAsync(int siteId, int diaryId, CancellationToken ct = default);
    Task<DiaryDto> CreateAsync(int siteId, int authorUserId, CreateDiaryDto dto, CancellationToken ct = default);
    Task<OperationResult<DiaryDto>> UpdateAsync(int siteId, int diaryId, int requestingUserId, UpdateDiaryDto dto, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int siteId, int diaryId, int requestingUserId, CancellationToken ct = default);
}

// Application/Features/Attachments/IAttachmentService.cs
public interface IAttachmentService
{
    Task<OperationResult<AttachmentDto>> UploadAsync(int diaryId, int uploadedByUserId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int attachmentId, int requestingUserId, CancellationToken ct = default);
}
```

---

## 6. File & Folder Structure

### Backend

```
src/
├── SiteDiary.Domain/
│   ├── Common/
│   │   └── BaseEntity.cs             (unchanged)
│   ├── Entities/                     (all unchanged)
│   └── Interfaces/
│       ├── IRepository.cs            ← MODIFIED: adds IQueryable<T> Query() method
│       ├── IUnitOfWork.cs            (unchanged — Diaries stays IRepository<Diary>)
│       ├── IStorageService.cs        (unchanged)
│       └── IAuditService.cs          (unchanged)
│
├── SiteDiary.Application/
│   ├── Shared/
│   │   └── OperationResult.cs        ← NEW: shared result type
│   └── Features/                     ← NEW feature-first directory
│       ├── Diaries/
│       │   ├── DiaryDto.cs
│       │   ├── DiaryDetailDto.cs
│       │   ├── CreateDiaryDto.cs
│       │   ├── UpdateDiaryDto.cs
│       │   ├── IDiaryService.cs
│       │   └── DiaryService.cs
│       └── Attachments/
│           ├── AttachmentDto.cs
│           ├── IAttachmentService.cs
│           └── AttachmentService.cs
│
├── SiteDiary.Infrastructure/
│   ├── Data/
│   │   └── ApplicationDbContext.cs   (unchanged)
│   └── Repositories/
│       ├── Repository.cs             ← MODIFIED: implements Query() via DbContext.Set<T>()
│       └── UnitOfWork.cs             (unchanged)
│
└── SiteDiary.Web/
    ├── Features/                     ← NEW feature-first directory
    │   ├── Diaries/
    │   │   └── DiariesController.cs  ← replaces Controllers/Api/DiariesController.cs
    │   └── Attachments/
    │       └── AttachmentsController.cs  ← NEW
    └── Program.cs                    ← MODIFIED: register new services
```

> **Migration note:** The existing `Controllers/Api/DiariesController.cs`,
> `Application/Interfaces/IDiaryService.cs`, `Application/Services/DiaryService.cs`,
> and `Application/DTOs/DiaryDtos.cs` are **superseded** by the new feature slices.
> They will be removed as part of this phase to avoid route conflicts.
> The existing `AttachmentDtos.cs` and `AttachmentDto` is replaced by the lean feature version.
> No `IDiaryRepository` or `DiaryRepository` files are created — the generic `Repository<T>`
> gains only the `Query()` method.

### Frontend (for `mobile-ui` agent review)

```
frontend/src/
├── api/
│   ├── client.ts         (unchanged — but X-User-Id header injection needed)
│   ├── diaries.ts        ← UPDATED: site-scoped routes, trimmed types
│   ├── attachments.ts    ← NEW: upload + delete
│   └── types.ts          ← UPDATED: DiaryDto, DiaryDetailDto, AttachmentDto aligned to new contracts
├── features/
│   ├── diaries/
│   │   ├── DiaryList.tsx       ← list for a given site
│   │   ├── DiaryDetail.tsx     ← detail view with attachment list
│   │   ├── DiaryForm.tsx       ← create / edit form
│   │   └── useDiaries.ts       ← custom hooks
│   └── attachments/
│       ├── AttachmentList.tsx  ← read-only list rendered inside DiaryDetail
│       ├── AttachmentUploader.tsx ← file input + upload action
│       └── useAttachments.ts
```

> **Note for `mobile-ui` agent:** The frontend should inject `X-User-Id` as a global
> Axios request interceptor on `client.ts` (e.g., reading from `localStorage` or a
> context store). The exact UX for user-selection in this POC phase (e.g., a simple
> dropdown or hardcoded dev ID) is left to the UI design review.

---

## 7. TDD Implementation Order

All tests are written **red** first, then the implementation is added to make them green.

### Phase 1A — Domain & Shared Contracts

| # | Artifact | Test scope |
|---|---|---|
| 1 | `IRepository<T>.Query()` | (contract only — no test; implementation verified via service tests) |
| 2 | `OperationResult<T>` | Unit: `Ok()`, `NotFound()`, `Forbidden()` factory methods |

### Phase 1B — Diary Feature (Application)

| # | Test | Implementation |
|---|---|---|
| 3 | `DiaryService_GetBySiteId_ReturnsOnlyNonArchivedDiariesForThatSite` | `DiaryService.GetBySiteIdAsync` |
| 4 | `DiaryService_GetByIdWithAttachments_ReturnsDiaryDetailDto_WithAttachments` | `DiaryService.GetByIdWithAttachmentsAsync` |
| 5 | `DiaryService_GetByIdWithAttachments_WhenArchivedOrNotFound_ReturnsNull` | same |
| 6 | `DiaryService_Create_SetsAuthorUserId_FromParameter_NotFromBody` | `DiaryService.CreateAsync` |
| 7 | `DiaryService_Create_SetsSiteIdFromRouteParameter` | same |
| 8 | `DiaryService_Update_WhenOwner_UpdatesAndReturnsSuccess` | `DiaryService.UpdateAsync` |
| 9 | `DiaryService_Update_WhenNotOwner_ReturnsForbidden` | same |
| 10 | `DiaryService_Update_WhenNotFound_ReturnsNotFound` | same |
| 11 | `DiaryService_Delete_WhenOwner_SoftDeletesAndReturnsSuccess` | `DiaryService.DeleteAsync` |
| 12 | `DiaryService_Delete_WhenNotOwner_ReturnsForbidden` | same |
| 13 | `DiaryService_Delete_WhenNotFound_ReturnsNotFound` | same |

### Phase 1C — Attachment Feature (Application)

| # | Test | Implementation |
|---|---|---|
| 14 | `AttachmentService_Upload_WhenDiaryExists_CreatesAttachmentAndReturnsDto` | `AttachmentService.UploadAsync` |
| 15 | `AttachmentService_Upload_WhenDiaryNotFound_ReturnsNotFound` | same |
| 16 | `AttachmentService_Delete_WhenOwner_DeletesFromStorageAndDb` | `AttachmentService.DeleteAsync` |
| 17 | `AttachmentService_Delete_WhenNotOwner_ReturnsForbidden` | same |
| 18 | `AttachmentService_Delete_WhenNotFound_ReturnsNotFound` | same |

### Phase 1D — Infrastructure

| # | Artifact | Test scope |
|---|---|---|
| 19 | `Repository<T>.Query()` | Integration: EF Core returns correct `IQueryable` against real DB (one smoke test via `DiaryService` integration test covers this implicitly) |

> **Note:** Dedicated `DiaryRepository` integration tests are no longer needed — the
> filtering logic now lives in `DiaryService` and is covered by service unit tests
> (rows 3–5). The remaining infrastructure concern — that `Query()` correctly surfaces
> EF Core's `DbSet<T>` — is verified by the controller integration tests in Phase 1E.

### Phase 1E — Web (Controller)

| # | Test (Integration via `WebApplicationFactory`) | Expected |
|---|---|---|
| 21 | `POST /api/sites/1/diaries` (valid, with X-User-Id) | 201 + `DiaryDto` |
| 22 | `GET /api/sites/1/diaries` | 200 + array of `DiaryDto` |
| 23 | `GET /api/sites/1/diaries/1` | 200 + `DiaryDetailDto` |
| 24 | `PUT /api/sites/1/diaries/1` (correct X-User-Id) | 200 + updated `DiaryDto` |
| 25 | `PUT /api/sites/1/diaries/1` (wrong X-User-Id) | 403 |
| 26 | `DELETE /api/sites/1/diaries/1` (correct X-User-Id) | 204 |
| 27 | `DELETE /api/sites/1/diaries/1` (wrong X-User-Id) | 403 |
| 28 | `POST /api/diaries/1/attachments` (multipart, valid) | 201 + `AttachmentDto` |
| 29 | `DELETE /api/attachments/10` (correct X-User-Id) | 204 |
| 30 | `DELETE /api/attachments/10` (wrong X-User-Id) | 403 |

### Phase 1F — Middleware (Unit)

| # | Test (`Unit/Web/Middleware/XUserIdMiddlewareTests.cs`) | Expected |
|---|---|---|
| 31 | Valid `X-User-Id: 42` header present | `HttpContext.Items[UserId]` == 42; pipeline continues |
| 32 | Header absent | `Items` does not contain `UserId` key; pipeline continues |
| 33 | Header present but non-integer (`"abc"`) | `Items` does not contain `UserId` key; pipeline continues |
| 34 | `HttpContext.GetCurrentUserId()` — key populated with 42 | returns `42` |
| 35 | `HttpContext.GetCurrentUserId()` — key absent | returns `null` |

---

## 8. Key Design Decisions & Rationale

### 8.1 `OperationResult<T>` instead of exceptions
Service methods return a discriminated result (`Success | NotFound | Forbidden`) rather
than throwing exceptions. This keeps service code free of HTTP concerns and makes
unit tests straightforward — no exception handling needed in tests.

### 8.2 `IRepository<T>.Query()` replaces a dedicated `IDiaryRepository`
Rather than a purpose-built `IDiaryRepository`, the generic repository is extended with
a single `IQueryable<T> Query()` method. The `DiaryService` applies site-scoping
(`.Where(d => d.ConstructionSiteId == siteId)`), soft-delete exclusion (`!d.IsArchived`),
and eager-loading (`.Include(d => d.Attachments)`) inline. This eliminates one interface
file, one concrete class, and two infrastructure integration tests, while keeping the
business filtering logic visible in the service — the correct layer. The accepted tradeoff
is that `IQueryable` couples the Application layer to EF Core expression tree semantics,
which is acceptable for this POC scope.

### 8.3 `Date` field — `DateTimeOffset` in DTOs, `DateOnly` in domain
The issue contract sends ISO 8601 datetimes (`"2026-05-16T10:00:00Z"`). The domain
entity stores `DateOnly`. The service layer converts between the two, keeping the domain
model clean and the JSON contract correct.

### 8.4 `X-User-Id` header — extracted by middleware, consumed via `HttpContext.Items`
A dedicated `XUserIdMiddleware` (see Section 5.6) parses the header once and stores the
result in `HttpContext.Items[HttpContextKeys.UserId]`. Controllers access it through an
`HttpContext.GetCurrentUserId()` extension method that returns `int?`. The parsed `int`
is still passed to service methods as a plain parameter, so service interfaces remain
free of HTTP types and are unit-testable without HTTP infrastructure.

This replaces the duplicated `TryGetUserId()` private method that previously lived
independently in `DiariesController` and `AttachmentsController`.

### 8.5 Ownership check in service, not controller
The ownership check (`requestingUserId == diary.AuthorUserId`) lives in the service, not
the controller. This is the correct separation of concerns and enables direct unit testing
of the auth logic without spinning up HTTP infrastructure.

### 8.6 Attachment upload — delegates to `IStorageService`
`AttachmentService` calls the existing `IStorageService.UploadAsync` to store the file
and records the returned `fileUrl` in the `Attachment` entity. The service is unaware of
the storage back-end (local filesystem in dev, cloud blob in production).

### 8.8 Middleware does not short-circuit on missing/invalid header
The middleware calls `next()` unconditionally — it never returns a 400 on its own.
Reasoning: not every endpoint requires the header (`GET /diaries`, `GET /sites` work
without it). Enforcement stays in each controller action that needs an authenticated
user (`if (HttpContext.GetCurrentUserId() is not { } userId) return BadRequest(...)`),
which keeps routing / response-code ownership at the controller layer.

### 8.9 Swagger/OpenAPI — `X-User-Id` documented globally
A Swagger `OperationFilter` (or `SecurityRequirement` header parameter) will document
`X-User-Id` on all relevant operations so the OpenAPI spec accurately reflects the
contracts.

---

## 5.6 X-User-Id Middleware — `Web/Middleware/`

### Problem
`DiariesController` and `AttachmentsController` each contain an identical private
`TryGetUserId(out int userId)` method that reads the raw `X-User-Id` request header.
This is duplicated logic that will spread to every future controller that needs the
caller identity.

### Decision — `HttpContext.Items` (not `ClaimsPrincipal`)

| Option | Verdict |
|---|---|
| `ClaimsPrincipal` with a custom claim | ❌ Over-engineered for a POC header; requires a full auth middleware chain (`UseAuthentication`/`UseAuthorization`) |
| `HttpContext.Items[string key]` | ✅ Simple, no auth setup, easily seeded in unit tests |
| `HttpContext.Items[object key]` (typed constant) | ✅ Same simplicity, eliminates magic-string risk — chosen approach |

### New Files

```
src/SiteDiary.Web/
  Middleware/
    HttpContextKeys.cs          ← static class with typed key constant
    XUserIdMiddleware.cs        ← middleware: parse header → Items
    HttpContextExtensions.cs   ← GetCurrentUserId() → int?
```

#### `HttpContextKeys.cs`
```csharp
namespace SiteDiary.Web.Middleware;

public static class HttpContextKeys
{
    // Typed object key — avoids magic strings, prevents accidental collision
    public static readonly object UserId = new();
}
```

#### `XUserIdMiddleware.cs`
```csharp
namespace SiteDiary.Web.Middleware;

public sealed class XUserIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-User-Id";

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var raw)
            && int.TryParse(raw, out var userId))
        {
            context.Items[HttpContextKeys.UserId] = userId;
        }
        // Always continue — enforcement is per-action, not global
        return next(context);
    }
}
```

#### `HttpContextExtensions.cs`
```csharp
namespace SiteDiary.Web.Middleware;

public static class HttpContextExtensions
{
    /// Returns the parsed X-User-Id, or null if the header was absent/invalid.
    public static int? GetCurrentUserId(this HttpContext context)
        => context.Items.TryGetValue(HttpContextKeys.UserId, out var val)
               ? (int?)val
               : null;
}
```

### Registration (`Program.cs`)
```csharp
// ── Middleware ────────────────────────────────────────────────────────────────
app.UseMiddleware<XUserIdMiddleware>();  // before MapControllers
app.MapControllers();
```

### Controller Changes
Remove the duplicated `TryGetUserId` private method from `DiariesController` and
`AttachmentsController`. Replace with:

```csharp
// Before (duplicated in each controller):
private bool TryGetUserId(out int userId) { ... }
if (!TryGetUserId(out var userId))
    return BadRequest("X-User-Id header is required...");

// After (one-liner, same 400 contract maintained):
if (HttpContext.GetCurrentUserId() is not { } userId)
    return BadRequest("X-User-Id header is required and must be a valid integer.");
```

### Testability
- **Middleware unit tests** (no HTTP stack): construct a `DefaultHttpContext`, populate
  `Request.Headers["X-User-Id"]`, call `InvokeAsync`, assert `Items[HttpContextKeys.UserId]`.
- **Controller unit tests**: seed `HttpContext.Items[HttpContextKeys.UserId] = 42` directly —
  no need to set headers or run the middleware.
- **Integration tests** (WebApplicationFactory): continue to pass the real header in
  `HttpClient` requests — the middleware handles parsing transparently.

---

## 9. Out of Scope (Phase 1)

- Authentication / JWT tokens
- Diary Templates (`DiaryTemplateId`)
- Pagination / filtering on list endpoints
- File size / type validation on attachments (future hardening)
- User/Role management endpoints

---

## 10. Acceptance Criteria Checklist

- [ ] All DTOs precisely match the JSON contracts in Issue #2
- [ ] `X-User-Id` header is used in controllers for `AuthorUserId` on create and ownership
      check on PUT/DELETE
- [ ] Server-owned fields (`Id`, `CreatedAt`, `UpdatedAt`) are absent from all input DTOs
- [ ] Soft-delete (`IsArchived = true`) is used for diary DELETE; record remains in DB
- [ ] `GET /api/sites/{siteId}/diaries/{diaryId}` response includes `attachments` array
- [ ] `POST /api/diaries/{diaryId}/attachments` accepts `multipart/form-data`
- [ ] All 35 TDD tests pass (29 original + 5 middleware unit tests)
- [ ] `TryGetUserId` private method removed from `DiariesController` and `AttachmentsController`
- [ ] `XUserIdMiddleware` registered before `MapControllers` in `Program.cs`
- [ ] Swagger/OpenAPI UI accurately shows exact payload schemas including `X-User-Id` header
- [ ] Existing `api/diaries` routes are removed to avoid conflicts
- [ ] `mobile-ui` agent has reviewed and signed off on frontend API client contracts

---

## 11. Agents & Handoffs

| Agent | Responsibility | Input |
|---|---|---|
| **architect** (this doc) | Design plan, abstractions, API contracts | Issue #2, codebase review |
| **mobile-ui** | Review & design frontend feature components (`DiaryList`, `DiaryDetail`, `DiaryForm`, `AttachmentUploader`); confirm `X-User-Id` injection strategy; align with existing Tailwind/Vite layout | This `design/plan.md` |
| **developer** | Write failing tests (Phase 1A–1E), then implementations | This `design/plan.md` (after LGTB approval) |

---

*Plan authored: 2026-05-16 | GitHub Issue: yhua045/site-diary#2*
