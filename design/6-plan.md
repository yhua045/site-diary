# Site Diary — Design Plan: Role-Based Diary Template Seeding

> **Scope:** Issue #6 — Design role-based diary templates with minimal required fields  
> **Methodology:** TDD — tests written before implementation  
> **Reviewed with:** `mobile-ui` agent (field type suitability, section grouping, form ergonomics in bottom-sheet / modal context)  
> **Date:** 16 May 2026  
> **Status:** Draft — awaiting approval (LGTB)

---

## 1. Problem Statement

The current seeder produces **one generic `IsDefault = true` template** (`"Site Daily Report"`)
shared by all roles. `DiaryTemplateService.GetByUserRoleAsync` is explicitly marked as a
"POC" that returns this single template regardless of who the user is.

Issue #6 requires **one purpose-built template per user role** so that each role sees only
the fields that matter for their responsibility.

---

## 2. Requirements

| # | Requirement |
|---|---|
| R1 | One template must exist for each of the 5 active roles: Project Manager, Site Manager, Safety Manager, Site Foreman, Construction Worker. |
| R2 | Each template uses only the fields relevant to that role (minimal set). |
| R3 | Templates must be renderable by the existing dynamic form engine without any changes to its rendering logic. |
| R4 | `GetByUserRoleAsync` must resolve the correct template for the calling user's active role. |
| R5 | Diary entries created before this change must not break (backward-compatible `TemplateSnapshot` approach already handles this). |
| R6 | A system-level fallback template (`IsDefault = true`) must remain for roles that have no assigned template. |
| R7 | All templates are extensible — new fields / sections can be added later without invalidating saved diary payloads. |
| R8 | Seeding must remain idempotent (no double-seeding on restart). |
| R9 | Every seeded template must include a file attachment field so users can attach photos and documents to their diary entry. |
| R10 | Every seeded template must include a dynamic field component that allows users to add custom fields on the fly at diary-entry time. |

---

## 3. Architecture

### 3.1 Domain Change — `DiaryTemplate.RoleId` FK

The FK is placed on **`DiaryTemplate`**, not on `Role`. This models the relationship
correctly as **One-to-Many**: one `Role` can have one or many `DiaryTemplate` records
associated with it, each carrying a `RoleId` column.

Rationale:
- Placing the FK on `DiaryTemplate` avoids any circular-reference issue during seeding —
  no two-phase link-back step is needed; `RoleId` is set directly when templates are created.
- The One-to-Many cardinality future-proofs the design: a role could later have multiple
  specialised templates (e.g. a "short form" vs. a "full form") without a schema change.
- For MVP, one template per role is seeded; the schema supports more without migration.

```
DiaryTemplate
  ├── Id
  ├── Name
  ├── Sections (JSON)
  ├── IsDefault
  ├── CreatedByUserId
  ├── IsArchived
  └── RoleId (nullable int, FK → Role.Id)   ← NEW

Role
  ├── Id
  ├── Name
  ├── Description
  └── DiaryTemplates (ICollection<DiaryTemplate>)   ← navigation only, no new column
```

Resolution path for `GetByUserRoleAsync`:

```
User
 └─► UserRole (active, IsActive = true)
       └─► Role
             └─► DiaryTemplates.Where(t => t.RoleId == role.Id).FirstOrDefault()
                  (fallback: query for IsDefault = true if no templates found for role)
```

### 3.2 New EF Migration

**Name:** `AddDiaryTemplateRoleFK`

Changes:
- Add column `RoleId int NULL` to `DiaryTemplates` table.
- Add FK constraint `FK_DiaryTemplates_Roles_RoleId` → `Roles.Id`
  with `ON DELETE SET NULL` (deleting a role does not cascade to templates).
- Add index `IX_DiaryTemplates_RoleId`.

No existing data is affected — the column is nullable and defaults to `NULL`.

### 3.3 Updated `OnModelCreating` Configuration

```csharp
modelBuilder.Entity<DiaryTemplate>(e =>
{
    // ... existing config ...
    e.HasOne(t => t.Role)
     .WithMany(r => r.DiaryTemplates)
     .HasForeignKey(t => t.RoleId)
     .OnDelete(DeleteBehavior.SetNull);

    e.HasIndex(t => t.RoleId)
     .HasDatabaseName("IX_DiaryTemplates_RoleId");
});
```

