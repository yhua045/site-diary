using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Features.Diaries;

[ApiController]
[Route("api/sites/{siteId:int}/diaries")]
public class DiariesController(IDiaryService diaryService) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiaryDto>>> GetAll(int siteId, CancellationToken ct)
    {
        var diaries = await diaryService.GetBySiteIdAsync(siteId, ct);
        return Ok(diaries.Select(MapToDto).ToList());
    }

    [HttpGet("timeline")]
    public async Task<ActionResult<IReadOnlyList<DiaryTimelineEntryDto>>> GetTimeline(int siteId, CancellationToken ct)
    {
        var diaries = await diaryService.GetTimelineAsync(siteId, ct);
        return Ok(diaries.Select(MapToTimelineEntryDto).ToList());
    }

    [HttpGet("{diaryId:int}")]
    public async Task<ActionResult<DiaryDetailDto>> GetById(int siteId, int diaryId, CancellationToken ct)
    {
        var diary = await diaryService.GetByIdWithAttachmentsAsync(siteId, diaryId, ct);
        if (diary is null)
            return NotFound();

        return Ok(MapToDetailDto(diary));
    }

    [HttpPost]
    public async Task<ActionResult<DiaryDto>> Create(int siteId, [FromBody] CreateDiaryDto dto, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var diary = new Diary
        {
            DiaryTemplateId = dto.DiaryTemplateId,
            Title = dto.Title,
            Content = dto.Content,
            Date = dto.Date,
            FieldOverrides = SerializeOverrides(dto.FieldOverrides),
            Payload = dto.Payload is { Count: > 0 } ? JsonSerializer.Serialize(dto.Payload, _jsonOptions) : null
        };

        var created = await diaryService.CreateAsync(siteId, userId, diary, ct);
        return CreatedAtAction(nameof(GetById), new { siteId, diaryId = created.Id }, MapToDto(created));
    }

    [HttpPut("{diaryId:int}")]
    public async Task<ActionResult<DiaryDto>> Update(int siteId, int diaryId,
        [FromBody] UpdateDiaryDto dto, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var updateValues = new Diary
        {
            Title = dto.Title,
            Content = dto.Content,
            Date = dto.Date,
            FieldOverrides = SerializeOverrides(dto.FieldOverrides)
        };

        var result = await diaryService.UpdateAsync(siteId, diaryId, userId, updateValues, ct);
        return result.Status switch
        {
            OperationStatus.Success => Ok(MapToDto(result.Value)),
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{diaryId:int}")]
    public async Task<IActionResult> Delete(int siteId, int diaryId, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var result = await diaryService.DeleteAsync(siteId, diaryId, userId, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── Mapping Helpers ───────────────────────────────────────────────────────

    private static DiaryDto MapToDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            d.Date,
            d.DiaryTemplateId);

    private static DiaryTimelineEntryDto MapToTimelineEntryDto(Diary d)
    {
        var authorName = $"{d.Author?.FirstName} {d.Author?.LastName}";
        var authorRole = d.Author?.UserRoles
            .Where(ur => ur.IsActive)
            .Select(ur => ur.Role?.Name)
            .FirstOrDefault();

        var payload = TryParseJsonElement(d.Payload) ?? JsonDocument.Parse("{}").RootElement.Clone();
        var snapshot = TryDeserializeSnapshot(d.TemplateSnapshot);
        var attachments = d.Attachments
            .Select(a => new AttachmentDto(a.Id, a.DiaryId, a.FileName, a.FileUrl, a.ContentType))
            .ToList();

        return new DiaryTimelineEntryDto(
            d.Id, d.ConstructionSiteId, d.AuthorUserId,
            authorName, authorRole,
            d.Date,
            payload, snapshot, attachments);
    }

    private static DiaryDetailDto MapToDetailDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            d.Date,
            d.Attachments.Select(a => new AttachmentDto(a.Id, a.DiaryId, a.FileName, a.FileUrl, a.ContentType))
                         .ToList(),
            d.DiaryTemplateId,
            DeserializeOverrides(d.FieldOverrides));

    private static string? SerializeOverrides(FieldOverridesDto? overrides)
    {
        if (overrides is null) return null;
        return JsonSerializer.Serialize(overrides, _jsonOptions);
    }

    private static FieldOverridesDto? DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<FieldOverridesDto>(json, _jsonOptions);
    }

    private static JsonElement? TryParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch { return null; }
    }

    private static IReadOnlyList<FieldDescriptorDto>? TryDeserializeSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<IReadOnlyList<FieldDescriptorDto>>(json, _jsonOptions); }
        catch { return null; }
    }
}
