namespace SiteDiary.Application.Features.Attachments;

/// <summary>
/// Lean attachment response — only fields the frontend needs.
/// </summary>
public record AttachmentDto(
    int Id,
    int DiaryId,
    string FileName,
    string FileUrl,
    string ContentType);
