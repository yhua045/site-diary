using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Services;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// Application-layer unit tests for SiteService using EF Core In-Memory database.
/// Validates business logic without mocking repositories.
/// </summary>
public class SiteServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;

    public SiteServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        // Arrange
        var sites = new List<ConstructionSite>
        {
            new() { Name = "Site Alpha", Address = "1 Alpha St", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Name = "Site Beta",  Address = "2 Beta Rd",  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        foreach (var site in sites)
            await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        var service = new SiteService(_uow);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Site Alpha");
        result[1].Name.Should().Be("Site Beta");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingSite_ReturnsDto()
    {
        // Arrange
        var site = new ConstructionSite { Name = "Alpha", Address = "1 St", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        var service = new SiteService(_uow);

        // Act
        var result = await service.GetByIdAsync(site.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_MissingSite_ReturnsNull()
    {
        var service = new SiteService(_uow);

        var result = await service.GetByIdAsync(99);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_AddsAndReturnsSite()
    {
        var service = new SiteService(_uow);
        var request = new CreateConstructionSiteRequest("New Site", "A description", "10 New St");

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Name.Should().Be("New Site");
        result.Address.Should().Be("10 New St");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify it was actually saved
        var fetched = await _uow.Sites.GetByIdAsync(result.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("New Site");
    }

    [Fact]
    public async Task UpdateAsync_ExistingSite_UpdatesAndReturns()
    {
        // Arrange
        var site = new ConstructionSite { Name = "Old", Address = "Old St", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        var service = new SiteService(_uow);

        // Act
        var result = await service.UpdateAsync(site.Id, new UpdateConstructionSiteRequest("New Name", null, "New St"));

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Address.Should().Be("New St");
    }

    [Fact]
    public async Task UpdateAsync_MissingSite_ReturnsNull()
    {
        var service = new SiteService(_uow);

        var result = await service.UpdateAsync(99, new UpdateConstructionSiteRequest("X", null, "Y"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_ExistingSite_SoftDeletesAndReturnsTrue()
    {
        // Arrange
        var site = new ConstructionSite { Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _uow.Sites.AddAsync(site);
        await _uow.SaveChangesAsync();

        var service = new SiteService(_uow);

        // Act
        var result = await service.ArchiveAsync(site.Id);

        // Assert
        result.Should().BeTrue();

        // Verify soft-delete
        var archived = await _uow.Sites.GetByIdAsync(site.Id);
        archived.Should().NotBeNull();
        archived!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_MissingSite_ReturnsFalse()
    {
        var service = new SiteService(_uow);

        var result = await service.ArchiveAsync(99);

        result.Should().BeFalse();
    }
}
