using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class Attachment : BaseEntity
{
    public int DiaryId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public string StorageProvider { get; set; } = string.Empty;

    // Navigation
    public Diary Diary { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
