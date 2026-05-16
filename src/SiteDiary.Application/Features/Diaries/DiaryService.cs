using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.Diaries;

public class DiaryService(IUnitOfWork uow) : IDiaryService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonElement _emptyPayload =
        JsonDocument.Parse("{}").RootElement.Clone();

    public async Task<IReadOnlyList<DiaryTimelineEntryDto>> GetTimelineAsync(int siteId, CancellationToken ct = default)
    {
        var diaries = await uow.Diaries.Query()
            .Include(d => d.Author).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(d => d.Attachments)
            .Where(d => d.ConstructionSiteId == siteId && !d.IsArchived)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);

        return diaries.Select(MapToTimelineEntry).ToList();
    }

    public async Task<IReadOnlyList<DiaryDto>> GetBySiteIdAsync(int siteId, CancellationToken ct = default)
    {
        var diaries = await uow.Diaries.Query()
            .Where(d => d.ConstructionSiteId == siteId && !d.IsArchived)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);
        return diaries.Select(MapToDto).ToList();
    }

    public async Task<DiaryDetailDto?> GetByIdWithAttachmentsAsync(int siteId, int diaryId, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.Query()
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.ConstructionSiteId == siteId && !d.IsArchived, ct);
        return diary is null ? null : MapToDetailDto(diary);
    }

    public async Task<DiaryDto> CreateAsync(int siteId, int authorUserId, CreateDiaryDto dto, CancellationToken ct = default)
    {
        // Build template snapshot: look up the template's sections if a template is referenced
        string? snapshotJson = null;
        if (dto.DiaryTemplateId.HasValue)
        {
            var template = await uow.DiaryTemplates.Query()
                .FirstOrDefaultAsync(t => t.Id == dto.DiaryTemplateId.Value && !t.IsArchived, ct);
            if (template is not null)
            {
                var sections = DeserializeSections(template.Sections);
                var overrides = dto.FieldOverrides;
                var removedSet = overrides?.Removed.ToHashSet() ?? new HashSet<string>();

                // Flatten template to FieldDescriptorDto list, applying overrides
                var order = 1;
                var descriptors = sections
                    .SelectMany(s => s.Fields)
                    .Where(f => !removedSet.Contains(f.Id))
                    .Select(f => new FieldDescriptorDto(f.Id, f.Label, f.Type, order++))
                    .ToList();

                // Append ad-hoc added fields
                if (overrides?.Added is { Count: > 0 } added)
                {
                    foreach (var f in added)
                        descriptors.Add(new FieldDescriptorDto(f.Id, f.Label, f.Type, order++));
                }

                snapshotJson = JsonSerializer.Serialize(descriptors, _jsonOptions);
            }
        }

        var payloadJson = dto.Payload is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.Payload, _jsonOptions)
            : null;

        var diary = new Diary
        {
            ConstructionSiteId = siteId,
            AuthorUserId = authorUserId,
            DiaryTemplateId = dto.DiaryTemplateId,
            Title = dto.Title,
            Content = dto.Content,
            Date = DateOnly.FromDateTime(dto.Date.Date),
            IsPublished = dto.IsPublished,
            FieldOverrides = SerializeOverrides(dto.FieldOverrides),
            Payload = payloadJson,
            TemplateSnapshot = snapshotJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await uow.Diaries.AddAsync(diary, ct);
        await uow.SaveChangesAsync(ct);
        return MapToDto(diary);
    }

    public async Task<OperationResult<DiaryDto>> UpdateAsync(int siteId, int diaryId, int requestingUserId, UpdateDiaryDto dto, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.Query()
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.ConstructionSiteId == siteId && !d.IsArchived, ct);
        if (diary is null) return OperationResult<DiaryDto>.NotFound();
        if (diary.AuthorUserId != requestingUserId) return OperationResult<DiaryDto>.Forbidden();

        diary.Title = dto.Title;
        diary.Content = dto.Content;
        diary.Date = DateOnly.FromDateTime(dto.Date.Date);
        diary.FieldOverrides = SerializeOverrides(dto.FieldOverrides);
        diary.UpdatedAt = DateTime.UtcNow;

        uow.Diaries.Update(diary);
        await uow.SaveChangesAsync(ct);
        return OperationResult<DiaryDto>.Ok(MapToDto(diary));
    }

    public async Task<OperationResult<bool>> DeleteAsync(int siteId, int diaryId, int requestingUserId, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.Query()
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.ConstructionSiteId == siteId && !d.IsArchived, ct);
        if (diary is null) return OperationResult<bool>.NotFound();
        if (diary.AuthorUserId != requestingUserId) return OperationResult<bool>.Forbidden();

        diary.IsArchived = true;
        diary.UpdatedAt = DateTime.UtcNow;
        uow.Diaries.Update(diary);
        await uow.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static DiaryTimelineEntryDto MapToTimelineEntry(Diary d)
    {
        var authorName = $"{d.Author.FirstName} {d.Author.LastName}";
        var authorRole = d.Author.UserRoles
            .Where(ur => ur.IsActive)
            .Select(ur => ur.Role?.Name)
            .FirstOrDefault();

        var payload = TryParseJsonElement(d.Payload) ?? _emptyPayload;
        var snapshot = TryDeserializeSnapshot(d.TemplateSnapshot);
        var attachments = d.Attachments
            .Select(a => new AttachmentDto(a.Id, a.DiaryId, a.FileName, a.FileUrl, a.ContentType))
            .ToList();

        return new DiaryTimelineEntryDto(
            d.Id, d.ConstructionSiteId, d.AuthorUserId,
            authorName, authorRole,
            d.Date, d.IsPublished,
            payload, snapshot, attachments);
    }

    private static DiaryDto MapToDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            new DateTimeOffset(d.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            d.IsPublished, d.DiaryTemplateId);

    private static DiaryDetailDto MapToDetailDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            new DateTimeOffset(d.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            d.IsPublished,
            d.Attachments.Select(a => new AttachmentDto(a.Id, a.DiaryId, a.FileName, a.FileUrl, a.ContentType))
                         .ToList(),
            d.DiaryTemplateId,
            DeserializeOverrides(d.FieldOverrides));

    private static FieldOverridesDto? DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<FieldOverridesDto>(json, _jsonOptions);
    }

    private static string? SerializeOverrides(FieldOverridesDto? overrides)
    {
        if (overrides is null) return null;
        return JsonSerializer.Serialize(overrides, _jsonOptions);
    }

    private static IReadOnlyList<SectionDef> DeserializeSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SectionDef>();
        return JsonSerializer.Deserialize<IReadOnlyList<SectionDef>>(json, _jsonOptions)
               ?? Array.Empty<SectionDef>();
    }

    private static JsonElement? TryParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch { return null; }
    }

    private static IReadOnlyList<FieldDescriptorDto> TryDeserializeSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<FieldDescriptorDto>();
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<FieldDescriptorDto>>(json, _jsonOptions)
                   ?? Array.Empty<FieldDescriptorDto>();
        }
        catch { return Array.Empty<FieldDescriptorDto>(); }
    }
}