### 3.4 Entity Updates

**`DiaryTemplate` entity** — add FK column and navigation (no breaking changes):

```csharp
public int? RoleId { get; set; }   // nullable FK → Role.Id
public Role? Role { get; set; }    // navigation property
```

**`Role` entity** — add collection navigation only (no new column, no breaking changes):

```csharp
public ICollection<DiaryTemplate> DiaryTemplates { get; set; } = [];
```

---

## 4. Template Definitions (Sections JSON)

All templates follow the existing `SectionDef` / `FieldDef` JSON schema already used
by the seeder and deserialized by `DiaryTemplateService`. Supported field types:
`textarea`, `text`, `number`, `select`, `checkbox`, `file_attachment`, `dynamic_fields`.

> **New field types for Issue #6:**
> - `file_attachment` — renders a file picker / camera button on mobile; attachment references are stored in the diary payload.
> - `dynamic_fields` — renders an **"Add custom field"** button that lets the user append ad-hoc key/value fields at diary-entry time. These custom fields are persisted inside the diary payload alongside the schema-defined fields.
>
> **All templates include an "Attachments" section** (appended as the last section) containing both new field types.

> **Mobile-UI consultation note:** The `mobile-ui` agent was consulted to confirm that:
> - `textarea` fields render with a 3-row minimum in the bottom-sheet form.
> - `number` fields surface a numeric keyboard on mobile.
> - Section groupings keep each section to ≤ 3 fields so the form stays scannable
>   without scrolling past a full screen-height section.
> - All section labels are kept to ≤ 3 words so they fit the section header pill without truncation on small screens.

### 4.1 Project Manager — Progress, blockers, coordination

**Template name:** `"Project Manager Daily Report"`

| Section | Fields |
|---|---|
| Progress | Progress Summary *(textarea, required)* |
| Issues & Actions | Blockers / Risks *(textarea, optional)* · Actions / Next Steps *(textarea, optional)* |
| Notes | Notes / Comments *(textarea, optional)* |
| Attachments | File Attachments *(file_attachment, optional)* · Custom Fields *(dynamic_fields, optional)* |

```json
[
  {
    "id": "s1",
    "label": "Progress",
    "fields": [
      {
        "id": "f_progress_summary",
        "label": "Progress Summary",
        "type": "textarea",
        "required": true,
        "placeholder": "Describe overall site progress today..."
      }
    ]
  },
  {
    "id": "s2",
    "label": "Issues & Actions",
    "fields": [
      {
        "id": "f_blockers",
        "label": "Blockers / Risks",
        "type": "textarea",
        "required": false,
        "placeholder": "List any blockers or risks..."
      },
      {
        "id": "f_actions",
        "label": "Actions / Next Steps",
        "type": "textarea",
        "required": false,
        "placeholder": "What actions are planned?"
      }
    ]
  },
  {
    "id": "s3",
    "label": "Notes",
    "fields": [
      {
        "id": "f_notes",
        "label": "Notes / Comments",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s_attachments",
    "label": "Attachments",
    "fields": [
      {
        "id": "f_file_attachment",
        "label": "File Attachments",
        "type": "file_attachment",
        "required": false,
        "placeholder": "Attach photos, documents or other files..."
      },
      {
        "id": "f_dynamic_fields",
        "label": "Custom Fields",
        "type": "dynamic_fields",
        "required": false
      }
    ]
  }
]
```

### 4.2 Site Manager — Site conditions, manpower, work progress

**Template name:** `"Site Manager Daily Report"`

| Section | Fields |
|---|---|
| Work | Work Completed Today *(textarea, required)* · Crew / Manpower Count *(number, required, min 0 max 500)* |
| Conditions & Issues | Site Conditions *(textarea, optional)* · Issues / Blockers *(textarea, optional)* |
| Planning | Next Plan *(textarea, optional)* |
| Attachments | File Attachments *(file_attachment, optional)* · Custom Fields *(dynamic_fields, optional)* |

