using SiteDiary.Application.Features.Attachments;

namespace SiteDiary.Application.Features.Diaries;

/// <summary>
/// Lean list/mutation response — server-managed fields excluded.
/// </summary>
public record DiaryDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished);

/// <summary>
/// Detail response — adds eager-loaded attachment list.
/// </summary>
public record DiaryDetailDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary>
/// POST body — server-managed fields (Id, AuthorUserId, ConstructionSiteId, timestamps) are NOT accepted.
/// </summary>
public record CreateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date,
    bool IsPublished = false);

/// <summary>
/// PUT body — only mutable user fields.
/// </summary>
public record UpdateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date);
