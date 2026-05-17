# Issue #8 — Refactor: Apply DTO at Controller Boundary; Map to Domain Entities (AutoMapper)

> **Status:** Draft — awaiting `developer` agent handoff  
> **Reviewed with:** `mobile-ui` agent (UI impact assessment — see §6)  
> **Date:** 17 May 2026  
> **TDD approach:** All interface contracts must be defined before implementation; failing tests are written first.

---

## 1. Problem Statement

The current architecture has a **mixed-boundary violation**: service interfaces accept and return DTO types (`CreateDiaryDto`, `UpdateDiaryDto`, `CreateConstructionSiteRequest`, `UserDto`, etc.). This means:

- Domain services are coupled to API contract types.
- Mapping logic is scattered ad-hoc inside each service method (`new Diary { Title = dto.Title, ... }`).
- Services cannot be reused in non-HTTP contexts without dragging in HTTP-facing types.
- There is no central, testable location for mapping rules.

**Goal:** Move all DTO ↔ domain entity translation to the controller boundary using AutoMapper. Service interfaces and implementations must operate exclusively on domain entities.

---

## 2. Architectural Target State

```
HTTP Request (JSON)
        │
        ▼
  [Controller]  ──── IMapper.Map<DomainEntity>(dto) ────►  [Service]
                                                              │  operates on
                                                              │  domain entities only
                                                              ▼
                                                         [Repository / UoW]
        │
        ▼
  IMapper.Map<Dto>(entity)
        │
        ▼
HTTP Response (JSON)
```

### 2.1 Layering Rules (post-refactor)

| Layer | May reference | May NOT reference |
|---|---|---|
| `SiteDiary.Domain` | Nothing external | DTO types |
| `SiteDiary.Application` | Domain entities, `OperationResult<T>`, `PagedResult<T>` | DTO types in service interface signatures |
| `SiteDiary.Web` | Application + Domain + AutoMapper | EF Core internals |
| `SiteDiary.Tests` | All of the above + AutoMapper.Extensions | — |

> **Note:** DTO record types remain physically in the Application project (they are API contracts). However, no service interface or implementation may reference them as method parameters or return types.

---

## 3. New Shared Abstraction

### 3.1 `PagedResult<T>` — `SiteDiary.Application/Shared/PagedResult.cs`

