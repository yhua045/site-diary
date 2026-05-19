using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;

namespace SiteDiary.Tests.Integration;

/// <summary>
/// Integration tests for security middleware (T66–T73).
/// Uses an in-memory database seeded with User A (member of Site 1) and User B (not a member).
/// </summary>
public class SiteAuthorizationIntegrationTests : IClassFixture<SiteAuthorizationWebFactory>
{
    private readonly SiteAuthorizationWebFactory _factory;

    public SiteAuthorizationIntegrationTests(SiteAuthorizationWebFactory factory)
    {
        _factory = factory;
    }

    // ── T66: User A (member) GET /api/sites/1/diaries → 200 ─────────────────

    [Fact]
    public async Task T66_GetSiteDiaries_UserAMember_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserAId.ToString());

        var response = await client.GetAsync($"/api/sites/{_factory.SiteId}/diaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── T67: User B (non-member) GET /api/sites/1/diaries → 403 ─────────────

    [Fact]
    public async Task T67_GetSiteDiaries_UserBNotMember_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserBId.ToString());

        var response = await client.GetAsync($"/api/sites/{_factory.SiteId}/diaries");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── T68: No header → 401 ────────────────────────────────────────────────

    [Fact]
    public async Task T68_GetSiteDiaries_NoHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/sites/{_factory.SiteId}/diaries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── T69: POST /api/diaries/{diaryId}/attachments — User A passes auth ───

    [Fact]
    public async Task T69_UploadAttachment_UserAMember_PassesAuth()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserAId.ToString());

        // Send a minimal multipart request — we only care that the middleware does NOT return 401/403
        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync($"/api/diaries/{_factory.DiaryId}/attachments", content);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── T70: GET /api/users/{userId}/sites — matching X-User-Id → passes auth

    [Fact]
    public async Task T70_GetUserSites_MatchingUserId_PassesAuth()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserAId.ToString());

        var response = await client.GetAsync($"/api/users/{_factory.UserAId}/sites");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── T71: GET /api/users/{userId}/sites — mismatching X-User-Id → 403 ───

    [Fact]
    public async Task T71_GetUserSites_MismatchingUserId_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserBId.ToString());

        var response = await client.GetAsync($"/api/users/{_factory.UserAId}/sites");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── T72: GET /api/users — no header → 200 (SkipResourceAuthorization) ───

    [Fact]
    public async Task T72_GetUsers_NoHeader_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── T73: GET /api/users — with header → 200 (SkipResourceAuthorization) ─

    [Fact]
    public async Task T73_GetUsers_WithHeader_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", _factory.UserAId.ToString());

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>
/// Custom WebApplicationFactory that replaces SQL Server with EF Core InMemory
/// and seeds the test data once per test class.
/// </summary>
public class SiteAuthorizationWebFactory : WebApplicationFactory<Program>
{
    public int SiteId { get; private set; }
    public int UserAId { get; private set; }
    public int UserBId { get; private set; }
    public int DiaryId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs skips AddDbContext<ApplicationDbContext> in Testing env,
            // so we only need to add the InMemory one — no provider conflict.
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase("SiteAuthIntegrationTests"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    // Seed once when factory is first used
    private bool _seeded;

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        if (!_seeded)
        {
            SeedAsync().GetAwaiter().GetResult();
            _seeded = true;
        }
        return client;
    }

    private async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed role
        var role = new Role { Name = "Worker", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Roles.Add(role);

        // Seed Site 1
        var site = new ConstructionSite
        {
            Name = "Integration Test Site",
            Address = "1 Integration Rd",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ConstructionSites.Add(site);

        // Seed User A
        var userA = new User
        {
            FirstName = "Alice",
            LastName = "Member",
            Email = "alice@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(userA);

        // Seed User B
        var userB = new User
        {
            FirstName = "Bob",
            LastName = "Stranger",
            Email = "bob@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(userB);

        await db.SaveChangesAsync();

        // Link User A to Site
        db.SiteUsers.Add(new SiteUser
        {
            ConstructionSiteId = site.Id,
            UserId = userA.Id,
            AssignedRoleId = role.Id,
            JoinedDate = DateOnly.FromDateTime(DateTime.Today)
        });

        // Seed a diary owned by Site (for T69)
        var diary = new Diary
        {
            ConstructionSiteId = site.Id,
            AuthorUserId = userA.Id,
            Title = "Integration Diary",
            Date = DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Diaries.Add(diary);

        await db.SaveChangesAsync();

        SiteId = site.Id;
        UserAId = userA.Id;
        UserBId = userB.Id;
        DiaryId = diary.Id;
    }
}