```json
[
  {
    "id": "s1",
    "label": "Work",
    "fields": [
      {
        "id": "f_work_completed",
        "label": "Work Completed Today",
        "type": "textarea",
        "required": true,
        "placeholder": "Describe work completed..."
      },
      {
        "id": "f_crew_count",
        "label": "Crew / Manpower Count",
        "type": "number",
        "required": true,
        "min": 0,
        "max": 500
      }
    ]
  },
  {
    "id": "s2",
    "label": "Conditions & Issues",
    "fields": [
      {
        "id": "f_site_conditions",
        "label": "Site Conditions",
        "type": "textarea",
        "required": false,
        "placeholder": "Weather, ground conditions, access..."
      },
      {
        "id": "f_issues",
        "label": "Issues / Blockers",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s3",
    "label": "Planning",
    "fields": [
      {
        "id": "f_next_plan",
        "label": "Next Plan",
        "type": "textarea",
        "required": false,
        "placeholder": "What is planned for tomorrow?"
      }
    ]
  },
  {
    "id": "s_attachments",
    "label": "Attachments",
    "fields": [
      {
        "id": "f_file_attachment",
        "label": "File Attachments",
        "type": "file_attachment",
        "required": false,
        "placeholder": "Attach photos, documents or other files..."
      },
      {
        "id": "f_dynamic_fields",
        "label": "Custom Fields",
        "type": "dynamic_fields",
        "required": false
      }
    ]
  }
]
```

### 4.3 Safety Manager — Safety observations and incidents

**Template name:** `"Safety Manager Daily Report"`

| Section | Fields |
|---|---|
| Compliance | Safety Check / Compliance *(textarea, required)* · Hazards Observed *(textarea, optional)* |
| Incidents | Incidents / Near Misses *(textarea, optional)* · Corrective Actions *(textarea, optional)* |
| Follow-up | Follow-up Owner / Due Date *(text, optional)* |
| Attachments | File Attachments *(file_attachment, optional)* · Custom Fields *(dynamic_fields, optional)* |

```json
[
  {
    "id": "s1",
    "label": "Compliance",
    "fields": [
      {
        "id": "f_safety_check",
        "label": "Safety Check / Compliance",
        "type": "textarea",
        "required": true,
        "placeholder": "Describe safety checks performed today..."
      },
      {
        "id": "f_hazards",
        "label": "Hazards Observed",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s2",
    "label": "Incidents",
    "fields": [
      {
        "id": "f_incidents",
        "label": "Incidents / Near Misses",
        "type": "textarea",
        "required": false,
        "placeholder": "Any incidents or near misses to report?"
      },
      {
        "id": "f_corrective_actions",
        "label": "Corrective Actions",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s3",
    "label": "Follow-up",
    "fields": [
      {
        "id": "f_followup_owner",
        "label": "Follow-up Owner / Due Date",
        "type": "text",
        "required": false,
        "placeholder": "Name and due date for follow-up..."
      }
    ]
  },
  {
    "id": "s_attachments",
    "label": "Attachments",
    "fields": [
      {
        "id": "f_file_attachment",
        "label": "File Attachments",
        "type": "file_attachment",
        "required": false,
        "placeholder": "Attach photos, documents or other files..."
      },
      {
        "id": "f_dynamic_fields",
        "label": "Custom Fields",
        "type": "dynamic_fields",
        "required": false
      }
    ]
  }
]
```

### 4.4 Site Foreman — Day-to-day execution and team progress

**Template name:** `"Site Foreman Daily Report"`

| Section | Fields |
|---|---|
| Tasks | Tasks Completed *(textarea, required)* · Work in Progress *(textarea, optional)* |
| Resources & Blockers | Resource / Manpower Notes *(textarea, optional)* · Blockers *(textarea, optional)* |
| Planning | Next-Day Plan *(textarea, optional)* |
| Attachments | File Attachments *(file_attachment, optional)* · Custom Fields *(dynamic_fields, optional)* |