```csharp
namespace SiteDiary.Application.Shared;

/// <summary>Generic pagination wrapper returned by services. T is always a domain entity.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

This replaces `AuditLogPageDto` as a service return type. The controller maps `PagedResult<AuditHistory>` → `AuditLogPageDto` for views/responses.

---

## 4. Service Interface Changes

### 4.1 `IDiaryService` (Application/Features/Diaries/IDiaryService.cs)

**Before → After** for each method:

| Method | Before (param types) | After (param types) | Before (return) | After (return) |
|---|---|---|---|---|
| `GetBySiteIdAsync` | `int siteId` | unchanged | `IReadOnlyList<DiaryDto>` | `IReadOnlyList<Diary>` |
| `GetTimelineAsync` | `int siteId` | unchanged | `IReadOnlyList<DiaryTimelineEntryDto>` | `IReadOnlyList<Diary>` |
| `GetByIdWithAttachmentsAsync` | `int siteId, int diaryId` | unchanged | `DiaryDetailDto?` | `Diary?` |
| `CreateAsync` | `int siteId, int authorUserId, CreateDiaryDto dto` | `int siteId, int authorUserId, Diary diary` | `DiaryDto` | `Diary` |
| `UpdateAsync` | `…, UpdateDiaryDto dto` | `…, Diary updateValues` | `OperationResult<DiaryDto>` | `OperationResult<Diary>` |
| `DeleteAsync` | unchanged | unchanged | `OperationResult<bool>` | unchanged |

> **Special case — `TemplateSnapshot`:** Building the template snapshot in `CreateAsync` requires a DB query (looking up `DiaryTemplate` sections). This logic stays in `DiaryService.CreateAsync`; the controller passes a `Diary` entity with `DiaryTemplateId` set, and the service fills `TemplateSnapshot` before persisting.

> **Special case — `GetTimelineAsync`:** The service must eager-load `Author`, `Author.UserRoles`, `Author.UserRoles.Role`, and `Attachments` navigation properties. The AutoMapper profile maps the fully-loaded `Diary` to `DiaryTimelineEntryDto`.

### 4.2 `ISiteService` (Application/Interfaces/ISiteService.cs)

| Method | Before (input) | After (input) | Before (return) | After (return) |
|---|---|---|---|---|
| `GetAllAsync` | — | — | `IReadOnlyList<ConstructionSiteDto>` | `IReadOnlyList<ConstructionSite>` |
| `GetByIdAsync` | `int id` | unchanged | `ConstructionSiteDto?` | `ConstructionSite?` |
| `GetByUserIdAsync` | `int userId` | unchanged | `IReadOnlyList<ConstructionSiteDto>` | `IReadOnlyList<ConstructionSite>` |
| `CreateAsync` | `CreateConstructionSiteRequest` | `ConstructionSite` | `ConstructionSiteDto` | `ConstructionSite` |
| `UpdateAsync` | `int id, UpdateConstructionSiteRequest` | `int id, ConstructionSite updateValues` | `ConstructionSiteDto?` | `ConstructionSite?` |
| `ArchiveAsync` | `int id` | unchanged | `bool` | unchanged |

### 4.3 `IUserService` (Application/Interfaces/IUserService.cs)

| Method | Before (input) | After (input) | Before (return) | After (return) |
|---|---|---|---|---|
| `GetAllAsync` | — | — | `IReadOnlyList<UserDto>` | `IReadOnlyList<User>` |
| `GetByIdAsync` | `int id` | unchanged | `UserDto?` | `User?` |
| `CreateAsync` | `CreateUserRequest` | `User` | `UserDto` | `User` |
| `UpdateAsync` | `int id, UpdateUserRequest` | `int id, User updateValues` | `UserDto?` | `User?` |

### 4.4 `IAuditLogService` (Application/Features/AuditLogs/IAuditLogService.cs)

| Method | Before (return) | After (return) |
|---|---|---|
| `GetPageAsync(int page, int pageSize, ct)` | `AuditLogPageDto` | `PagedResult<AuditHistory>` |

### 4.5 `IAttachmentService` (Application/Features/Attachments/IAttachmentService.cs)

| Method | Before (return) | After (return) |
|---|---|---|
| `UploadAsync(…)` | `OperationResult<AttachmentDto>` | `OperationResult<Attachment>` |
| `DeleteAsync(…)` | `OperationResult<bool>` | unchanged |

### 4.6 `IDiaryTemplateService` (Application/Features/DiaryTemplates/IDiaryTemplateService.cs)

| Method | Before (return) | After (return) |
|---|---|---|
| `GetByIdAsync(int id, ct)` | `DiaryTemplateDto?` | `DiaryTemplate?` |
| `GetByUserRoleAsync(int userId, ct)` | `DiaryTemplateDto?` | `DiaryTemplate?` |

---

## 5. AutoMapper Profiles

All profiles reside in **`SiteDiary.Web/Mappings/`**. The Web project is the controller boundary; profiles belong where mapping decisions live.

### 5.1 `DiaryMappingProfile`

```
CreateDiaryDto  →  Diary
  - Title, Content, Date (DateTimeOffset → DateOnly via .Date), IsPublished, DiaryTemplateId: direct
  - FieldOverrides: ValueConverter (FieldOverridesDto? → string? via JsonSerializer.Serialize)
  - Payload: ValueConverter (Dictionary<string, JsonElement>? → string? via JsonSerializer.Serialize)
  - TemplateSnapshot, ConstructionSiteId, AuthorUserId, CreatedAt, UpdatedAt: Ignore (set by service/middleware)

UpdateDiaryDto  →  Diary
  - Title, Content, Date (DateTimeOffset → DateOnly): direct
  - FieldOverrides: ValueConverter
  - All other fields: Ignore

Diary  →  DiaryDto
  - Date (DateOnly → DateTimeOffset): via DateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
  - All other scalar fields: direct (Id, ConstructionSiteId, AuthorUserId, Title, Content, IsPublished, DiaryTemplateId)

Diary  →  DiaryDetailDto
  - Same as DiaryDto plus:
  - Attachments: via Attachment → AttachmentDto sub-mapping
  - FieldOverrides: ValueConverter (string? → FieldOverridesDto? via JsonSerializer.Deserialize)

