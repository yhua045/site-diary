using SiteDiary.Application.Shared;

namespace SiteDiary.Application.Features.Diaries;

public interface IDiaryService
{
    Task<IReadOnlyList<DiaryDto>> GetBySiteIdAsync(int siteId, CancellationToken ct = default);
    Task<DiaryDetailDto?> GetByIdWithAttachmentsAsync(int siteId, int diaryId, CancellationToken ct = default);
    Task<DiaryDto> CreateAsync(int siteId, int authorUserId, CreateDiaryDto dto, CancellationToken ct = default);
    Task<OperationResult<DiaryDto>> UpdateAsync(int siteId, int diaryId, int requestingUserId, UpdateDiaryDto dto, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int siteId, int diaryId, int requestingUserId, CancellationToken ct = default);
}
