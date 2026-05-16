using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Interceptors;

namespace SiteDiary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="AuditSaveChangesInterceptor"/> wired into
/// an EF Core InMemory DbContext. Validates end-to-end audit logging for the
/// three entity state transitions (Insert / Update / Delete) and verifies that
/// the <c>AuthenticatedUserId</c> surface-up correctly in the log output.
/// </summary>
public class AuditInterceptorIntegrationTests
{
    // ── Factory ───────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildDb(
        ICurrentUserService userService,
        ILogger<AuditSaveChangesInterceptor> logger)
    {
        var interceptor = new AuditSaveChangesInterceptor(userService, logger);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new ApplicationDbContext(options);
    }

    // ── IT01 — Insert emits an audit log containing the user ID ──────────────

    [Fact]
    public async Task IT01_Insert_EmitsAuditLog_WithCorrectUserIdAndInsertState()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns("user-123");
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        db.Users.Add(new User
        {
            FirstName = "Alice", LastName = "Smith", Email = "alice@example.com",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("user-123") && v.ToString()!.Contains("Insert")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── IT02 — Update emits an audit log with changed-property context ────────

    [Fact]
    public async Task IT02_Update_EmitsAuditLog_WithUpdateStateAndUserId()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns("user-456");
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        var user = new User
        {
            FirstName = "Bob", LastName = "Jones", Email = "bob@example.com",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.FirstName = "Robert";
        db.Users.Update(user);
        await db.SaveChangesAsync();

        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("Update") && v.ToString()!.Contains("user-456")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── IT03 — Delete emits an audit log with Delete state ───────────────────

    [Fact]
    public async Task IT03_Delete_EmitsAuditLog_WithDeleteState()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns("user-789");
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        var site = new ConstructionSite
        {
            Name = "To Remove", Address = "99 Last St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.ConstructionSites.Add(site);
        await db.SaveChangesAsync();

        db.ConstructionSites.Remove(site);
        await db.SaveChangesAsync();

        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("Delete") && v.ToString()!.Contains("user-789")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── IT04 — Empty SaveChanges produces no audit log ────────────────────────

    [Fact]
    public async Task IT04_EmptySaveChanges_ProducesNoAuditLog()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns("user-000");
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        await db.SaveChangesAsync(); // nothing tracked

        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    // ── IT05 — Unauthenticated request falls back to "anonymous" ─────────────

    [Fact]
    public async Task IT05_NullUserId_AuditLogContainsAnonymous()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns((string?)null);
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Anon Insert", Address = "0 Zero St",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("anonymous")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── IT06 — Multiple entities in one save all get logged ──────────────────

    [Fact]
    public async Task IT06_MultipleEntitiesInOneSave_EachGetsAuditEntry()
    {
        var userService = new Mock<ICurrentUserService>();
        userService.Setup(x => x.AuthenticatedUserId).Returns("bulk-user");
        var logger = new Mock<ILogger<AuditSaveChangesInterceptor>>();

        await using var db = BuildDb(userService.Object, logger.Object);

        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Site A", Address = "Addr A",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.ConstructionSites.Add(new ConstructionSite
        {
            Name = "Site B", Address = "Addr B",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Two entries → two log calls
        logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Insert")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(2));
    }
}