Diary  →  DiaryTimelineEntryDto
  - Id, ConstructionSiteId, AuthorUserId, Date, IsPublished: direct
  - AuthorName: MapFrom(src => src.Author.FullName)
  - AuthorRole: MapFrom(src => src.Author.UserRoles.FirstOrDefault()?.Role?.Name)
  - Payload: ValueConverter (string? → JsonElement via JsonDocument.Parse / .RootElement.Clone)
  - TemplateSnapshot: ValueConverter (string? → IReadOnlyList<FieldDescriptorDto> via JsonSerializer.Deserialize)
  - Attachments: via Attachment → AttachmentDto sub-mapping

Attachment  →  AttachmentDto
  - Id, DiaryId, FileName, FileUrl, ContentType: direct
```

### 5.2 `SiteMappingProfile`

```
CreateConstructionSiteRequest  →  ConstructionSite
  - Name, Description, Address: direct
  - Id, IsArchived, CreatedAt, UpdatedAt, SiteUsers, Diaries: Ignore

UpdateConstructionSiteRequest  →  ConstructionSite
  - Name, Description, Address: direct
  - All other fields: Ignore

ConstructionSite  →  ConstructionSiteDto
  - All scalar fields direct (Id, Name, Description, Address, IsArchived, CreatedAt, UpdatedAt)
```

### 5.3 `UserMappingProfile`

```
CreateUserRequest  →  User
  - FirstName, LastName, Email: direct
  - IsActive (default true), IsArchived (default false), Id, CreatedAt, UpdatedAt: Ignore

UpdateUserRequest  →  User
  - FirstName, LastName, Email, IsActive: direct
  - All other fields: Ignore

User  →  UserDto
  - All scalar fields direct (Id, FirstName, LastName, Email, IsActive, IsArchived, CreatedAt, UpdatedAt)
```

### 5.4 `AuditLogMappingProfile`

```
AuditHistory  →  AuditLogDto
  - Id, EntityName, EntityId, Action, ChangedByUserId, Changes, Timestamp: direct
  - ChangedByUserName: MapFrom(src => src.ChangedBy != null ? src.ChangedBy.FullName : "System")
```

### 5.5 `DiaryTemplateMappingProfile`

```
DiaryTemplate  →  DiaryTemplateDto
  - Id, Name: direct
  - Sections: ValueConverter (string → IReadOnlyList<SectionDef> via JsonSerializer.Deserialize)
```

### 5.6 Attachment (already covered in DiaryMappingProfile — no separate profile needed)

The `Attachment → AttachmentDto` mapping is registered in `DiaryMappingProfile` since it is always used in that context.

---

## 6. UI / Mobile Alignment (mobile-ui Agent Review)

> This is a backend-only refactor. All DTO shapes returned to the frontend **remain identical**. The controller still returns the same JSON structures — the mapping just happens via AutoMapper instead of ad-hoc service methods.

Findings confirmed with the `mobile-ui` agent:

- **No breaking changes** to the React frontend's API contract. All response shapes (`DiaryDto`, `DiaryTimelineEntryDto`, `AttachmentDto`, etc.) are preserved verbatim.
- The existing Tailwind CSS card renderer (`DiaryCard.tsx`) depends on `templateSnapshot` and `payload` JSON fields — these are correctly preserved in the `Diary → DiaryTimelineEntryDto` profile (§5.1).
- The `[+]` create-diary modal and update sheet POST/PUT to `/api/sites/{siteId}/diaries` — request bodies remain unchanged.
- No mobile layout or style changes are required.

---

## 7. Controller Changes

Each controller gains an `IMapper` constructor parameter. The pattern per controller action is:

**Write (POST/PUT):**
```
1. Accept DTO from [FromBody]
2. _mapper.Map<DomainEntity>(dto)  ← translate at boundary
3. (optionally set route-bound fields: siteId, userId)
4. service.MethodAsync(domainEntity, ct)  ← service receives entity
5. _mapper.Map<ResponseDto>(returnedEntity)  ← translate before response
6. Return action result
```

**Read (GET):**
```
1. service.MethodAsync(ids, ct)  ← service returns entity/list
2. _mapper.Map<Dto>(entity) or _mapper.Map<IReadOnlyList<Dto>>(list)
3. Return action result
```

### 7.1 Affected Controllers

| Controller | Actions changed |
|---|---|
| `DiariesController` | All (GetAll, GetTimeline, GetById, Create, Update, Delete) |
| `SitesController` | All (GetAll, GetById, Create, Update, Archive) |
| `UsersController` | GetAll, GetById, Create, Update, GetSitesByUserId, GetDiaryTemplateByUserId |
| `AuditLogsController` | Index |
| `DiaryTemplatesController` | GetById |
| `AttachmentsController` | Upload |

---

## 8. DI Registration

In `SiteDiary.Web/Program.cs`, add after existing service registrations:

```csharp
// ── AutoMapper ────────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

