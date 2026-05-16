# Design Plan 7 — Security Middleware Architecture

**Status:** Draft — awaiting approval  
**Date:** 2026-05-16  
**Scope:** ASP.NET Core middleware pipeline (`SiteDiary.Web`)

---

## 1. Context & Goals

The current pipeline has a single `XUserIdMiddleware` that extracts the `X-User-Id` header and stores the parsed integer in `HttpContext.Items`. Enforcement is done **per-action** inside controllers — each write action manually calls `HttpContext.GetCurrentUserId()` and returns `BadRequest` if absent. This approach has two problems:

1. **No site/diary-level access control.** Any user with a valid header can read or write any site's data — there is no check that the user is actually a member of the requested site.
2. **Boilerplate in every action.** The `userId` null-check is repeated across `DiariesController`, `AttachmentsController`, and `DiaryTemplatesController`.

This plan introduces **two focused middleware components** that together enforce:

- **Every** request (except those explicitly opted out) must carry a valid `X-User-Id` header. Missing or non-integer values are rejected with **401 Unauthorized** before the controller is reached.
- Endpoints can opt out of all resource-authorization checks by decorating the action or controller with `[SkipResourceAuthorization]` (or the built-in `[AllowAnonymous]`). This is required for genuinely public endpoints such as `GET api/users`.
- Requests that target a `siteId` or `diaryId` route parameter additionally require that the authenticated user is a member of the resolved construction site (via `SiteUser`). Membership failures are rejected with **403 Forbidden**.

---

## 2. Current State

```
Request
  → XUserIdMiddleware           (extracts header → Items[UserId])
  → MapControllers()            (route + action selection)
    → Controller action         (checks userId manually)
```

`XUserIdMiddleware` is a **pass-through** — it never rejects requests. Route values (`siteId`, `diaryId`) are never read by middleware.

---

## 3. New Architecture

### 3.1 Middleware Pipeline (after change)

```
Request
  → app.UseRouting()                          ← explicit, ensures RouteValues populated
  → RequestContextExtractionMiddleware        ← new (replaces XUserIdMiddleware)
  → ResourceAuthorizationMiddleware           ← new
  → app.MapControllers()
```

Both new middleware components are registered **after** `UseRouting()` so that `HttpContext.GetRouteValue(...)` is populated when they execute.

---

### 3.2 Scoped Context Object — `IRequestSecurityContext`

**Layer:** `SiteDiary.Web.Middleware`

Rather than scattering `HttpContext.Items` magic keys across the codebase, a scoped service is used as the shared communication channel between the two middleware and controllers.

```csharp
// SiteDiary.Web/Middleware/IRequestSecurityContext.cs
public interface IRequestSecurityContext
{
    /// <summary>Parsed value from the X-User-Id header. Null if header absent or non-integer.</summary>
    int? AuthenticatedUserId { get; set; }

    /// <summary>siteId route parameter, if present in the current route.</summary>
    int? RequestedSiteId { get; set; }

    /// <summary>diaryId route parameter, if present in the current route.</summary>
    int? RequestedDiaryId { get; set; }

    /// <summary>userId route parameter from routes such as <c>api/users/{userId:int}/…</c>. Validated against <see cref="AuthenticatedUserId"/> by <see cref="ResourceAuthorizationMiddleware"/>.</summary>
    int? RequestedUserId { get; set; }
}

// SiteDiary.Web/Middleware/RequestSecurityContext.cs
public sealed class RequestSecurityContext : IRequestSecurityContext
{
    public int? AuthenticatedUserId { get; set; }
    public int? RequestedSiteId { get; set; }
    public int? RequestedDiaryId { get; set; }
    public int? RequestedUserId { get; set; }
}
```

**Registration:** `services.AddScoped<IRequestSecurityContext, RequestSecurityContext>();`

`HttpContextKeys` and `HttpContextExtensions` remain unchanged for backward compatibility; controllers may migrate to `IRequestSecurityContext` incrementally.

---

### 3.3 Extraction Middleware — `RequestContextExtractionMiddleware`

**Layer:** `SiteDiary.Web.Middleware`  
**Replaces:** `XUserIdMiddleware`

