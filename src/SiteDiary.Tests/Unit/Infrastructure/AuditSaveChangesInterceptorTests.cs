using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Interceptors;

namespace SiteDiary.Tests.Unit.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AuditSaveChangesInterceptor"/> using the EF Core
/// InMemory provider. Validates that correct audit log entries are emitted for
/// the Added, Modified, and Deleted entity states.
/// </summary>
public class AuditSaveChangesInterceptorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(AuditSaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (AuditSaveChangesInterceptor interceptor, Mock<ILogger<AuditSaveChangesInterceptor>> loggerMock)
        BuildSut(string? userId = "42")
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns(userId);

        var loggerMock = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        var interceptor = new AuditSaveChangesInterceptor(userService.Object, loggerMock.Object);
        return (interceptor, loggerMock);
    }

    private static bool LogContains(string fragment) =>
        true; // helper placeholder — real check done inline via It.Is<>

    // ── T01 — Added entity logs "Insert" ─────────────────────────────────────

    [Fact]
    public async Task T01_SaveChangesAsync_Added_LogsInsertEvent()
    {
        var (sut, loggerMock) = BuildSut("42");
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "New Site", Address = "1 Main St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Insert") && v.ToString()!.Contains("ConstructionSite")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T02 — Modified entity logs "Update" ───────────────────────────────────

    [Fact]
    public async Task T02_SaveChangesAsync_Modified_LogsUpdateEvent()
    {
        var (sut, loggerMock) = BuildSut("42");
        await using var db = BuildContext(sut);

        var site = new ConstructionSite
        {
            Name = "Original", Address = "2 Second St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.ConstructionSites.Add(site);
        await db.SaveChangesAsync();

        site.Name = "Updated";
        db.ConstructionSites.Update(site);
        await db.SaveChangesAsync();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Update") && v.ToString()!.Contains("ConstructionSite")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T03 — Deleted entity logs "Delete" ────────────────────────────────────

    [Fact]
    public async Task T03_SaveChangesAsync_Deleted_LogsDeleteEvent()
    {
        var (sut, loggerMock) = BuildSut("42");
        await using var db = BuildContext(sut);

        var site = new ConstructionSite
        {
            Name = "ToDelete", Address = "3 Third St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.ConstructionSites.Add(site);
        await db.SaveChangesAsync();

        db.ConstructionSites.Remove(site);
        await db.SaveChangesAsync();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Delete") && v.ToString()!.Contains("ConstructionSite")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T04 — UserId is included in the log message ───────────────────────────

    [Fact]
    public async Task T04_SaveChangesAsync_LogsAuthenticatedUserId()
    {
        var (sut, loggerMock) = BuildSut("99");
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Audit Site", Address = "4 Fourth St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("99")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T05 — Null UserId falls back to "anonymous" ───────────────────────────

    [Fact]
    public async Task T05_SaveChangesAsync_NullUserId_UsesAnonymousFallback()
    {
        var (sut, loggerMock) = BuildSut(null);
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Anon Site", Address = "5 Fifth St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("anonymous")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T06 — Synchronous SaveChanges also logs ───────────────────────────────

    [Fact]
    public void T06_SaveChanges_Sync_LogsInsertEvent()
    {
        var (sut, loggerMock) = BuildSut("7");
        using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Sync Site", Address = "6 Sixth Ave",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Insert")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T07 — No changed entities → no log calls ─────────────────────────────

    [Fact]
    public async Task T07_SaveChangesAsync_NoChanges_DoesNotLog()
    {
        var (sut, loggerMock) = BuildSut("42");
        await using var db = BuildContext(sut);

        await db.SaveChangesAsync(); // nothing tracked

        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    // ── T08 — Primary key is captured in the log ──────────────────────────────

    [Fact]
    public async Task T08_SaveChangesAsync_Added_LogsEntityPrimaryKey()
    {
        var (sut, loggerMock) = BuildSut("5");
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "PK Site", Address = "7 Seventh Rd",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // The log should contain the primary key label "Id="
        loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Id=")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── T09 — Interceptor writes AuditHistory row to DB for Added entity ──────

    [Fact]
    public async Task T09_SavingChanges_AddsAuditHistoryRow_ForAddedEntity()
    {
        var (sut, _) = BuildSut("42");
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Site for Audit", Address = "7 Seventh Ave",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.AuditHistories.Should().HaveCount(1);
        db.AuditHistories.First().Action.Should().Be("Insert");
    }

    [Fact]
    public async Task T09b_SavingChanges_AddsInsertedValues_ToAuditHistoryRow()
    {
        var (sut, _) = BuildSut("42");
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Site for Insert Values",
            Address = "8 Eighth Ave",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var auditRow = db.AuditHistories.Single();
        auditRow.Action.Should().Be("Insert");
        auditRow.Changes.Should().Contain("Site for Insert Values");
        auditRow.Changes.Should().Contain("8 Eighth Ave");
    }

    // ── T10 — Interceptor skips AuditHistory entities (self-audit guard) ──────

    [Fact]
    public async Task T10_SavingChanges_SkipsAuditHistoryEntities()
    {
        var (sut, _) = BuildSut("42");
        await using var db = BuildContext(sut);

        db.AuditHistories.Add(new AuditHistory
        {
            EntityName = "Test", EntityId = 1, Action = "Insert",
            ChangedByUserId = null, Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Self-audit guard: the interceptor must not create a second AuditHistory row
        db.AuditHistories.Should().HaveCount(1);
    }

    // ── T11 — Null user ID → AuditHistory.ChangedByUserId is null ────────────

    [Fact]
    public async Task T11_SavingChanges_SetsNullChangedByUserId_WhenUserIsAnonymous()
    {
        var (sut, _) = BuildSut(null);
        await using var db = BuildContext(sut);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Anon Site", Address = "8 Eighth St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var auditRow = db.AuditHistories.Single();
        auditRow.ChangedByUserId.Should().BeNull();
    }

    // ── T12 — Modified entity → AuditHistory.Changes contains valid JSON ──────

    [Fact]
    public async Task T12_SavingChanges_SerializesChangedProperties_ForModifiedEntity()
    {
        var (sut, _) = BuildSut("42");
        await using var db = BuildContext(sut);

        var site = new ConstructionSite
        {
            Name = "Original", Address = "9 Ninth St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.ConstructionSites.Add(site);
        await db.SaveChangesAsync();

        site.Name = "Updated Name";
        db.ConstructionSites.Update(site);
        await db.SaveChangesAsync();

        var updateRow = db.AuditHistories.First(a => a.Action == "Update");
        updateRow.Changes.Should().NotBeNull();
        var act = () => System.Text.Json.JsonDocument.Parse(updateRow.Changes!);
        act.Should().NotThrow();
    }
}