```json
[
  {
    "id": "s1",
    "label": "Tasks",
    "fields": [
      {
        "id": "f_tasks_completed",
        "label": "Tasks Completed",
        "type": "textarea",
        "required": true,
        "placeholder": "List tasks completed today..."
      },
      {
        "id": "f_work_in_progress",
        "label": "Work in Progress",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s2",
    "label": "Resources & Blockers",
    "fields": [
      {
        "id": "f_resource_notes",
        "label": "Resource / Manpower Notes",
        "type": "textarea",
        "required": false
      },
      {
        "id": "f_blockers",
        "label": "Blockers",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s3",
    "label": "Planning",
    "fields": [
      {
        "id": "f_next_day_plan",
        "label": "Next-Day Plan",
        "type": "textarea",
        "required": false,
        "placeholder": "What is planned for tomorrow?"
      }
    ]
  },
  {
    "id": "s_attachments",
    "label": "Attachments",
    "fields": [
      {
        "id": "f_file_attachment",
        "label": "File Attachments",
        "type": "file_attachment",
        "required": false,
        "placeholder": "Attach photos, documents or other files..."
      },
      {
        "id": "f_dynamic_fields",
        "label": "Custom Fields",
        "type": "dynamic_fields",
        "required": false
      }
    ]
  }
]
```

### 4.5 Construction Worker — Ground-level tasks and issues

**Template name:** `"Construction Worker Daily Report"`

| Section | Fields |
|---|---|
| Work Done | Tasks Performed *(textarea, required)* · Tools / Equipment Used *(textarea, optional)* |
| Issues | Issues / Blockers *(textarea, optional)* · Notes *(textarea, optional)* |
| Attachments | File Attachments *(file_attachment, optional)* · Custom Fields *(dynamic_fields, optional)* |

```json
[
  {
    "id": "s1",
    "label": "Work Done",
    "fields": [
      {
        "id": "f_tasks_performed",
        "label": "Tasks Performed",
        "type": "textarea",
        "required": true,
        "placeholder": "What did you work on today?"
      },
      {
        "id": "f_tools_equipment",
        "label": "Tools / Equipment Used",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s2",
    "label": "Issues",
    "fields": [
      {
        "id": "f_issues",
        "label": "Issues / Blockers",
        "type": "textarea",
        "required": false
      },
      {
        "id": "f_notes",
        "label": "Notes",
        "type": "textarea",
        "required": false
      }
    ]
  },
  {
    "id": "s_attachments",
    "label": "Attachments",
    "fields": [
      {
        "id": "f_file_attachment",
        "label": "File Attachments",
        "type": "file_attachment",
        "required": false,
        "placeholder": "Attach photos, documents or other files..."
      },
      {
        "id": "f_dynamic_fields",
        "label": "Custom Fields",
        "type": "dynamic_fields",
        "required": false
      }
    ]
  }
]
```

### 4.6 System Fallback Template (existing, updated)

**Template name:** `"Site Daily Report"` (`IsDefault = true`, `RoleId = null`)  
The existing fallback template is retained and updated to also include the Attachments section
(`file_attachment` + `dynamic_fields`) to satisfy R9 and R10. Used when no template with
a matching `RoleId` is found for the user's role.

---

## 5. Seeder Changes (`DataSeeder.cs`)

The seeder currently exits early if any `ConstructionSites`, `Roles`, or `Users` exist.
This guard must be extended to also check `DiaryTemplates` to detect a partial re-seed.

### 5.1 Updated Guard

```csharp
if (await context.ConstructionSites.AnyAsync() ||
    await context.Roles.AnyAsync()             ||
    await context.Users.AnyAsync()             ||
    await context.DiaryTemplates.AnyAsync())   // ← ADD
{
    return;
}
```

### 5.2 Seeding Order (to resolve FK dependencies)

```
Step 1  — Seed ConstructionSites (no FK deps)
Step 2  — Seed Roles (no FK to DiaryTemplates — FK is on DiaryTemplate side)
Step 3  — SaveChangesAsync()            ← Roles get database IDs
Step 4  — Seed Users (FK → Roles via UserRole)
Step 5  — SaveChangesAsync()            ← Users get database IDs
Step 6  — Seed SiteUser associations
Step 7  — SaveChangesAsync()
Step 8  — Seed 5 role-specific DiaryTemplates  (CreatedByUserId = users[0].Id,
                                                  RoleId = corresponding role.Id)
          Seed 1 fallback DiaryTemplate         (IsDefault = true, RoleId = null)
Step 9  — SaveChangesAsync()            ← Done
```