`AddAutoMapper` scans the assembly for all `Profile` subclasses and registers `IMapper` as a singleton.

**NuGet packages to add:**

| Project | Package |
|---|---|
| `SiteDiary.Web` | `AutoMapper` ≥ 13.0 |
| `SiteDiary.Tests` | `AutoMapper` ≥ 13.0 |

---

## 9. Files to Create / Modify

### New Files

| Path | Description |
|---|---|
| `src/SiteDiary.Application/Shared/PagedResult.cs` | Generic `PagedResult<T>` record |
| `src/SiteDiary.Web/Mappings/DiaryMappingProfile.cs` | AutoMapper profile for Diary feature |
| `src/SiteDiary.Web/Mappings/SiteMappingProfile.cs` | AutoMapper profile for ConstructionSite |
| `src/SiteDiary.Web/Mappings/UserMappingProfile.cs` | AutoMapper profile for User |
| `src/SiteDiary.Web/Mappings/AuditLogMappingProfile.cs` | AutoMapper profile for AuditHistory |
| `src/SiteDiary.Web/Mappings/DiaryTemplateMappingProfile.cs` | AutoMapper profile for DiaryTemplate |
| `src/SiteDiary.Tests/Unit/Mappings/MappingProfileTests.cs` | Profile validity tests (`AssertConfigurationIsValid`) |
| `src/SiteDiary.Tests/Unit/Web/DiariesControllerTests.cs` | Updated controller unit tests |
| `src/SiteDiary.Tests/Unit/Web/SitesControllerTests.cs` | Updated controller unit tests |
| `src/SiteDiary.Tests/Unit/Web/AttachmentsControllerTests.cs` | New controller unit tests |

### Modified Files

| Path | Change |
|---|---|
| `src/SiteDiary.Application/Features/Diaries/IDiaryService.cs` | Update signatures (§4.1) |
| `src/SiteDiary.Application/Features/Diaries/DiaryService.cs` | Update to accept/return domain entities |
| `src/SiteDiary.Application/Interfaces/ISiteService.cs` | Update signatures (§4.2) |
| `src/SiteDiary.Application/Services/SiteService.cs` | Remove inline mapping; accept/return entities |
| `src/SiteDiary.Application/Interfaces/IUserService.cs` | Update signatures (§4.3) |
| `src/SiteDiary.Application/Services/UserService.cs` | Remove inline mapping; accept/return entities |
| `src/SiteDiary.Application/Features/AuditLogs/IAuditLogService.cs` | Return `PagedResult<AuditHistory>` |
| `src/SiteDiary.Application/Features/AuditLogs/AuditLogService.cs` | Return domain entities in `PagedResult` |
| `src/SiteDiary.Application/Features/Attachments/IAttachmentService.cs` | Return `OperationResult<Attachment>` |
| `src/SiteDiary.Application/Features/Attachments/AttachmentService.cs` | Return `Attachment` entity |
| `src/SiteDiary.Application/Features/DiaryTemplates/IDiaryTemplateService.cs` | Return `DiaryTemplate?` |
| `src/SiteDiary.Application/Features/DiaryTemplates/DiaryTemplateService.cs` | Return domain entities |
| `src/SiteDiary.Web/Features/Diaries/DiariesController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Controllers/Api/SitesController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Controllers/Api/UsersController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Features/AuditLogs/AuditLogsController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Features/DiaryTemplates/DiaryTemplatesController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Features/Attachments/AttachmentsController.cs` | Inject IMapper; map at boundary |
| `src/SiteDiary.Web/Program.cs` | Add `AddAutoMapper(typeof(Program).Assembly)` |
| `src/SiteDiary.Web/SiteDiary.Web.csproj` | Add `<PackageReference Include="AutoMapper" …/>` |
| `src/SiteDiary.Tests/SiteDiary.Tests.csproj` | Add `<PackageReference Include="AutoMapper" …/>` |
| `src/SiteDiary.Tests/Unit/Web/DiaryTemplatesControllerTests.cs` | Update: services now return entities; add IMapper mock |
| `src/SiteDiary.Tests/Unit/Web/UsersControllerTests.cs` | Update: services now return entities; add IMapper mock |
| `src/SiteDiary.Tests/Unit/Application/DiaryServiceV2Tests_InMemory.cs` | Update: CreateAsync / UpdateAsync now accept Diary entity |
| `src/SiteDiary.Tests/Unit/Application/SiteServiceTests.cs` | Update: service signatures changed |
| `src/SiteDiary.Tests/Unit/Application/AttachmentServiceTests.cs` | Update: UploadAsync returns Attachment entity |
| `src/SiteDiary.Tests/Unit/Application/DiaryTemplateServiceTests.cs` | Update: service returns DiaryTemplate entity |
| `src/SiteDiary.Tests/Unit/Application/AuditLogServiceTests.cs` | Update: service returns PagedResult<AuditHistory> |

