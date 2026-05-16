# AuditSaveChangesInterceptor Design

## 1. Overview
This document outlines the design for implementing an Entity Framework Core `SaveChangesInterceptor` to capture domain-level data mutations for audit logging, fulfilling part of Issue #7.

## 2. Core Requirements
- Automatically intercept EF Core `SaveChanges` and `SaveChangesAsync` operations.
- Capture the identity of the user making the change (`AuthenticatedUserId`).
- Extract modified entity states (Added, Modified, Deleted) and their relevant property changes.
- Output audit information using the application's structured logging mechanism.

## 3. Components

### A. User Context Abstraction (`ICurrentUserService`)
To provide the `AuthenticatedUserId` cleanly without coupling the data access layer to the HTTP layer, we will define an interface and its web implementation.

- **Interface:** `ICurrentUserService`
  - Property: `string? AuthenticatedUserId { get; }`
- **Implementation:** `CurrentUserService` 
  - Uses `IHttpContextAccessor` to retrieve the current user's claims and extract the user ID.

### B. `AuditSaveChangesInterceptor`
This interceptor will hook into EF Core's change tracker right before changes are committed to the database.

- **Location:** `src/SiteDiary.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`
- **Dependencies:** 
  - `ICurrentUserService` (to get the user ID)
  - `ILogger<AuditSaveChangesInterceptor>` (to emit the structured logs)
- **Behavior:**
  - Overrides `SavingChanges` and `SavingChangesAsync`.
  - Iterates over the `DbContext.ChangeTracker.Entries()`.
  - Filters for entities in `Added`, `Modified`, or `Deleted` states.
  - Constructs an audit event payload including:
    - Entity Name / Table
    - Primary Key
    - State (Insert/Update/Delete)
    - Modified Properties (Old Values & New Values)
    - `AuthenticatedUserId` from `ICurrentUserService`
    - Timestamp
  - Emits the payload via `ILogger`.

### C. Dependency Injection & Registration
Entity Framework Core interceptors that rely on scoped services (like `ICurrentUserService`) need to be registered carefully.

- Register `ICurrentUserService` and `CurrentUserService` in `Program.cs`.
- Register `AuditSaveChangesInterceptor` as a Scoped service.
- In the `AddDbContext` setup within `Program.cs`, resolve the interceptor from the `IServiceProvider` and attach it to the `DbContextOptionsBuilder` using `.AddInterceptors()`.

## 4. Testing Strategy
- **Unit Tests:** Test the `AuditSaveChangesInterceptor` logic with an in-memory or mocked `DbContext` and a mocked `ICurrentUserService` to ensure correct payload generation.
- **Integration Tests:** Add tests inside `src/SiteDiary.Tests/Integration` using a test database. Perform standard inserts and updates, and assert that the logging sink captures the correct `AuthenticatedUserId` and entity changes.