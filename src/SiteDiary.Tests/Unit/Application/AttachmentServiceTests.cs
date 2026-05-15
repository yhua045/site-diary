using FluentAssertions;
using Moq;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Tests.Unit.Application;

/// <summary>
/// TDD tests for AttachmentService (Phase 1C, tests #14–#18).
/// </summary>
public class AttachmentServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRepository<Attachment>> _attachmentRepo = new();
    private readonly Mock<IRepository<Diary>> _diaryRepo = new();
    private readonly Mock<IStorageService> _storage = new();

    public AttachmentServiceTests()
    {
        _uow.Setup(u => u.Attachments).Returns(_attachmentRepo.Object);
        _uow.Setup(u => u.Diaries).Returns(_diaryRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private static Attachment MakeAttachment(int id, int diaryId, int uploadedBy) => new()
    {
        Id = id,
        DiaryId = diaryId,
        FileName = "photo.jpg",
        FileUrl = "/uploads/photo.jpg",
        ContentType = "image/jpeg",
        SizeBytes = 1024,
        UploadedByUserId = uploadedBy,
        StorageProvider = "local",
        UploadedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // Test #14
    [Fact]
    public async Task Upload_WhenDiaryExists_CreatesAttachmentAndReturnsDto()
    {
        var diary = new Diary { Id = 1, ConstructionSiteId = 1, AuthorUserId = 10, Title = "D",
            Date = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _diaryRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(diary);
        _storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/photo.jpg");
        _attachmentRepo.Setup(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AttachmentService(_uow.Object, _storage.Object);
        using var stream = new MemoryStream(new byte[100]);
        var result = await service.UploadAsync(diaryId: 1, uploadedByUserId: 42, stream, "photo.jpg", "image/jpeg");

        result.Status.Should().Be(OperationStatus.Success);
        result.Value.Should().NotBeNull();
        result.Value!.DiaryId.Should().Be(1);
        result.Value.FileName.Should().Be("photo.jpg");
        result.Value.FileUrl.Should().Be("/uploads/photo.jpg");
        _attachmentRepo.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test #15
    [Fact]
    public async Task Upload_WhenDiaryNotFound_ReturnsNotFound()
    {
        _diaryRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Diary?)null);

        var service = new AttachmentService(_uow.Object, _storage.Object);
        using var stream = new MemoryStream();
        var result = await service.UploadAsync(diaryId: 99, uploadedByUserId: 1, stream, "f.jpg", "image/jpeg");

        result.Status.Should().Be(OperationStatus.NotFound);
        _storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test #16
    [Fact]
    public async Task Delete_WhenOwner_DeletesFromStorageAndDb()
    {
        var attachment = MakeAttachment(10, diaryId: 1, uploadedBy: 42);
        _attachmentRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);
        _storage.Setup(s => s.DeleteAsync("/uploads/photo.jpg", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new AttachmentService(_uow.Object, _storage.Object);
        var result = await service.DeleteAsync(attachmentId: 10, requestingUserId: 42);

        result.Status.Should().Be(OperationStatus.Success);
        _storage.Verify(s => s.DeleteAsync("/uploads/photo.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _attachmentRepo.Verify(r => r.Remove(attachment), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test #17
    [Fact]
    public async Task Delete_WhenNotOwner_ReturnsForbidden()
    {
        var attachment = MakeAttachment(10, diaryId: 1, uploadedBy: 42);
        _attachmentRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);

        var service = new AttachmentService(_uow.Object, _storage.Object);
        var result = await service.DeleteAsync(attachmentId: 10, requestingUserId: 99);

        result.Status.Should().Be(OperationStatus.Forbidden);
        _storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test #18
    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        _attachmentRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Attachment?)null);

        var service = new AttachmentService(_uow.Object, _storage.Object);
        var result = await service.DeleteAsync(attachmentId: 99, requestingUserId: 1);

        result.Status.Should().Be(OperationStatus.NotFound);
    }
}
