using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Shared;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Features.Attachments;

[ApiController]
public class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    [HttpPost("api/diaries/{diaryId:int}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AttachmentDto>> Upload(int diaryId, IFormFile file, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        await using var stream = file.OpenReadStream();
        var result = await attachmentService.UploadAsync(diaryId, userId, stream, file.FileName, file.ContentType, ct);
        return result.Status switch
        {
            OperationStatus.Success => CreatedAtAction(nameof(Upload), new { diaryId, attachmentId = result.Value!.Id }, ToDto(result.Value!)),
            OperationStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("api/attachments/{attachmentId:int}")]
    public async Task<IActionResult> Delete(int attachmentId, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var result = await attachmentService.DeleteAsync(attachmentId, userId, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static AttachmentDto ToDto(SiteDiary.Domain.Entities.Attachment attachment) =>
        new(attachment.Id, attachment.DiaryId, attachment.FileName, attachment.FileUrl, attachment.ContentType);
}
