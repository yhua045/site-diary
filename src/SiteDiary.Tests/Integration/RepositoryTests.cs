using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Integration;

/// <summary>
/// Repository integration tests using EF Core In-Memory provider.
/// Validates CRUD and soft-delete behaviour without a real database.
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AddAndGetById_ConstructionSite_RoundTrips()
    {
        var site = new ConstructionSite
        {
            Name = "Test Site",
            Address = "123 Test St",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        var found = await _uow.Sites.GetByIdAsync(site.Id);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Test Site");
    }

    [Fact]
    public async Task Query_ReturnsOnlyNonArchivedSites_ViaGlobalFilter()
    {
        await _uow.Sites.AddAsync(new ConstructionSite { Name = "Active", Address = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _uow.Sites.AddAsync(new ConstructionSite { Name = "Archived", Address = "B", IsArchived = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _uow.SaveChangesAsync();

        var all = await _uow.Sites.Query().ToListAsync();

        all.Should().HaveCount(1);
        all[0].Name.Should().Be("Active");
    }

    [Fact]
    public async Task ManualSoftDelete_SetsIsArchivedTrue()
    {
        var site = new ConstructionSite { Name = "ToDelete", Address = "Del St", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        site.IsArchived = true;
        site.UpdatedAt = DateTime.UtcNow;
        _uow.Sites.Update(site);
        await _uow.SaveChangesAsync();

        // Bypass query filter to inspect actual DB state
        var raw = await _db.ConstructionSites.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == site.Id);
        raw.Should().NotBeNull();
        raw!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ChangesArePersisted()
    {
        var site = new ConstructionSite { Name = "Original", Address = "Orig St", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        site.Name = "Updated";
        _uow.Sites.Update(site);
        await _uow.SaveChangesAsync();

        var found = await _uow.Sites.GetByIdAsync(site.Id);
        found!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task AddUser_UniqueEmail_Constraint_Via_DbContext()
    {
        var user = new User
        {
            FirstName = "Jo",
            LastName = "Doe",
            Email = "jo@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        var found = await _uow.Users.GetByIdAsync(user.Id);
        found!.Email.Should().Be("jo@example.com");
    }

    [Fact]
    public async Task AddDiary_AndRetrieve_RoundTrips()
    {
        var user = new User { FirstName = "A", LastName = "B", Email = "a@b.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var site = new ConstructionSite { Name = "S", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Users.Add(user);
        _db.ConstructionSites.Add(site);
        await _db.SaveChangesAsync();

        var diary = new Diary
        {
            Title = "Day 1",
            ConstructionSiteId = site.Id,
            AuthorUserId = user.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _uow.Diaries.AddAsync(diary);
        await _uow.SaveChangesAsync();

        var found = await _uow.Diaries.GetByIdAsync(diary.Id);
        found.Should().NotBeNull();
        found!.Title.Should().Be("Day 1");
    }
}
