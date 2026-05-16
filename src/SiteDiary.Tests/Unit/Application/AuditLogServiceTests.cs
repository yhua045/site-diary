using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.AuditLogs;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="AuditLogService"/> using the EF Core InMemory provider.
/// Validates pagination, ordering, and user name resolution.
/// </summary>
public class AuditLogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
        _sut = new AuditLogService(_uow);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static User MakeUser(int id, string first, string last) => new()
    {
        Id = id, FirstName = first, LastName = last,
        Email = $"{first.ToLower()}.{last.ToLower()}@test.com",
        IsActive = true, IsArchived = false,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static AuditHistory MakeAuditHistory(int id, string action, DateTime timestamp,
        int? changedByUserId = 1) => new()
    {
        Id = id,
        EntityName = "Diary",
        EntityId = id,
        Action = action,
        ChangedByUserId = changedByUserId,
        Changes = action == "Update" ? """[{"Property":"Title","OldValue":"A","NewValue":"B"}]""" : null,
        Timestamp = timestamp,
        CreatedAt = timestamp,
        UpdatedAt = timestamp
    };

    // ── AL01 — Results are ordered oldest → newest ────────────────────────────

    [Fact]
    public async Task AL01_GetPageAsync_ReturnsItemsOrderedByTimestampAscending()
    {
        var user = MakeUser(1, "Alice", "Smith");
        _db.Users.Add(user);
        var now = DateTime.UtcNow;
        _db.AuditHistories.AddRange(
            MakeAuditHistory(1, "Insert", now.AddHours(-2)),
            MakeAuditHistory(2, "Update", now.AddHours(-1)),
            MakeAuditHistory(3, "Delete", now));
        await _db.SaveChangesAsync();

        var result = await _sut.GetPageAsync(1, 10);

        result.Items.Should().HaveCount(3);
        result.Items[0].Timestamp.Should().BeBefore(result.Items[1].Timestamp);
        result.Items[1].Timestamp.Should().BeBefore(result.Items[2].Timestamp);
    }

    // ── AL02 — Paging skips the correct rows ─────────────────────────────────

    [Fact]
    public async Task AL02_GetPageAsync_ReturnsCorrectPage()
    {
        var user = MakeUser(1, "Bob", "Jones");
        _db.Users.Add(user);
        var now = DateTime.UtcNow;
        _db.AuditHistories.AddRange(
            MakeAuditHistory(1, "Insert", now.AddHours(-3)),
            MakeAuditHistory(2, "Update", now.AddHours(-2)),
            MakeAuditHistory(3, "Delete", now.AddHours(-1)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetPageAsync(page: 2, pageSize: 2);

        result.Items.Should().HaveCount(1);
        result.Items[0].EntityId.Should().Be(3);
    }

    // ── AL03 — TotalCount reflects all rows regardless of page ───────────────

    [Fact]
    public async Task AL03_GetPageAsync_ReturnsTotalCount()
    {
        var user = MakeUser(1, "Carol", "White");
        _db.Users.Add(user);
        var now = DateTime.UtcNow;
        _db.AuditHistories.AddRange(
            MakeAuditHistory(1, "Insert", now.AddHours(-2)),
            MakeAuditHistory(2, "Update", now.AddHours(-1)),
            MakeAuditHistory(3, "Delete", now));
        await _db.SaveChangesAsync();

        var result = await _sut.GetPageAsync(page: 1, pageSize: 1);

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(1);
    }

    // ── AL04 — ChangedByUserName populated from navigation ───────────────────

    [Fact]
    public async Task AL04_GetPageAsync_IncludesChangedByUserName()
    {
        var user = MakeUser(1, "Dave", "Brown");
        _db.Users.Add(user);
        var now = DateTime.UtcNow;
        _db.AuditHistories.Add(MakeAuditHistory(1, "Insert", now, changedByUserId: 1));
        await _db.SaveChangesAsync();

        var result = await _sut.GetPageAsync(1, 10);

        result.Items.Should().HaveCount(1);
        result.Items[0].ChangedByUserName.Should().Be("Dave Brown");
    }
}
