# Site Diary — Architectural Design Plan

> **Version:** 1.0  
> **Date:** 16 May 2026  
> **Status:** Ready for TDD handoff  
> **References:** GitHub Issue #1

---

## 1. Overview

**Site Diary** is a construction-site daily diary management system. Site managers and workers can create structured diary entries, attach files, manage team membership, and audit all changes. The product targets web browsers (desktop-first, mobile-responsive) with a possible future native mobile expansion.

---

## 2. Tech Stack

| Layer | Technology |
|---|---|
| Backend framework | ASP.NET Core MVC (.NET 10) |
| Data access | Entity Framework Core 9 (code-first, migrations) |
| Database | Microsoft SQL Server (MSSQL) |
| Frontend | React 18 + Vite 5 + Tailwind CSS 3 |
| API communication | RESTful JSON API (ASP.NET Core Web API controllers) |
| Auth (future) | ASP.NET Core Identity (JWT bearer tokens) |
| Testing | xUnit + Moq + FluentAssertions (backend); Vitest + React Testing Library (frontend) |

---

## 3. Solution Structure

```
site-diary/
├── design/
│   └── plan.md                         ← this document
│
├── src/
│   ├── SiteDiary.Domain/               # Entities, value objects, domain interfaces
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   └── Common/                     # BaseEntity, soft-delete helpers
│   │
│   ├── SiteDiary.Application/          # Use-cases, DTOs, service interfaces
│   │   ├── Services/
│   │   ├── DTOs/
│   │   └── Interfaces/
│   │
│   ├── SiteDiary.Infrastructure/       # EF Core DbContext, repositories, migrations
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Repositories/
│   │   └── Services/                   # StorageService, AuditService, etc.
│   │
│   ├── SiteDiary.Web/                  # ASP.NET Core MVC host + API controllers
│   │   ├── Controllers/
│   │   │   └── Api/                    # RESTful endpoints consumed by React
│   │   ├── wwwroot/                    # Built React bundle is served from here
│   │   └── Program.cs
│   │
│   └── SiteDiary.Tests/
│       ├── Unit/                       # Domain + Application layer unit tests
│       ├── Integration/                # EF Core + repository integration tests
│       └── Api/                        # HTTP-level controller tests
│
└── frontend/                           # React + Vite + Tailwind project root
    ├── src/
    │   ├── api/                        # Typed API client (fetch/axios wrappers)
    │   ├── components/                 # Shared, reusable UI components
    │   ├── features/                   # Feature-sliced modules
    │   │   ├── sites/
    │   │   ├── diaries/
    │   │   ├── users/
    │   │   └── attachments/
    │   ├── hooks/                      # Custom React hooks
    │   ├── pages/                      # Route-level page components
    │   └── main.tsx
    ├── vite.config.ts
    └── tailwind.config.ts
```

---

## 4. Domain Model

### 4.1 Key Decisions

- **Primary key type:** All entities use `int` PKs and `int` FKs — no GUIDs — for simplicity and index performance in MSSQL.
- **Soft-delete:** Applied via `IsArchived bool` on entities where records must be retained for audit purposes. Hard-delete is only used for join/log tables where the parent carries the archive flag.
- **JSON columns:** `DiaryTemplate.Sections` and `AuditHistory.Changes` are stored as `nvarchar(max)` with EF Core's `HasColumnType("nvarchar(max)")` and serialised as JSON strings. MSSQL 2022+ JSON support can be leveraged later.
- **Email on User:** Added to satisfy the index requirement stated in the issue.

### 4.2 Entity Definitions

