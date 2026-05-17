using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.Attachments;

public class AttachmentService(IUnitOfWork uow, IStorageService storage) : IAttachmentService
{
    public async Task<OperationResult<Attachment>> UploadAsync(int diaryId, int uploadedByUserId,
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var diary = await uow.Diaries.GetByIdAsync(diaryId, ct);
        if (diary is null) return OperationResult<Attachment>.NotFound();

        var fileUrl = await storage.UploadAsync(fileStream, fileName, contentType, ct);
        var sizeBytes = fileStream.CanSeek ? fileStream.Length : 0L;

        var attachment = new Attachment
        {
            DiaryId = diaryId,
            FileName = fileName,
            FileUrl = fileUrl,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow,
            StorageProvider = "local",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await uow.Attachments.AddAsync(attachment, ct);
        await uow.SaveChangesAsync(ct);
        return OperationResult<Attachment>.Ok(attachment);
    }

    public async Task<OperationResult<bool>> DeleteAsync(int attachmentId, int requestingUserId, CancellationToken ct = default)
    {
        var attachment = await uow.Attachments.GetByIdAsync(attachmentId, ct);
        if (attachment is null) return OperationResult<bool>.NotFound();
        if (attachment.UploadedByUserId != requestingUserId) return OperationResult<bool>.Forbidden();

        await storage.DeleteAsync(attachment.FileUrl, ct);
        uow.Attachments.Remove(attachment);
        await uow.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }


}
