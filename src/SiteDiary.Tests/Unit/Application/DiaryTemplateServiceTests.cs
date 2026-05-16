using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// TDD tests for DiaryTemplateService using EF Core InMemory.
/// </summary>
public class DiaryTemplateServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;

    public DiaryTemplateServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    private DiaryTemplate MakeTemplate(int id, string sections = "[]", bool isArchived = false) => new()
    {
        Id = id,
        Name = $"Template {id}",
        Sections = sections,
        IsDefault = false,
        CreatedByUserId = 1,       // InMemory: FK not enforced
        IsArchived = isArchived,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingTemplate_ReturnsDtoWithDeserializedSections()
    {
        // Arrange
        const string sectionsJson =
            """
            [
              {
                "id": "s1",
                "label": "General",
                "fields": [
                  { "id": "f1", "label": "Notes", "type": "textarea", "required": false }
                ]
              }
            ]
            """;

        var template = MakeTemplate(1, sectionsJson);
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Template 1");
        result.Sections.Should().HaveCount(1);
        result.Sections[0].Id.Should().Be("s1");
        result.Sections[0].Label.Should().Be("General");
        result.Sections[0].Fields.Should().HaveCount(1);
        result.Sections[0].Fields[0].Id.Should().Be("f1");
        result.Sections[0].Fields[0].Type.Should().Be("textarea");
    }

    [Fact]
    public async Task GetByIdAsync_EmptySections_ReturnsDtoWithEmptyList()
    {
        // Arrange
        var template = MakeTemplate(2, "[]");
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByIdAsync(2);

        // Assert
        result.Should().NotBeNull();
        result!.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ArchivedTemplate_ReturnsNull()
    {
        // Arrange — global query filter on DiaryTemplate excludes IsArchived=true
        var archived = MakeTemplate(3, isArchived: true);
        await _db.Set<DiaryTemplate>().AddAsync(archived);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByIdAsync(3);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_MissingTemplate_ReturnsNull()
    {
        var service = new DiaryTemplateService(_uow);

        var result = await service.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_TemplateWithSelectField_DeserializesOptions()
    {
        // Arrange
        const string sectionsJson =
            """
            [
              {
                "id": "s1",
                "label": "Weather",
                "fields": [
                  {
                    "id": "f_weather",
                    "label": "Condition",
                    "type": "select",
                    "required": true,
                    "options": ["Sunny", "Rainy", "Cloudy"]
                  }
                ]
              }
            ]
            """;

        var template = MakeTemplate(4, sectionsJson);
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByIdAsync(4);

        // Assert
        result.Should().NotBeNull();
        var field = result!.Sections[0].Fields[0];
        field.Type.Should().Be("select");
        field.Required.Should().BeTrue();
        field.Options.Should().BeEquivalentTo(["Sunny", "Rainy", "Cloudy"]);
    }

    // ── GetByUserRoleAsync tests ──────────────────────────────────────────────

    private DiaryTemplate MakeDefaultTemplate(int id, string sections = "[]") => new()
    {
        Id = id,
        Name = $"Default Template {id}",
        Sections = sections,
        IsDefault = true,
        CreatedByUserId = 1,
        IsArchived = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task GetByUserRoleAsync_WithDefaultTemplate_ReturnsDtoForAnyUserId()
    {
        // Arrange — a default template exists; userId doesn't matter for POC
        var template = MakeDefaultTemplate(10, """[{"id":"s1","label":"General","fields":[]}]""");
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByUserRoleAsync(userId: 999);

        // Assert — role-resolved by API regardless of userId for POC
        result.Should().NotBeNull();
        result!.Id.Should().Be(10);
        result.Name.Should().Be("Default Template 10");
        result.Sections.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByUserRoleAsync_NoDefaultTemplate_ReturnsNull()
    {
        // Arrange — only non-default templates exist
        var nonDefault = MakeTemplate(20);
        await _db.Set<DiaryTemplate>().AddAsync(nonDefault);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByUserRoleAsync(userId: 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserRoleAsync_ArchivedDefaultTemplate_ReturnsNull()
    {
        // Arrange — the default template is archived; global filter excludes it
        var archived = new DiaryTemplate
        {
            Id = 30,
            Name = "Archived Default",
            Sections = "[]",
            IsDefault = true,
            CreatedByUserId = 1,
            IsArchived = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await _db.Set<DiaryTemplate>().AddAsync(archived);
        await _db.SaveChangesAsync();

        var service = new DiaryTemplateService(_uow);

        // Act
        var result = await service.GetByUserRoleAsync(userId: 1);

        // Assert
        result.Should().BeNull();
    }
}
