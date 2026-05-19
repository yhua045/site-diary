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
/// Integration tests for the Audit Log MVC page (GET /AuditLogs).
/// Uses an InMemory database seeded with a User and AuditHistory rows.
/// </summary>
public class AuditLogsControllerIntegrationTests : IClassFixture<AuditLogsWebFactory>
{
    private readonly AuditLogsWebFactory _factory;

    public AuditLogsControllerIntegrationTests(AuditLogsWebFactory factory)
    {
        _factory = factory;
    }

    // ── IT-AL01 — GET /AuditLogs returns 200 ─────────────────────────────────

    [Fact]
    public async Task ITAL01_GET_AuditLogs_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/AuditLogs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── IT-AL02 — Response HTML contains table rows ───────────────────────────

    [Fact]
    public async Task ITAL02_GET_AuditLogs_RendersTableRows()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/AuditLogs");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("<td");
        html.Should().Contain("Insert");
    }

    // ── IT-AL03 — Page 2 returns second row ──────────────────────────────────

    [Fact]
    public async Task ITAL03_GET_AuditLogs_Page2_ReturnsCorrectSubset()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/AuditLogs?page=2&pageSize=1");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Second page currently shows the older entry after the controller's ordering.
        html.Should().Contain("Insert");
        // The newer action should not appear on page 2.
        html.Should().NotContain("Update");
    }
}

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for audit log controller tests.
/// Registers an EF Core InMemory database and seeds two AuditHistory rows.
/// </summary>
public class AuditLogsWebFactory : WebApplicationFactory<Program>
{
    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs skips AddDbContext in Testing env — add InMemory here.
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase("AuditLogsIntegrationTests"));
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "1");

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

        var user = new User
        {
            Id = 1, FirstName = "Test", LastName = "User",
            Email = "test.user@example.com",
            IsActive = true, IsArchived = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var now = DateTime.UtcNow;

        db.AuditHistories.AddRange(
            new AuditHistory
            {
                Id = 1, EntityName = "Diary", EntityId = 1,
                Action = "Insert", ChangedByUserId = 1,
                Timestamp = now.AddHours(-1),
                CreatedAt = now.AddHours(-1), UpdatedAt = now.AddHours(-1)
            },
            new AuditHistory
            {
                Id = 2, EntityName = "Diary", EntityId = 2,
                Action = "Update", ChangedByUserId = 1,
                Changes = """[{"Property":"Title","OldValue":"Old","NewValue":"New"}]""",
                Timestamp = now,
                CreatedAt = now, UpdatedAt = now
            }
        );

        await db.SaveChangesAsync();
    }
}