#### BaseEntity (abstract)
```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### ConstructionSite
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string (200) | required |
| Description | string (2000) | nullable |
| Address | string (500) | required |
| IsArchived | bool | soft-delete, default false |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### User
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| FirstName | string (100) | required |
| LastName | string (100) | required |
| Email | string (256) | required, **unique index** |
| IsActive | bool | default true |
| IsArchived | bool | soft-delete, default false |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### Role
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string (100) | required, unique |
| Description | string (500) | nullable |
| CreatedAt | DateTime | |

#### Diary
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| ConstructionSiteId | int | FK → ConstructionSite, **index** |
| AuthorUserId | int | FK → User, **index** |
| DiaryTemplateId | int? | FK → DiaryTemplate (nullable) |
| Title | string (300) | required |
| Content | string (max) | nullable |
| Date | DateOnly | **index** |
| IsPublished | bool | default false |
| IsArchived | bool | soft-delete, default false |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### SiteUser *(site–user membership join with metadata)*
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| ConstructionSiteId | int | FK → ConstructionSite, **composite index** (SiteId, UserId) |
| UserId | int | FK → User |
| AssignedRoleId | int | FK → Role |
| JoinedDate | DateOnly | |
| IsPrimaryContact | bool | default false |
| CreatedAt | DateTime | |

#### UserRole *(system-wide role assignments)*
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| UserId | int | FK → User, **index** |
| RoleId | int | FK → Role |
| AssignedAt | DateTime | |
| IsActive | bool | default true — acts as soft-delete |

#### DiaryTemplate
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string (200) | required |
| Sections | string | JSON, stored as nvarchar(max) |
| IsDefault | bool | default false |
| CreatedByUserId | int | FK → User |
| IsArchived | bool | soft-delete, default false |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### AuditHistory
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| EntityName | string (200) | **composite index** (EntityName, EntityId) |
| EntityId | int | |
| Action | string (50) | e.g. "Created", "Updated", "Deleted" |
| ChangedByUserId | int | FK → User, **index** |
| Changes | string | JSON diff, stored as nvarchar(max) |
| Timestamp | DateTime | **index** |

#### Attachment
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| DiaryId | int | FK → Diary, **index** |
| FileName | string (260) | |
| FileUrl | string (2048) | |
| ContentType | string (100) | |
| SizeBytes | long | |
| UploadedByUserId | int | FK → User |
| UploadedAt | DateTime | |
| StorageProvider | string (50) | e.g. "AzureBlobStorage", "Local" |

### 4.3 Entity Relationship Summary

```
ConstructionSite ──< SiteUser >── User
                                   │
                              UserRole >── Role
                                   │
ConstructionSite ──< Diary ──< Attachment
                      │
                DiaryTemplate
                      │
               AuditHistory (cross-cutting)
```

---

## 5. Database Indexes

| Table | Index |
|---|---|
| `Users` | `IX_Users_Email` (unique) |
| `Diaries` | `IX_Diaries_ConstructionSiteId`, `IX_Diaries_AuthorUserId`, `IX_Diaries_Date` |
| `SiteUsers` | `IX_SiteUsers_ConstructionSiteId_UserId` (composite) |
| `UserRoles` | `IX_UserRoles_UserId` |
| `AuditHistories` | `IX_AuditHistories_EntityName_EntityId` (composite), `IX_AuditHistories_ChangedByUserId`, `IX_AuditHistories_Timestamp` |
| `Attachments` | `IX_Attachments_DiaryId` |

---

## 6. API Design (RESTful)

All routes are prefixed `/api/`.

| Resource | Endpoints |
|---|---|
| Sites | `GET /sites`, `POST /sites`, `GET /sites/{id}`, `PUT /sites/{id}`, `DELETE /sites/{id}` |
| Users | `GET /users`, `POST /users`, `GET /users/{id}`, `PUT /users/{id}` |
| Roles | `GET /roles`, `POST /roles` |
| Diaries | `GET /diaries`, `POST /diaries`, `GET /diaries/{id}`, `PUT /diaries/{id}`, `DELETE /diaries/{id}` |
| Diaries – publish | `POST /diaries/{id}/publish` |
| Templates | `GET /templates`, `POST /templates`, `GET /templates/{id}`, `PUT /templates/{id}` |
| Attachments | `GET /diaries/{id}/attachments`, `POST /diaries/{id}/attachments`, `DELETE /attachments/{id}` |
| Site users | `GET /sites/{id}/users`, `POST /sites/{id}/users`, `DELETE /sites/{id}/users/{userId}` |
| Audit | `GET /audit?entityName=&entityId=` |

All mutating endpoints return `ProblemDetails` on error (RFC 7807). Soft-deletes are exposed as `DELETE` and set `IsArchived = true`.

---

## 7. Frontend Architecture

> *Reviewed and approved by `mobile-ui` agent — see Section 8 for UI/UX notes.*

### 7.1 Routing (React Router v6)

```
/                       → Dashboard (recent sites + diary summary)
/sites                  → Site list
/sites/:id              → Site detail (members, recent diaries)
/sites/:id/diaries      → Diary list for site
/sites/:id/diaries/new  → New diary (template selection)
/diaries/:id            → Diary detail (read + edit)
/diaries/:id/edit       → Diary editor
/users                  → User management
/templates              → Template management
/audit                  → Audit log viewer
```

### 7.2 Component Hierarchy

```
App
├── Layout
│   ├── Sidebar (desktop) / BottomNav (mobile ≤ 768 px)
│   ├── TopBar (breadcrumb + user avatar)
│   └── <Outlet />
│
├── Pages/
│   ├── DashboardPage
│   ├── SiteListPage / SiteDetailPage
│   ├── DiaryListPage / DiaryDetailPage / DiaryEditorPage
│   ├── UserListPage
│   ├── TemplateListPage / TemplateEditorPage
│   └── AuditLogPage
│
└── Features/
    ├── sites/   → SiteCard, SiteForm, SiteMemberTable
    ├── diaries/ → DiaryCard, DiaryForm, DiaryEditor, PublishButton, AttachmentPanel
    ├── users/   → UserTable, UserForm
    └── templates/ → TemplateSectionBuilder
