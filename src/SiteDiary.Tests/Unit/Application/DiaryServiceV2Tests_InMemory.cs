using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// TDD tests for the new vertical-slice DiaryService using EF Core InMemory.
/// This approach provides reliable async query testing without external mocking libraries.
/// </summary>
public class DiaryServiceV2Tests_InMemory : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _uow;

    public DiaryServiceV2Tests_InMemory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _uow = new UnitOfWork(_db);
    }

    public void Dispose() => _db.Dispose();

    private Diary MakeDiary(int id, int siteId, int authorId, bool isArchived = false,
        List<Attachment>? attachments = null) => new()
    {
        Id = id,
        ConstructionSiteId = siteId,
        AuthorUserId = authorId,
        Title = $"Diary {id}",
        Content = "Some content",
        Date = DateOnly.FromDateTime(DateTime.UtcNow),
        IsPublished = false,
        IsArchived = isArchived,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Attachments = attachments ?? new List<Attachment>()
    };

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBySiteId_ReturnsOnlyNonArchivedDiariesForThatSite()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site 1", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var diary1 = MakeDiary(1, siteId: 1, authorId: 10);
        var diary2 = MakeDiary(2, siteId: 1, authorId: 10, isArchived: true);
        var diary3 = MakeDiary(3, siteId: 2, authorId: 10);
        await _db.Set<Diary>().AddRangeAsync(diary1, diary2, diary3);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);

        // Act
        var result = await service.GetBySiteIdAsync(siteId: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdWithAttachments_ReturnsDiaryDetailDto_WithAttachments()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var attachment = new Attachment
        {
            Id = 5, FileName = "photo.jpg", FileUrl = "/uploads/photo.jpg",
            ContentType = "image/jpeg", SizeBytes = 1024, UploadedByUserId = 10,
            UploadedAt = DateTime.UtcNow, StorageProvider = "local",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var diary = MakeDiary(1, siteId: 1, authorId: 10, attachments: new List<Attachment> { attachment });
        attachment.DiaryId = diary.Id;

        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);

        // Act
        var result = await service.GetByIdWithAttachmentsAsync(siteId: 1, diaryId: 1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Attachments.Should().HaveCount(1);
        result.Attachments[0].FileName.Should().Be("photo.jpg");
    }

    [Fact]
    public async Task GetByIdWithAttachments_WhenArchived_ReturnsNull()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var archived = MakeDiary(1, siteId: 1, authorId: 10, isArchived: true);
        await _db.Set<Diary>().AddAsync(archived);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);

        // Act
        var result = await service.GetByIdWithAttachmentsAsync(siteId: 1, diaryId: 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Create_SetsAuthorUserIdAndSiteIdFromParameters()
    {
        // Arrange
        var site = new ConstructionSite { Id = 100, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);
        await _db.SaveChangesAsync();

        var dto = new CreateDiaryDto("Title", "Content",
            new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero), false);

        var service = new DiaryService(_uow);

        // Act
        var result = await service.CreateAsync(siteId: 100, authorUserId: 42, dto);

        // Assert
        result.ConstructionSiteId.Should().Be(100);
        result.AuthorUserId.Should().Be(42);

        var saved = await _db.Set<Diary>().FirstOrDefaultAsync(d => d.Id == result.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Title");
    }

    [Fact]
    public async Task Update_WhenOwner_UpdatesAndReturnsSuccess()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var diary = MakeDiary(1, siteId: 1, authorId: 10);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var dto = new UpdateDiaryDto("Updated Title", "Updated content",
            new DateTimeOffset(2026, 5, 17, 8, 0, 0, TimeSpan.Zero));

        var service = new DiaryService(_uow);

        // Act
        var result = await service.UpdateAsync(siteId: 1, diaryId: 1, requestingUserId: 10, dto);

        // Assert
        result.Status.Should().Be(OperationStatus.Success);
        result.Value!.Title.Should().Be("Updated Title");

        var updated = await _db.Set<Diary>().FirstAsync(d => d.Id == 1);
        updated.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Update_WhenNotOwner_ReturnsForbidden()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var diary = MakeDiary(1, siteId: 1, authorId: 10);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var dto = new UpdateDiaryDto("Title", null, new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero));

        var service = new DiaryService(_uow);

        // Act
        var result = await service.UpdateAsync(siteId: 1, diaryId: 1, requestingUserId: 99, dto);

        // Assert
        result.Status.Should().Be(OperationStatus.Forbidden);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateDiaryDto("Title", null, new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero));

        var service = new DiaryService(_uow);

        // Act
        var result = await service.UpdateAsync(siteId: 1, diaryId: 999, requestingUserId: 1, dto);

        // Assert
        result.Status.Should().Be(OperationStatus.NotFound);
    }

    [Fact]
    public async Task Delete_WhenOwner_SoftDeletesAndReturnsSuccess()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var diary = MakeDiary(1, siteId: 1, authorId: 10);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);

        // Act
        var result = await service.DeleteAsync(siteId: 1, diaryId: 1, requestingUserId: 10);

        // Assert
        result.Status.Should().Be(OperationStatus.Success);

        var deleted = await _db.Set<Diary>().IgnoreQueryFilters().FirstAsync(d => d.Id == 1);
        deleted.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenNotOwner_ReturnsForbidden()
    {
        // Arrange
        var site = new ConstructionSite { Id = 1, Name = "Site", Address = "Addr", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _db.Set<ConstructionSite>().AddAsync(site);

        var diary = MakeDiary(1, siteId: 1, authorId: 10);
        await _db.Set<Diary>().AddAsync(diary);
        await _db.SaveChangesAsync();

        var service = new DiaryService(_uow);

        // Act
        var result = await service.DeleteAsync(siteId: 1, diaryId: 1, requestingUserId: 99);

        // Assert
        result.Status.Should().Be(OperationStatus.Forbidden);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = new DiaryService(_uow);

        // Act
        var result = await service.DeleteAsync(siteId: 1, diaryId: 999, requestingUserId: 1);

        // Assert
        result.Status.Should().Be(OperationStatus.NotFound);
    }
}