> The FK direction (`DiaryTemplate.RoleId`) eliminates the circular-dependency concern
> from the original design. `RoleId` is simply set when each template object is constructed
> — no link-back phase or second `SaveChangesAsync` is required.

### 5.3 Template-to-Role Mapping Table

The `RoleId` property is set directly on each `DiaryTemplate` object when it is constructed in the seeder.

| Role Name | Template Name | RoleId set to |
|---|---|---|
| Project Manager | Project Manager Daily Report | `roles["Project Manager"].Id` |
| Site Manager | Site Manager Daily Report | `roles["Site Manager"].Id` |
| Safety Manager | Safety Manager Daily Report | `roles["Safety Manager"].Id` |
| Site Foreman | Site Foreman Daily Report | `roles["Site Foreman"].Id` |
| Construction Worker | Construction Worker Daily Report | `roles["Construction Worker"].Id` |
| *(fallback)* | Site Daily Report (`IsDefault = true`) | `null` |

---

## 6. Service Changes (`DiaryTemplateService.cs`)

### 6.1 Updated `GetByUserRoleAsync`

Replace the current POC implementation with:

```csharp
public async Task<DiaryTemplateDto?> GetByUserRoleAsync(int userId, CancellationToken ct = default)
{
    // Resolve: User → active UserRole → Role
    var role = await uow.UserRoles
        .Query()
        .Where(ur => ur.UserId == userId && ur.IsActive)
        .Select(ur => ur.Role)
        .FirstOrDefaultAsync(ct);

    // Look up the role-specific template by RoleId
    var template = role is not null
        ? await uow.DiaryTemplates
               .Query()
               .FirstOrDefaultAsync(t => t.RoleId == role.Id, ct)
        : null;

    // Fall back to the system default if no role-specific template found
    template ??= await uow.DiaryTemplates
           .Query()
           .FirstOrDefaultAsync(t => t.IsDefault, ct);

    if (template is null) return null;

    var sections = DeserializeSections(template.Sections);
    return new DiaryTemplateDto(template.Id, template.Name, sections);
}
```

### 6.2 `IDiaryTemplateService` — No interface changes

The signature `Task<DiaryTemplateDto?> GetByUserRoleAsync(int userId, ...)` is unchanged.
Only the implementation changes — all callers remain unaffected.

---

## 7. UI Considerations

> **Consultation with `mobile-ui` agent confirmed:**  
> This issue is **purely a backend seeding and schema change**. The existing dynamic form
> engine in the React frontend already:
> - Fetches the template from `/api/users/{userId}/diary-template` (via `GetByUserRoleAsync`).
> - Renders sections and fields dynamically in declaration order.
> - Applies per-field input controls based on `type` (`textarea`, `number`, `text`, `select`, `checkbox`).
> - Stores a `TemplateSnapshot` alongside each saved diary entry (backward compatibility).
>
> **No changes to React components are required.** Once the correct role-specific
> template is returned by the API, the form will render accordingly.
>
> **Mobile-UI recommendations for field design (applied above):**
> - Keep each section to ≤ 3 fields to avoid overflowing one screen height in the bottom sheet.
> - Use `textarea` (not `text`) for all narrative fields — this triggers the multi-line
>   keyboard area on iOS/Android, which is more ergonomic for field notes.
> - `number` fields must set `min` / `max` so the mobile numeric stepper renders sensible bounds.
> - Section labels capped at 3 words to avoid truncation in the section header pill.

---

## 8. Test Plan (TDD)

Tests must be written **before** implementation. The following tests are required:

### 8.1 `DataSeederTests` (Integration — existing file)

