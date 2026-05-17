using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Features.Attachments;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Controller unit tests for AttachmentsController post-refactor (Issue #8).
/// Service returns Attachment entity; IMapper translates to AttachmentDto at boundary.
/// Phase B: these fail until the controller is updated in Phase C.
/// </summary>
public class AttachmentsControllerTests
{
    private readonly Mock<IAttachmentService> _svc = new();
    private readonly Mock<IMapper> _mapper = new();

    private AttachmentsController MakeController()
    {
        var ctrl = new AttachmentsController(_svc.Object, _mapper.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        ctrl.HttpContext.Items["CurrentUserId"] = 42;
        return ctrl;
    }

    private static Attachment MakeAttachment(int id = 1) => new()
    {
        Id = id,
        DiaryId = 5,
        FileName = "photo.jpg",
        FileUrl = "/uploads/photo.jpg",
        ContentType = "image/jpeg",
        SizeBytes = 1024,
        UploadedByUserId = 42,
        UploadedAt = DateTime.UtcNow,
        StorageProvider = "local",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Upload_Success_Returns201WithMappedDto()
    {
        var savedEntity = MakeAttachment(1);
        var responseDto = new AttachmentDto(1, 5, "photo.jpg", "/uploads/photo.jpg", "image/jpeg");

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("photo.jpg");
        file.Setup(f => f.ContentType).Returns("image/jpeg");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        _svc.Setup(s => s.UploadAsync(5, 42, It.IsAny<Stream>(), "photo.jpg", "image/jpeg", default))
            .ReturnsAsync(OperationResult<Attachment>.Ok(savedEntity));
        _mapper.Setup(m => m.Map<AttachmentDto>(savedEntity)).Returns(responseDto);

        var result = await MakeController().Upload(5, file.Object, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mapper.Verify(m => m.Map<AttachmentDto>(savedEntity), Times.Once);
    }

    [Fact]
    public async Task Upload_DiaryNotFound_Returns404()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("x.jpg");
        file.Setup(f => f.ContentType).Returns("image/jpeg");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _svc.Setup(s => s.UploadAsync(99, 42, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(OperationResult<Attachment>.NotFound());

        var result = await MakeController().Upload(99, file.Object, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
