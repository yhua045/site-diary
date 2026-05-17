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

    public async Task<IReadOnlyList<Diary>> GetTimelineAsync(int siteId, CancellationToken ct = default)
    {
        var diaries = await uow.Diaries.Query()
            .Include(d => d.Author).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(d => d.Attachments)
            .Where(d => d.ConstructionSiteId == siteId && !d.IsArchived)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);

        return diaries;
    }

    public async Task<IReadOnlyList<Diary>> GetBySiteIdAsync(int siteId, CancellationToken ct = default)
    {
        var diaries = await uow.Diaries.Query()
            .Where(d => d.ConstructionSiteId == siteId && !d.IsArchived)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);
        return diaries;
    }

    public async Task<Diary?> GetByIdWithAttachmentsAsync(int siteId, int diaryId, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.Query()
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.ConstructionSiteId == siteId && !d.IsArchived, ct);
        return diary;
    }

    public async Task<Diary> CreateAsync(int siteId, int authorUserId, Diary diary, CancellationToken ct = default)
    {
        diary.ConstructionSiteId = siteId;
        diary.AuthorUserId = authorUserId;
        diary.CreatedAt = DateTime.UtcNow;
        diary.UpdatedAt = DateTime.UtcNow;

        // Build template snapshot if missing
        if (diary.DiaryTemplateId.HasValue && string.IsNullOrEmpty(diary.TemplateSnapshot))
        {
            var template = await uow.DiaryTemplates.Query()
                .FirstOrDefaultAsync(t => t.Id == diary.DiaryTemplateId.Value && !t.IsArchived, ct);
            if (template is not null)
            {
                var sections = DeserializeSections(template.Sections);
                
                var removedSet = new HashSet<string>();
                List<FieldDef> addedFields = new();
                if (!string.IsNullOrEmpty(diary.FieldOverrides))
                {
                    try
                    {
                        var overrides = JsonSerializer.Deserialize<FieldOverridesDto>(diary.FieldOverrides, _jsonOptions);
                        if (overrides?.Removed != null)
                            removedSet = overrides.Removed.ToHashSet();
                        if (overrides?.Added != null)
                            addedFields = overrides.Added.ToList();
                    }
                    catch { /* ignore invalid overrides */ }
                }

                var order = 1;
                var descriptors = sections
                    .SelectMany(s => s.Fields)
                    .Where(f => !removedSet.Contains(f.Id))
                    .Select(f => new FieldDescriptorDto(f.Id, f.Label, f.Type, order++))
                    .ToList();

                foreach (var f in addedFields)
                    descriptors.Add(new FieldDescriptorDto(f.Id, f.Label, f.Type, order++));

                diary.TemplateSnapshot = JsonSerializer.Serialize(descriptors, _jsonOptions);
            }
        }

        await uow.Diaries.AddAsync(diary, ct);
        await uow.SaveChangesAsync(ct);
        return diary;
    }

    public async Task<OperationResult<Diary>> UpdateAsync(int siteId, int diaryId, int requestingUserId, Diary updateValues, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.Query()
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.ConstructionSiteId == siteId && !d.IsArchived, ct);
        if (diary is null) return OperationResult<Diary>.NotFound();
        if (diary.AuthorUserId != requestingUserId) return OperationResult<Diary>.Forbidden();

        diary.Title = updateValues.Title;
        diary.Content = updateValues.Content;
        diary.Date = updateValues.Date;
        diary.FieldOverrides = updateValues.FieldOverrides;
        diary.UpdatedAt = DateTime.UtcNow;

        uow.Diaries.Update(diary);
        await uow.SaveChangesAsync(ct);
        return OperationResult<Diary>.Ok(diary);
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
            d.Date, 
            payload, snapshot, attachments);
    }

    private static DiaryDto MapToDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            new DateTimeOffset(d.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            d.DiaryTemplateId);

    private static DiaryDetailDto MapToDetailDto(Diary d) =>
        new(d.Id, d.ConstructionSiteId, d.AuthorUserId, d.Title, d.Content,
            new DateTimeOffset(d.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
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