```

### 7.3 Shared Component Library (Tailwind-based)

| Component | Purpose |
|---|---|
| `Button` | primary / secondary / danger variants |
| `Input`, `Textarea`, `Select` | form fields, consistent focus ring |
| `Badge` | status labels (Published, Archived, Active) |
| `Card` | content container with hover/shadow |
| `Modal` | accessible dialog (focus trap) |
| `DataTable` | sortable/paginated table |
| `FileDropZone` | drag-and-drop attachment upload |
| `Toast` | success/error notifications |
| `Spinner` | loading indicator |
| `EmptyState` | illustrated empty list placeholder |

---

## 8. UI/UX Design Notes (mobile-ui agent input)

The following design guidelines were produced in collaboration with the `mobile-ui` agent to ensure the interface is consistent, accessible, and mobile-friendly:

### 8.1 Design Tokens (Tailwind config)

```ts
// tailwind.config.ts
colors: {
  primary:   { DEFAULT: '#2563EB', dark: '#1D4ED8' },   // blue-600 / blue-700
  secondary: { DEFAULT: '#64748B' },                      // slate-500
  success:   { DEFAULT: '#16A34A' },                      // green-600
  danger:    { DEFAULT: '#DC2626' },                      // red-600
  surface:   { DEFAULT: '#F8FAFC', card: '#FFFFFF' },    // slate-50 / white
}
// Font: Inter (Google Fonts), 14 px base, 16 px touch targets
```

### 8.2 Responsive Layout Strategy

- **Desktop (≥ 1024 px):** Fixed 240 px left sidebar navigation + main content area.
- **Tablet (768–1023 px):** Collapsible sidebar (icon-only mode, expands on hover/click).
- **Mobile (< 768 px):** Fixed bottom navigation bar (max 5 items); sidebar hidden. Card-based lists replace tables. Forms stack to single column. FAB (floating action button) for primary create actions.

### 8.3 Diary Editor

- Rich-text editor: `react-quill` (lightweight) or `tiptap` (recommended for extensibility).
- Section-based layout driven by `DiaryTemplate.Sections` JSON — each section renders as a collapsible fieldset.
- Autosave (debounced 3 s) with visual indicator ("Saving…" / "Saved ✓").

### 8.4 Accessibility

- WCAG 2.1 AA minimum.
- All interactive elements keyboard-navigable.
- Colour contrast ≥ 4.5:1 for normal text.
- ARIA labels on icon-only buttons.
- Focus trap in modals.

---

## 9. TDD Strategy

### 9.1 Test Layers

| Layer | Framework | Scope |
|---|---|---|
| Domain unit tests | xUnit + FluentAssertions | Entity constructors, value validation, domain rules |
| Application unit tests | xUnit + Moq | Service methods, use-case orchestration (mock repositories) |
| Infrastructure integration tests | xUnit + EF Core In-Memory / TestContainers (MSSQL) | Repository CRUD, migration idempotency |
| API tests | xUnit + `WebApplicationFactory` | HTTP round-trips, status codes, validation errors |
| Frontend unit tests | Vitest + React Testing Library | Component rendering, user interactions |
| Frontend E2E (future) | Playwright | Critical user flows |

### 9.2 Test-First Priority Order

1. **Domain entities** — constructors, computed properties, soft-delete helpers.
2. **Repository contracts** — `IRepository<T>` interface tests against in-memory EF provider.
3. **Application services** — `SiteService`, `DiaryService`, `AttachmentService` etc.
4. **API controllers** — `SitesController`, `DiariesController`, `AttachmentsController`.
5. **Frontend components** — `DiaryCard`, `DiaryForm`, `AttachmentPanel`.

### 9.3 Key Interfaces to Define First (to enable mocking)

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task SoftDeleteAsync(int id, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    IRepository<ConstructionSite> Sites { get; }
    IRepository<User> Users { get; }
    IRepository<Diary> Diaries { get; }
    IRepository<DiaryTemplate> DiaryTemplates { get; }
    IRepository<Attachment> Attachments { get; }
    IRepository<AuditHistory> AuditHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IAuditService
{
    Task RecordAsync(string entityName, int entityId, string action,
                     int changedByUserId, object? changes, CancellationToken ct = default);
}

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}
```