---

## 10. TDD Implementation Order

Follow strict Red → Green → Refactor cycles in this sequence:

### Phase A — Contracts and Shared Types (no implementation)
1. Add `PagedResult<T>` to `Application/Shared/`.
2. Update all service interfaces (§4.1–4.6) — this breaks existing tests immediately (Red).

### Phase B — Failing Tests First
3. Write `MappingProfileTests.cs` (all `AssertConfigurationIsValid()` — will fail until profiles exist).
4. Update/write controller unit tests expecting:
   - Service mock returns **domain entity** (not DTO).
   - `IMapper.Map<Dto>(entity)` is called (verify with `Mock<IMapper>`).
5. Update service unit tests: `CreateAsync` receives `Diary` entity, not `CreateDiaryDto`.

### Phase C — Make Tests Green
6. Add AutoMapper NuGet packages.
7. Implement AutoMapper profiles (§5.1–5.5) — mapping tests go green.
8. Refactor service implementations to accept/return domain entities — service tests go green.
9. Refactor controllers to inject `IMapper` and map at boundary — controller tests go green.
10. Update `Program.cs` DI registration.

### Phase D — Integration Verification
11. Run full test suite.
12. Verify existing integration test (`AuditLogsControllerIntegrationTests`) still passes.
13. Manual smoke-test: `GET /api/sites/{id}/diaries/timeline` returns same JSON shape.

---

## 11. Key Design Decisions

| Decision | Rationale |
|---|---|
| Profiles in `SiteDiary.Web` (not Application) | Mapping is a controller-boundary concern; Application layer must not depend on AutoMapper framework |
| DTOs remain in Application project | They are API contracts shared between controllers; not service concerns |
| `TemplateSnapshot` built in `DiaryService.CreateAsync` | Requires a DB query for the template — cannot be done in controller without violating layer boundaries |
| `Diary → DiaryTimelineEntryDto` via eager-loaded navigation | Clean single `Map<>` call; avoids N+1 if `Include` is correct; service's responsibility is to load the graph |
| `PagedResult<T>` in Application/Shared | Pagination metadata is application-level, not domain-level; makes the service boundary explicit |
| `AutoMapper` ≥ 13 (no `AutoMapper.Extensions.Microsoft.DependencyInjection`) | In v13+, `AddAutoMapper(Assembly)` is built into the core package |
| `IMapper` as singleton | AutoMapper profiles are stateless; singleton is appropriate and performant |
| Controller tests mock `IMapper` | Keeps controller tests fast; profile correctness is verified separately in `MappingProfileTests` |

---

## 12. Risk Register

| Risk | Mitigation |
|---|---|
| `DiaryTimelineEntryDto` projection breaks if Author nav property is null | Profile uses `NullSubstitute` or null-safe ForMember expressions; service ensures Author is included |
| `TemplateSnapshot` / `Payload` JSON round-trip fidelity | Covered by profile unit tests with representative fixtures |
| Existing `UsersControllerTests` mock `IUserService` returning `UserDto` — tests break | Tests must be updated in Phase B to return `User` entity and use a mock `IMapper` |
| `AuditLogsController` is MVC (Razor view), not API — `IMapper` injection pattern applies the same way | The MVC controller follows identical injection pattern; view model remains `AuditLogPageDto` |

---

*Design document location: `/Users/boqi/Documents/Git/site-diary/design/8-plan.md`*  
*Reference for `developer` agent and `mobile-ui` agent.*
