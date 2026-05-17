using System.Text.Json;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Features.DiaryTemplates;

namespace SiteDiary.Application.Features.Diaries;

/// <summary>
/// A single field descriptor embedded in a diary entry's template snapshot.
/// Mirrors the TypeScript FieldDescriptor interface used by the card renderer.
/// </summary>
public record FieldDescriptorDto(
    string Key,
    string Label,
    string Type,
    int DisplayOrder);

/// <summary>
/// Full timeline entry returned by GET /api/sites/{siteId}/diaries.
/// Includes author info, dynamic payload, template snapshot, and inline attachments.
/// </summary>
public record DiaryTimelineEntryDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string AuthorName,
    string? AuthorRole,
    DateTimeOffset Date,
    JsonElement Payload,
    IReadOnlyList<FieldDescriptorDto> TemplateSnapshot,
    IReadOnlyList<AttachmentDto> Attachments);

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
    int? DiaryTemplateId = null);

/// <summary>
/// Detail response — adds eager-loaded attachment list and per-diary field overrides.
/// </summary>
public record DiaryDetailDto(
    int Id,
    int ConstructionSiteId,
    int AuthorUserId,
    string Title,
    string? Content,
    DateTimeOffset Date,
    IReadOnlyList<AttachmentDto> Attachments,
    int? DiaryTemplateId = null,
    FieldOverridesDto? FieldOverrides = null);

/// <summary>
/// POST body — server-managed fields (Id, AuthorUserId, ConstructionSiteId, timestamps) are NOT accepted.
/// </summary>
public record CreateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date,
    int? DiaryTemplateId = null,
    FieldOverridesDto? FieldOverrides = null,
    Dictionary<string, JsonElement>? Payload = null);

/// <summary>
/// PUT body — only mutable user fields.
/// </summary>
public record UpdateDiaryDto(
    string Title,
    string? Content,
    DateTimeOffset Date,
    FieldOverridesDto? FieldOverrides = null);