Responsibilities:
1. Parse `X-User-Id` header → set `IRequestSecurityContext.AuthenticatedUserId` AND `HttpContext.Items[HttpContextKeys.UserId]` (backward compat).
2. Read `HttpContext.GetRouteValue("siteId")` → set `IRequestSecurityContext.RequestedSiteId`.
3. Read `HttpContext.GetRouteValue("diaryId")` → set `IRequestSecurityContext.RequestedDiaryId`.
4. Read `HttpContext.GetRouteValue("userId")` → set `IRequestSecurityContext.RequestedUserId`.
5. **Always calls `next`** — no short-circuiting; this is pure extraction.

```
// Pseudocode
public async Task InvokeAsync(HttpContext ctx, IRequestSecurityContext secCtx)
{
    // 1. Header extraction (keep existing Items key for backward compat)
    if (TryParseHeader(ctx, "X-User-Id", out int userId))
    {
        secCtx.AuthenticatedUserId = userId;
        ctx.Items[HttpContextKeys.UserId] = userId;   // backward compat
    }

    // 2. Route value extraction
    if (TryParseRouteInt(ctx, "siteId", out int siteId))
        secCtx.RequestedSiteId = siteId;

    if (TryParseRouteInt(ctx, "diaryId", out int diaryId))
        secCtx.RequestedDiaryId = diaryId;

    if (TryParseRouteInt(ctx, "userId", out int routeUserId))
        secCtx.RequestedUserId = routeUserId;

    await next(ctx);
}
```

**Note on diaryId-only routes:** `POST /api/diaries/{diaryId}/attachments` has a `diaryId` but no `siteId`. The extraction middleware captures `diaryId`; the authorization middleware resolves the parent `siteId` from the diary record (see §3.5).

---

### 3.4 Authorization Service Interface — `ISiteAuthorizationService`

**Layer:** `SiteDiary.Application` (new interface under `Application/Interfaces/`)

This interface is the authorization contract. The infrastructure implementation queries EF Core. Placing it in the Application layer keeps the middleware decoupled from EF Core.

```csharp
// SiteDiary.Application/Interfaces/ISiteAuthorizationService.cs
public interface ISiteAuthorizationService
{
    /// <summary>
    /// Returns true if <paramref name="userId"/> has an active SiteUser row for
    /// <paramref name="siteId"/> (ConstructionSite.IsArchived == false).
    /// </summary>
    Task<bool> IsUserMemberOfSiteAsync(int userId, int siteId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the ConstructionSiteId for the given <paramref name="diaryId"/>,
    /// then delegates to <see cref="IsUserMemberOfSiteAsync"/>.
    /// Returns false if the diary does not exist (treats absence as unauthorized).
    /// </summary>
    Task<bool> IsUserAuthorizedForDiaryAsync(int userId, int diaryId, CancellationToken ct = default);
}
```

**Registration:** `services.AddScoped<ISiteAuthorizationService, SiteAuthorizationService>();`

---

### 3.5 Authorization Middleware — `ResourceAuthorizationMiddleware`

**Layer:** `SiteDiary.Web.Middleware`

Decision tree (executed in order):

```
-1. Endpoint metadata contains [SkipResourceAuthorization] or [AllowAnonymous]
        → skip ALL validation, pass through immediately

 0. AuthenticatedUserId == null  (X-User-Id header absent or non-integer)
        → 401 Unauthorized  (authentication is required on every non-bypassed endpoint)

 1. RequestedUserId != null
        → if AuthenticatedUserId != RequestedUserId
            → 403 Forbidden  (caller is accessing another user's resource)

 2. RequestedSiteId == null AND RequestedDiaryId == null
        → pass through (no site/diary context; caller is authenticated)

 3. RequestedSiteId != null
        → call ISiteAuthorizationService.IsUserMemberOfSiteAsync(userId, siteId)
        → false → 403 Forbidden
        → true  → next()

 4. RequestedSiteId == null AND RequestedDiaryId != null
        → call ISiteAuthorizationService.IsUserAuthorizedForDiaryAsync(userId, diaryId)
        → false → 403 Forbidden
        → true  → next()
```

