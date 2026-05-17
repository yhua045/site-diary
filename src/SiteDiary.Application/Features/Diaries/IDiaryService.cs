using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Features.Diaries;

public interface IDiaryService
{
    /// <summary>Returns the full timeline feed for a site (newest first), with attachments and template snapshots eager-loaded.</summary>
    Task<IReadOnlyList<Diary>> GetTimelineAsync(int siteId, CancellationToken ct = default);

    Task<IReadOnlyList<Diary>> GetBySiteIdAsync(int siteId, CancellationToken ct = default);
    Task<Diary?> GetByIdWithAttachmentsAsync(int siteId, int diaryId, CancellationToken ct = default);
    Task<Diary> CreateAsync(int siteId, int authorUserId, Diary diary, CancellationToken ct = default);
    Task<OperationResult<Diary>> UpdateAsync(int siteId, int diaryId, int requestingUserId, Diary updateValues, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int siteId, int diaryId, int requestingUserId, CancellationToken ct = default);
}
