using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class AuditHistory : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int? ChangedByUserId { get; set; }
    /// <summary>JSON diff stored as nvarchar(max).</summary>
    public string? Changes { get; set; }
    public DateTime Timestamp { get; set; }

    // Navigation
    public User? ChangedBy { get; set; }
}