The bypass check uses `HttpContext.GetEndpoint()?.Metadata` which is available after `UseRouting()` is called. Both `[SkipResourceAuthorization]` (custom, see §3.7) and the built-in `[AllowAnonymous]` / `IAllowAnonymous` are recognised so that the two mechanisms compose cleanly.

```
// Pseudocode
public async Task InvokeAsync(HttpContext ctx,
    IRequestSecurityContext secCtx,
    ISiteAuthorizationService authSvc)
{
    // Rule -1: bypass — endpoint explicitly opted out of resource authorization
    var endpoint = ctx.GetEndpoint();
    if (endpoint?.Metadata.GetMetadata<SkipResourceAuthorizationAttribute>() is not null
        || endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
    {
        await next(ctx);
        return;
    }

    // Rule 0: X-User-Id is required on ALL non-bypassed endpoints
    if (secCtx.AuthenticatedUserId is not { } userId)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    // Rule 1: userId route parameter must exactly match the authenticated user
    if (secCtx.RequestedUserId is { } routeUserId && userId != routeUserId)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    // Rule 2: no site/diary context → pass through (caller is authenticated)
    if (secCtx.RequestedSiteId is null && secCtx.RequestedDiaryId is null)
    {
        await next(ctx);
        return;
    }

    // Rule 3: siteId available → membership check
    if (secCtx.RequestedSiteId is { } siteId)
    {
        if (!await authSvc.IsUserMemberOfSiteAsync(userId, siteId, ct))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(ctx);
        return;
    }

    // Rule 4: diaryId only → diary-based check
    if (!await authSvc.IsUserAuthorizedForDiaryAsync(userId, secCtx.RequestedDiaryId!.Value, ct))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next(ctx);
}
```

---

### 3.7 Bypass Attribute — `SkipResourceAuthorizationAttribute`

**Layer:** `SiteDiary.Web.Middleware`

A lightweight marker attribute that signals to `ResourceAuthorizationMiddleware` that the endpoint is intentionally public and requires neither an `X-User-Id` header nor any membership check.

```csharp
// SiteDiary.Web/Middleware/SkipResourceAuthorizationAttribute.cs
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipResourceAuthorizationAttribute : Attribute { }
```

**Usage example:**

```csharp
[HttpGet("api/users")]
[SkipResourceAuthorization]   // ← marks this endpoint as publicly accessible
public IActionResult GetUsers() { ... }
```

**Design rationale:** A custom attribute is preferred over relying solely on `[AllowAnonymous]` because:
1. `[AllowAnonymous]` carries implicit semantics tied to ASP.NET Core authentication middleware which is not used here.
2. Having an explicit, domain-specific attribute makes intent clear during code review and avoids confusion if authentication middleware is added later.

`ResourceAuthorizationMiddleware` checks for **both** `SkipResourceAuthorizationAttribute` and the built-in `IAllowAnonymous` marker interface, so either decoration will bypass the middleware (§3.5 Rule -1).

---

### 3.6 Infrastructure Implementation — `SiteAuthorizationService`

**Layer:** `SiteDiary.Infrastructure/Services/SiteAuthorizationService.cs`

```
// IsUserMemberOfSiteAsync:
//   siteUserRepo.Query()
//     .Where(su => su.UserId == userId
//               && su.ConstructionSiteId == siteId
//               && !su.ConstructionSite.IsArchived)
//     .AnyAsync(ct)

// IsUserAuthorizedForDiaryAsync:
//   var diary = await diaryRepo.Query()
//                  .Where(d => d.Id == diaryId && !d.IsArchived)
//                  .Select(d => d.ConstructionSiteId)
//                  .FirstOrDefaultAsync(ct);
//   if (diary == 0) return false;   // diary not found
//   return await IsUserMemberOfSiteAsync(userId, diary, ct);
```

