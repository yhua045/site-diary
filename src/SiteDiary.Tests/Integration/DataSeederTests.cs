using Microsoft.EntityFrameworkCore;
using Xunit;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;

namespace SiteDiary.Tests.Integration;

public class DataSeederTests
{
    [Fact]
    public async Task SeedAsync_ShouldSeedInitialData_WhenDatabaseIsEmpty()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        await using var context = new ApplicationDbContext(options);
        
        // Act
        await context.SeedAsync();
        
        // Assert
        var sitesCount = await context.ConstructionSites.CountAsync();
        Assert.Equal(2, sitesCount);

        var rolesCount = await context.Roles.CountAsync();
        Assert.Equal(5, rolesCount);
        
        var expectedRoles = new[] { "Project Manager", "Site Manager", "Safety Manager", "Site Foreman", "Construction Worker" };
        var actualRoles = await context.Roles.Select(r => r.Name).ToListAsync();
        foreach (var role in expectedRoles)
        {
            Assert.Contains(role, actualRoles);
        }

        var usersCount = await context.Users.CountAsync();
        Assert.Equal(5, usersCount);
    }

    [Fact]
    public async Task SeedAsync_ShouldNotDoubleSeed_WhenDataAlreadyExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        await using var context = new ApplicationDbContext(options);
        
        // Seed once
        await context.SeedAsync();
        
        // Act - Seed twice
        await context.SeedAsync();
        
        // Assert - counts should not double
        var sitesCount = await context.ConstructionSites.CountAsync();
        Assert.Equal(2, sitesCount);

        var rolesCount = await context.Roles.CountAsync();
        Assert.Equal(5, rolesCount);

        var usersCount = await context.Users.CountAsync();
        Assert.Equal(5, usersCount);
    }
}