| Test | Assert |
|---|---|
| `SeedAsync_ShouldSeed6Templates` | `DiaryTemplates.Count() == 6` (5 role + 1 fallback) |
| `SeedAsync_ShouldLinkEachRoleToItsTemplate` | For each of the 5 roles, exactly one `DiaryTemplate` with `RoleId == role.Id` exists |
| `SeedAsync_FallbackTemplate_IsDefault` | Exactly one template has `IsDefault == true` and `RoleId == null` |
| `SeedAsync_RoleTemplates_AreNotDefault` | The 5 role-specific templates all have `IsDefault == false` |
| `SeedAsync_ShouldNotDoubleSeed` | Calling `SeedAsync` twice still yields 6 templates |
| `SeedAsync_AllTemplates_HaveFileAttachmentField` | Every template's `Sections` JSON contains at least one field with `"type": "file_attachment"` |
| `SeedAsync_AllTemplates_HaveDynamicFieldsField` | Every template's `Sections` JSON contains at least one field with `"type": "dynamic_fields"` |

### 8.2 `DiaryTemplateServiceTests` (Unit — new file)

| Test | Arrange | Assert |
|---|---|---|
| `GetByUserRoleAsync_ReturnsRoleTemplate_WhenRoleHasTemplate` | User with Project Manager role; a `DiaryTemplate` with `RoleId = projectManagerRole.Id` exists | Returns template with name `"Project Manager Daily Report"` |
| `GetByUserRoleAsync_ReturnsFallback_WhenRoleHasNoTemplate` | User's role has no `DiaryTemplate` with matching `RoleId`; fallback template (`IsDefault = true`) exists | Returns the fallback template |
| `GetByUserRoleAsync_ReturnsNull_WhenNoTemplateExists` | User with no active role; no templates in DB | Returns `null` |
| `GetByUserRoleAsync_IgnoresInactiveUserRoles` | User has both active and inactive UserRoles | Resolves from the active role's template only |

---

## 9. Files to Change

| File | Change |
|---|---|
| `SiteDiary.Domain/Entities/DiaryTemplate.cs` | Add `RoleId` (nullable int) + `Role` navigation property |
| `SiteDiary.Domain/Entities/Role.cs` | Add `DiaryTemplates` collection navigation (no new column) |
| `SiteDiary.Infrastructure/Data/ApplicationDbContext.cs` | Add FK config for `DiaryTemplate.RoleId` in `OnModelCreating` |
| `SiteDiary.Infrastructure/Data/DataSeeder.cs` | Extend guard; seed 5 role templates (with `RoleId` set) + 1 fallback (with updated Sections JSON including `file_attachment` + `dynamic_fields`) |
| `SiteDiary.Application/Features/DiaryTemplates/DiaryTemplateService.cs` | Replace POC `GetByUserRoleAsync` with `RoleId`-based lookup |
| *(new)* EF Migration `AddDiaryTemplateRoleFK` | Add `RoleId` column + FK to `DiaryTemplates` table |
| `SiteDiary.Tests/Integration/DataSeederTests.cs` | Add 7 new seeder assertions (including field-type checks) |
| *(new)* `SiteDiary.Tests/Unit/DiaryTemplateServiceTests.cs` | 4 new unit tests for `GetByUserRoleAsync` |

---

## 10. Out of Scope

- UI component changes (dynamic form engine already handles variable templates).
- API controller changes (`GET /api/users/{userId}/diary-template` signature unchanged).
- Admin UI for managing templates (post-MVP).
- Per-site template overrides.
- Multi-template selection per user.

---

## 11. Acceptance Criteria (from Issue #6)

| Criterion | How satisfied |
|---|---|
| One template exists for each active user role | 5 role-specific templates seeded; each `Role.DiaryTemplateId` set |
| Each template uses the smallest practical set of fields | 2–3 sections, ≤ 3 fields per section, fields relevant to role only |
| Templates are readable and presentable in the UI | Existing dynamic form renders without changes |
| Templates map cleanly to the dynamic diary form and diary timeline cards | Same `SectionDef`/`FieldDef` JSON schema; `TemplateSnapshot` stored at diary creation |
| Templates can be extended later without breaking existing diary entries | `TemplateSnapshot` already captures the schema at time of creation; new fields don't affect old entries |

---

## Handoff

> Once approved with **LGTB**, hand off to the `developer` agent:
>
> **"Plan approved. Write failing tests for these requirements."**
>
> Reference this document at `design/6-plan.md`.  
> Start with `DataSeederTests` and `DiaryTemplateServiceTests` before any implementation.