Both queries are read-only (`AsNoTracking()` implied by the generic repository's `Query()` pattern).

---

## 4. Updated `HttpContextKeys`

Add two new typed object keys (no string allocations, no collision risk):

```csharp
public static class HttpContextKeys
{
    public static readonly object UserId  = new();   // existing
    public static readonly object SiteId  = new();   // new
    public static readonly object DiaryId = new();   // new
}
```

`HttpContextExtensions` gets two new helpers:

```csharp
public static int? GetCurrentSiteId(this HttpContext ctx) => ...;
public static int? GetCurrentDiaryId(this HttpContext ctx) => ...;
```

These are optional helpers for controllers that need the values without injecting `IRequestSecurityContext`.

---

## 5. `Program.cs` Changes

```csharp
// Registration (builder phase)
builder.Services.AddScoped<IRequestSecurityContext, RequestSecurityContext>();
builder.Services.AddScoped<ISiteAuthorizationService, SiteAuthorizationService>();

// Pipeline (app phase) — replace XUserIdMiddleware
app.UseRouting();                                          // ← make explicit
app.UseMiddleware<RequestContextExtractionMiddleware>();   // ← replaces XUserIdMiddleware
app.UseMiddleware<ResourceAuthorizationMiddleware>();      // ← new
app.MapControllers();
```

---

## 6. Controller Simplification (Post-Implementation)

Once the middleware enforces the presence of `X-User-Id` for all site/diary-scoped routes, the per-action guard:

```csharp
if (HttpContext.GetCurrentUserId() is not { } userId)
    return BadRequest("X-User-Id header is required and must be a valid integer.");
```

can be replaced with the simpler (guaranteed non-null by the time the action runs):

```csharp
var userId = HttpContext.GetCurrentUserId()!.Value;
```

This simplification is **out of scope** for this PR — it should be done in a follow-up after tests are green.

---

## 7. TDD Test Plan

Tests continue the existing numbering (T35 is the last in `XUserIdMiddlewareTests`). New tests start at **T36**.

### 7.1 `RequestContextExtractionMiddlewareTests` (Unit) — `T36–T44`

| # | Scenario | Expected |
|---|---|---|
| T36 | Valid `X-User-Id` header | `secCtx.AuthenticatedUserId` set; `Items[UserId]` set; pipeline called |
| T37 | Missing header | `secCtx.AuthenticatedUserId` null; pipeline called |
| T38 | Non-integer header | `secCtx.AuthenticatedUserId` null; pipeline called |
| T39 | Route has `siteId` | `secCtx.RequestedSiteId` set |
| T40 | Route has `diaryId` | `secCtx.RequestedDiaryId` set |
| T41 | Route has both `siteId` and `diaryId` | Both set |
| T42 | Route has neither | Both null; pipeline called |
| T43 | Route has `userId` | `secCtx.RequestedUserId` set |
| T44 | All four present (header + siteId + diaryId + userId) | All four fields populated |

### 7.2 `ResourceAuthorizationMiddlewareTests` (Unit) — `T45–T60`

All tests mock `ISiteAuthorizationService` and `IRequestSecurityContext`.

| # | Scenario | Expected HTTP |
|---|---|---|
| T45 | Endpoint has `[SkipResourceAuthorization]`, no `X-User-Id` → bypass | pipeline called, no 401/403 |
| T46 | Endpoint has `[AllowAnonymous]`, no `X-User-Id` → bypass | pipeline called, no 401/403 |
| T47 | Endpoint has `[SkipResourceAuthorization]`, siteId present, no `X-User-Id` → bypass | pipeline called, no 401/403 |
| T48 | No bypass, no `X-User-Id`, no siteId/diaryId → global auth fails | 401 |
| T49 | No bypass, has `X-User-Id`, no siteId/diaryId, no routeUserId → pass through | pipeline called |
| T50 | siteId present, no `X-User-Id` header → global auth fails | 401 |
| T51 | siteId present, `X-User-Id` valid, member → pass through | pipeline called |
| T52 | siteId present, `X-User-Id` valid, NOT member → 403 | 403 |
| T53 | diaryId only, no `X-User-Id` header → global auth fails | 401 |
| T54 | diaryId only, `X-User-Id` valid, authorized → pass through | pipeline called |
| T55 | diaryId only, `X-User-Id` valid, NOT authorized → 403 | 403 |
| T56 | diaryId only, diary not found → 403 | 403 |
| T57 | siteId + diaryId present → uses siteId path (not diary path) | siteId check called, diary check NOT called |
| T58 | `userId` in route, `X-User-Id` matches → pass through (no siteId/diaryId) | pipeline called |
| T59 | `userId` in route, `X-User-Id` mismatches → 403 | 403 |
| T60 | `userId` in route, `X-User-Id` absent → global auth fails | 401 |

### 7.3 `SiteAuthorizationServiceTests` (Unit) — `T61–T65`

Tests mock `IRepository<SiteUser>` and `IRepository<Diary>`.

| # | Scenario | Expected |
|---|---|---|
| T61 | User IS a member of site (not archived) → true | `true` |
| T62 | User is NOT a member of site → false | `false` |
| T63 | Site is archived → false | `false` |
| T64 | Diary exists, user is member of its site → true | `true` |
| T65 | Diary does not exist (id not found) → false | `false` |

### 7.4 Integration Tests — `T66–T73`

Use `WebApplicationFactory` with an in-memory or SQLite DB. Seed: User A (member of Site 1), User B (not a member).

| # | Route | Header | Expected |
|---|---|---|---|
| T66 | `GET /api/sites/1/diaries` | `X-User-Id: <User A>` | 200 |
| T67 | `GET /api/sites/1/diaries` | `X-User-Id: <User B>` | 403 |
| T68 | `GET /api/sites/1/diaries` | *(absent)* | **401** |
| T69 | `POST /api/diaries/{diaryId}/attachments` | `X-User-Id: <User A>` (member) | passes auth, proceeds to action |
| T70 | `GET /api/users/1/sites` | `X-User-Id: 1` (matching) | passes auth, proceeds to action |
| T71 | `GET /api/users/1/sites` | `X-User-Id: 2` (mismatching) | 403 |
| T72 | `GET /api/users` *(decorated with `[SkipResourceAuthorization]`)* | *(absent)* | 200 |
| T73 | `GET /api/users` *(decorated with `[SkipResourceAuthorization]`)* | `X-User-Id: <User A>` | 200 |

---

## 8. File Map

| File | Action |
|---|---|
| `SiteDiary.Web/Middleware/IRequestSecurityContext.cs` | **New** |
| `SiteDiary.Web/Middleware/RequestSecurityContext.cs` | **New** |
| `SiteDiary.Web/Middleware/RequestContextExtractionMiddleware.cs` | **New** (replaces `XUserIdMiddleware`) |
| `SiteDiary.Web/Middleware/ResourceAuthorizationMiddleware.cs` | **New** |
| `SiteDiary.Web/Middleware/SkipResourceAuthorizationAttribute.cs` | **New** |
| `SiteDiary.Web/Middleware/HttpContextKeys.cs` | **Extend** (add `SiteId`, `DiaryId` keys) |
| `SiteDiary.Web/Middleware/HttpContextExtensions.cs` | **Extend** (`GetCurrentSiteId`, `GetCurrentDiaryId`) |
| `SiteDiary.Application/Interfaces/ISiteAuthorizationService.cs` | **New** |
| `SiteDiary.Infrastructure/Services/SiteAuthorizationService.cs` | **New** |
| `SiteDiary.Web/Program.cs` | **Modify** (add `UseRouting()`, swap middleware) |
| `SiteDiary.Tests/Unit/Web/Middleware/RequestContextExtractionMiddlewareTests.cs` | **New** |
| `SiteDiary.Tests/Unit/Web/Middleware/ResourceAuthorizationMiddlewareTests.cs` | **New** |
| `SiteDiary.Tests/Unit/Application/SiteAuthorizationServiceTests.cs` | **New** |
| `SiteDiary.Tests/Integration/SiteAuthorizationIntegrationTests.cs` | **New** |
| `SiteDiary.Web/Middleware/XUserIdMiddleware.cs` | **Delete** (absorbed into new extraction middleware) |

---

## 9. Non-Goals

- **Role-based authorization** (e.g., only `Admin` role can delete a diary) — this is a separate concern and belongs in the service layer or a future policy-based middleware.
- **JWT / OAuth authentication** — out of scope; `X-User-Id` is the trust boundary by design.
- **Rate limiting or audit logging** — separate middleware concerns.
- **Controller refactoring** (removing the `GetCurrentUserId()` null-guard) — deferred to follow-up PR.
