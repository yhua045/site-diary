using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;
using SiteDiary.Infrastructure.Services;

namespace SiteDiary.Tests.Unit.Application;

public class SiteAuthorizationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;
    private readonly SiteAuthorizationService _sut;

    public SiteAuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
        _sut = new SiteAuthorizationService(_uow);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(ConstructionSite site, User user, Role role)> SeedBasicAsync(bool siteArchived = false)
    {
        var site = new ConstructionSite
        {
            Name = "Test Site",
            Address = "1 Test St",
            IsArchived = siteArchived,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _db.ConstructionSites.AddAsync(site);

        var role = new Role { Name = "Worker", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Roles.AddAsync(role);

        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = $"jane{Guid.NewGuid():N}@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
        return (site, user, role);
    }

    private async Task LinkUserToSite(User user, ConstructionSite site, Role role)
    {
        var siteUser = new SiteUser
        {
            ConstructionSiteId = site.Id,
            UserId = user.Id,
            AssignedRoleId = role.Id,
            JoinedDate = DateOnly.FromDateTime(DateTime.Today)
        };
        await _db.SiteUsers.AddAsync(siteUser);
        await _db.SaveChangesAsync();
    }

    // ── T61: user IS a member of site (not archived) ──────────────────────────

    [Fact]
    public async Task T61_UserIsMemberOfActiveSite_ReturnsTrue()
    {
        var (site, user, role) = await SeedBasicAsync();
        await LinkUserToSite(user, site, role);

        var result = await _sut.IsUserMemberOfSiteAsync(user.Id, site.Id);

        result.Should().BeTrue();
    }

    // ── T62: user is NOT a member ────────────────────────────────────────────

    [Fact]
    public async Task T62_UserIsNotMemberOfSite_ReturnsFalse()
    {
        var (site, user, _) = await SeedBasicAsync();
        // No SiteUser created

        var result = await _sut.IsUserMemberOfSiteAsync(user.Id, site.Id);

        result.Should().BeFalse();
    }

    // ── T63: site is archived ────────────────────────────────────────────────

    [Fact]
    public async Task T63_SiteIsArchived_ReturnsFalse()
    {
        var (site, user, role) = await SeedBasicAsync(siteArchived: true);
        // Manually add SiteUser bypassing the global filter (use ChangeTracker)
        _db.SiteUsers.Add(new SiteUser
        {
            ConstructionSiteId = site.Id,
            UserId = user.Id,
            AssignedRoleId = role.Id,
            JoinedDate = DateOnly.FromDateTime(DateTime.Today)
        });
        await _db.SaveChangesAsync();

        // The query joins with Sites which has global filter !IsArchived
        var result = await _sut.IsUserMemberOfSiteAsync(user.Id, site.Id);

        result.Should().BeFalse();
    }

    // ── T64: diary exists, user is member of its site ────────────────────────

    [Fact]
    public async Task T64_DiaryExists_UserIsMemberOfItsSite_ReturnsTrue()
    {
        var (site, user, role) = await SeedBasicAsync();
        await LinkUserToSite(user, site, role);

        var diary = new Diary
        {
            ConstructionSiteId = site.Id,
            AuthorUserId = user.Id,
            Title = "Day 1",
            Date = DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _db.Diaries.AddAsync(diary);
        await _db.SaveChangesAsync();

        var result = await _sut.IsUserAuthorizedForDiaryAsync(user.Id, diary.Id);

        result.Should().BeTrue();
    }

    // ── T65: diary does not exist ────────────────────────────────────────────

    [Fact]
    public async Task T65_DiaryDoesNotExist_ReturnsFalse()
    {
        var result = await _sut.IsUserAuthorizedForDiaryAsync(userId: 1, diaryId: 9999);

        result.Should().BeFalse();
    }
}
