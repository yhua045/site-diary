using Microsoft.EntityFrameworkCore;
using Xunit;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;

namespace SiteDiary.Tests.Integration;

public class DataSeederTests
{
    private static DbContextOptions<ApplicationDbContext> NewOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task SeedAsync_ShouldSeedInitialData_WhenDatabaseIsEmpty()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

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

        var siteUsers = await context.Set<SiteUser>().ToListAsync();
        Assert.Equal(5, siteUsers.Count);
        // Expect 2 users assigned to first site, 3 to second
        var siteAssignments = siteUsers.GroupBy(su => su.ConstructionSiteId).Select(g => g.Count()).OrderBy(c => c).ToList();
        Assert.Equal(2, siteAssignments[0]);
        Assert.Equal(3, siteAssignments[1]);
    }

    // ── Issue #6: role-based template seeding ────────────────────────────────

    [Fact]
    public async Task SeedAsync_ShouldSeed6Templates()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — 5 role-specific + 1 fallback
        var count = await context.DiaryTemplates.CountAsync();
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task SeedAsync_ShouldLinkEachRoleToItsTemplate()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — each of the 5 roles has exactly one template with matching RoleId
        var roles = await context.Roles.ToListAsync();
        foreach (var role in roles)
        {
            var count = await context.DiaryTemplates.CountAsync(t => t.RoleId == role.Id);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task SeedAsync_FallbackTemplate_IsDefault()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — exactly one template is default with no role binding
        var count = await context.DiaryTemplates.CountAsync(t => t.IsDefault && t.RoleId == null);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SeedAsync_RoleTemplates_AreNotDefault()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — 5 role-specific templates must all have IsDefault=false
        var nonDefaultCount = await context.DiaryTemplates.CountAsync(t => !t.IsDefault);
        Assert.Equal(5, nonDefaultCount);
    }

    [Fact]
    public async Task SeedAsync_ShouldNotDoubleSeed_WhenDataAlreadyExists()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

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

        var templatesCount = await context.DiaryTemplates.CountAsync();
        Assert.Equal(6, templatesCount);
    }

    [Fact]
    public async Task SeedAsync_AllTemplates_HaveFileAttachmentField()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — every template's Sections JSON contains a file_attachment field
        var templates = await context.DiaryTemplates.ToListAsync();
        Assert.All(templates, t =>
            Assert.Contains("file_attachment", t.Sections));
    }

    [Fact]
    public async Task SeedAsync_AllTemplates_HaveFieldsSectionJson()
    {
        // Arrange
        await using var context = new ApplicationDbContext(NewOptions());

        // Act
        await context.SeedAsync();

        // Assert — every template's Sections JSON contains at least one field definition
        var templates = await context.DiaryTemplates.ToListAsync();
        Assert.All(templates, t =>
            Assert.Contains("\"fields\"", t.Sections));
    }
}