---

## 10. EF Core Configuration Highlights

```csharp
// ApplicationDbContext.cs (excerpt)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Global soft-delete query filter
    modelBuilder.Entity<ConstructionSite>().HasQueryFilter(e => !e.IsArchived);
    modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsArchived);
    modelBuilder.Entity<Diary>().HasQueryFilter(e => !e.IsArchived);
    modelBuilder.Entity<DiaryTemplate>().HasQueryFilter(e => !e.IsArchived);

    // Unique + search indexes
    modelBuilder.Entity<User>()
        .HasIndex(u => u.Email).IsUnique();

    modelBuilder.Entity<Diary>()
        .HasIndex(d => d.ConstructionSiteId)
        .HasIndex(d => d.AuthorUserId)
        .HasIndex(d => d.Date);

    modelBuilder.Entity<SiteUser>()
        .HasIndex(su => new { su.ConstructionSiteId, su.UserId });

    modelBuilder.Entity<AuditHistory>()
        .HasIndex(a => new { a.EntityName, a.EntityId })
        .HasIndex(a => a.ChangedByUserId)
        .HasIndex(a => a.Timestamp);

    modelBuilder.Entity<Attachment>()
        .HasIndex(a => a.DiaryId);

    // JSON column types
    modelBuilder.Entity<DiaryTemplate>()
        .Property(t => t.Sections).HasColumnType("nvarchar(max)");
    modelBuilder.Entity<AuditHistory>()
        .Property(a => a.Changes).HasColumnType("nvarchar(max)");
}
```

---

## 11. Development Environment Setup

### Prerequisites
- .NET 10 SDK
- Node.js 22 LTS
- Docker Desktop (for local MSSQL via `docker compose`)
- SQL Server Management Studio or Azure Data Studio (optional)

### Quickstart (planned)
```bash
# 1. Start local MSSQL
docker compose up -d db

# 2. Apply EF Core migrations
cd src/SiteDiary.Web
dotnet ef database update

# 3. Start backend
dotnet run

# 4. Start frontend dev server (hot reload proxied to backend)
cd ../../frontend
npm install && npm run dev
```

### docker-compose.yml (planned)
```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "SiteDiary_Dev#2026"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
```

---

## 12. Open Questions / Deferred Decisions

| # | Topic | Decision needed |
|---|---|---|
| 1 | Authentication | ASP.NET Core Identity vs external IdP (Entra ID / Auth0)? |
| 2 | File storage | Local disk (dev) vs Azure Blob (production)? `StorageProvider` enum handles both. |
| 3 | DiaryTemplate.Sections schema | Define JSON schema for sections (type, label, required, ordering). |
| 4 | Real-time notifications | SignalR for diary publish events? Deferred to Phase 2. |
| 5 | Internationalisation | English-only for Phase 1. |
| 6 | Offline support (mobile) | Service Worker / PWA? Deferred to Phase 2. |

---

## 13. Implementation Phases

| Phase | Scope |
|---|---|
| **Phase 1 — Foundation** | Domain entities, EF Core config + migrations, IRepository/IUnitOfWork interfaces, seed data, Docker setup |
| **Phase 2 — Core API** | CRUD endpoints for Sites, Users, Roles, Diaries, DiaryTemplates; unit + integration tests |
| **Phase 3 — Attachments & Audit** | Attachment upload/download, AuditService auto-hooks via EF Core SaveChanges interceptor |
| **Phase 4 — Frontend** | React shell, routing, shared components, feature modules (sites, diaries, attachments) |
| **Phase 5 — Polish & Security** | Auth integration, input validation, OWASP hardening, E2E tests, CI pipeline |

---

## 14. Document Locations

| Artifact | Path |
|---|---|
| This design plan | `design/plan.md` |
| Backend solution | `src/` |
| Frontend project | `frontend/` |
| EF Core migrations | `src/SiteDiary.Infrastructure/Data/Migrations/` |
| Tests | `src/SiteDiary.Tests/` |

---

*This document is the authoritative reference for both the **`mobile-ui`** agent (Sections 7–8) and the **`developer`** agent (all sections). Any deviations from this plan should be discussed and reflected back here before implementation begins.*
