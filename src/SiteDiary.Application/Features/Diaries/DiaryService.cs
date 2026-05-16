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
}
