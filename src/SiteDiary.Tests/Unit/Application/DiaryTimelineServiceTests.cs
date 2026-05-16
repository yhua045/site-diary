using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// Phase 3 TDD tests for the DiaryTimeline feature.
/// Tests GetTimelineAsync and the TemplateSnapshot embedding in CreateAsync.
/// </summary>
public class DiaryTimelineServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;

    public DiaryTimelineServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Role MakeRole(int id, string name) => new()
    {
        Id = id, Name = name,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User MakeUser(int id, string first, string last, Role? role = null)
    {
        var user = new User
        {
            Id = id, FirstName = first, LastName = last,
            Email = $"{first.ToLower()}@test.com",
            IsActive = true, IsArchived = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        if (role is not null)
        {
            user.UserRoles = new List<UserRole>
            {
                new()
                {
                    Id = id * 100, UserId = id, RoleId = role.Id,
                    Role = role, AssignedAt = DateTime.UtcNow, IsActive = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            };
        }
        return user;
    }

    private static ConstructionSite MakeSite(int id) => new()
    {
        Id = id, Name = $"Site {id}", Address = "Test Addr",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Diary MakeDiary(int id, int siteId, User author, bool isArchived = false,
        string? payload = null, string? templateSnapshot = null) => new()
    {
        Id = id, ConstructionSiteId = siteId, AuthorUserId = author.Id,
        Author = author,
        Title = $"Diary {id}", Content = "Content",
        Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
        IsPublished = false, IsArchived = isArchived,
        Payload = payload, TemplateSnapshot = templateSnapshot,
        Attachments = new List<Attachment>(),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    // ── GetTimelineAsync tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTimeline_ReturnsEntriesNewestFirst_ForNonArchivedDiaries()
    {
        var site = MakeSite(1);
        var role = MakeRole(1, "Foreman");
        var author = MakeUser(10, "John", "Smith", role);
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<Role>().AddAsync(role);
        await _db.Set<User>().AddAsync(author);

        var diaryOld = MakeDiary(1, siteId: 1, author);
        var diaryNew = new Diary
        {
            Id = 2, ConstructionSiteId = 1, AuthorUserId = author.Id, Author = author,
            Title = "Diary 2", Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            IsPublished = false, IsArchived = false,
            Attachments = new List<Attachment>(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var diaryArchived = MakeDiary(3, siteId: 1, author, isArchived: true);

        await _db.Set<Diary>().AddRangeAsync(diaryOld, diaryNew, diaryArchived);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);
        var result = await service.GetTimelineAsync(siteId: 1);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2, because: "newest entry should be first");
        result[1].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetTimeline_IncludesAuthorNameAndRole()
    {
        var site = MakeSite(1);
        var role = MakeRole(1, "Site Manager");
        var author = MakeUser(10, "Jane", "Doe", role);
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<Role>().AddAsync(role);
        await _db.Set<User>().AddAsync(author);
        await _db.Set<Diary>().AddAsync(MakeDiary(1, siteId: 1, author));
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);
        var result = await service.GetTimelineAsync(siteId: 1);

        result.Should().HaveCount(1);
        result[0].AuthorName.Should().Be("Jane Doe");
        result[0].AuthorRole.Should().Be("Site Manager");
    }

    [Fact]
    public async Task GetTimeline_ParsesPayloadAndTemplateSnapshot_WhenPresent()
    {
        var site = MakeSite(1);
        var role = MakeRole(1, "Foreman");
        var author = MakeUser(10, "John", "Smith", role);
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<Role>().AddAsync(role);
        await _db.Set<User>().AddAsync(author);

        var payloadJson = """{"weather":"Sunny","workers":12}""";
        var snapshotJson = """[{"Key":"weather","Label":"Weather","Type":"text","DisplayOrder":1}]""";
        var diary = MakeDiary(1, siteId: 1, author, payload: payloadJson, templateSnapshot: snapshotJson);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);
        var result = await service.GetTimelineAsync(siteId: 1);

        result.Should().HaveCount(1);
        result[0].Payload.ValueKind.Should().Be(JsonValueKind.Object);
        result[0].Payload.GetProperty("weather").GetString().Should().Be("Sunny");
        result[0].TemplateSnapshot.Should().HaveCount(1);
        result[0].TemplateSnapshot[0].Key.Should().Be("weather");
        result[0].TemplateSnapshot[0].Label.Should().Be("Weather");
    }

    [Fact]
    public async Task GetTimeline_ReturnsEmptyPayloadAndSnapshot_WhenNull()
    {
        var site = MakeSite(1);
        var role = MakeRole(1, "Foreman");
        var author = MakeUser(10, "John", "Smith", role);
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<Role>().AddAsync(role);
        await _db.Set<User>().AddAsync(author);
        await _db.Set<Diary>().AddAsync(MakeDiary(1, siteId: 1, author));
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);
        var result = await service.GetTimelineAsync(siteId: 1);

        result.Should().HaveCount(1);
        result[0].Payload.ValueKind.Should().Be(JsonValueKind.Object);
        result[0].TemplateSnapshot.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimeline_IncludesAttachments()
    {
        var site = MakeSite(1);
        var role = MakeRole(1, "Foreman");
        var author = MakeUser(10, "John", "Smith", role);
        var attachment = new Attachment
        {
            Id = 5, FileName = "photo.jpg", FileUrl = "/uploads/photo.jpg",
            ContentType = "image/jpeg", SizeBytes = 2048, UploadedByUserId = 10,
            UploadedAt = DateTime.UtcNow, StorageProvider = "local",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var diary = new Diary
        {
            Id = 1, ConstructionSiteId = 1, AuthorUserId = author.Id, Author = author,
            Title = "Diary 1", Date = DateOnly.FromDateTime(DateTime.UtcNow),
            IsPublished = false, IsArchived = false,
            Attachments = new List<Attachment> { attachment },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        attachment.DiaryId = 1;

        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<Role>().AddAsync(role);
        await _db.Set<User>().AddAsync(author);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);
        var result = await service.GetTimelineAsync(siteId: 1);

        result.Should().HaveCount(1);
        result[0].Attachments.Should().HaveCount(1);
        result[0].Attachments[0].FileName.Should().Be("photo.jpg");
        result[0].Attachments[0].ContentType.Should().Be("image/jpeg");
    }

    // ── CreateAsync with TemplateSnapshot tests ────────────────────────────────

    [Fact]
    public async Task Create_EmbedsTemplateSnapshot_WhenTemplateExists()
    {
        var site = MakeSite(1);
        var sectionsJson = """
            [{"Id":"sec1","Label":"General","Fields":[
              {"Id":"weather","Label":"Weather","Type":"text","Required":true},
              {"Id":"workers","Label":"Workers","Type":"number","Required":false}
            ]}]
            """;
        var template = new DiaryTemplate
        {
            Id = 1, Name = "Standard", Sections = sectionsJson,
            IsArchived = false, CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var dto = new CreateDiaryDto(
            "Test Entry", null,
            new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
            DiaryTemplateId: 1);

        var service = new DiaryService(_uow);
        var result = await service.CreateAsync(siteId: 1, authorUserId: 10, dto);

        var saved = await _db.Set<Diary>().FirstAsync(d => d.Id == result.Id);
        saved.TemplateSnapshot.Should().NotBeNullOrWhiteSpace();

        var snapshot = JsonSerializer.Deserialize<List<FieldDescriptorDto>>(saved.TemplateSnapshot!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        snapshot.Should().HaveCount(2);
        snapshot![0].Key.Should().Be("weather");
        snapshot[1].Key.Should().Be("workers");
    }

    [Fact]
    public async Task Create_EmbedsPayload_WhenPayloadProvided()
    {
        var site = MakeSite(1);
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.SaveChangesAsync();

        var payloadDict = new Dictionary<string, JsonElement>
        {
            ["weather"] = JsonDocument.Parse("\"Sunny\"").RootElement.Clone(),
            ["workers"] = JsonDocument.Parse("12").RootElement.Clone()
        };

        var dto = new CreateDiaryDto(
            "Test", null,
            new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
            Payload: payloadDict);

        var service = new DiaryService(_uow);
        var result = await service.CreateAsync(siteId: 1, authorUserId: 10, dto);

        var saved = await _db.Set<Diary>().FirstAsync(d => d.Id == result.Id);
        saved.Payload.Should().NotBeNullOrWhiteSpace();

        var parsed = JsonDocument.Parse(saved.Payload!).RootElement;
        parsed.GetProperty("weather").GetString().Should().Be("Sunny");
        parsed.GetProperty("workers").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task Create_AppliesFieldOverrides_RemovedFieldsAbsentFromSnapshot()
    {
        var site = MakeSite(1);
        var sectionsJson = """
            [{"Id":"sec1","Label":"General","Fields":[
              {"Id":"weather","Label":"Weather","Type":"text","Required":true},
              {"Id":"workers","Label":"Workers","Type":"number","Required":false},
              {"Id":"notes","Label":"Notes","Type":"text","Required":false}
            ]}]
            """;
        var template = new DiaryTemplate
        {
            Id = 1, Name = "Standard", Sections = sectionsJson,
            IsArchived = false, CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.Set<DiaryTemplate>().AddAsync(template);
        await _db.SaveChangesAsync();

        var dto = new CreateDiaryDto(
            "Test", null,
            new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
            DiaryTemplateId: 1,
            FieldOverrides: new FieldOverridesDto { Removed = new List<string> { "workers" } });

        var service = new DiaryService(_uow);
        var result = await service.CreateAsync(siteId: 1, authorUserId: 10, dto);

        var saved = await _db.Set<Diary>().FirstAsync(d => d.Id == result.Id);
        var snapshot = JsonSerializer.Deserialize<List<FieldDescriptorDto>>(saved.TemplateSnapshot!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        snapshot.Should().HaveCount(2);
        snapshot!.Select(f => f.Key).Should().NotContain("workers");
    }
}
