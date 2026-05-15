using SiteDiary.Application.Shared;

namespace SiteDiary.Application.Features.Attachments;

public interface IAttachmentService
{
    Task<OperationResult<AttachmentDto>> UploadAsync(int diaryId, int uploadedByUserId,
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int attachmentId, int requestingUserId, CancellationToken ct = default);
}
