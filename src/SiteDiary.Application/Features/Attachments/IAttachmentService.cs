using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Features.Attachments;

public interface IAttachmentService
{
    Task<OperationResult<Attachment>> UploadAsync(int diaryId, int uploadedByUserId,
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteAsync(int attachmentId, int requestingUserId, CancellationToken ct = default);
}
