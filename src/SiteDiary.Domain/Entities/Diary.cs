using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class Diary : BaseEntity
{
    public int ConstructionSiteId { get; set; }
    public int AuthorUserId { get; set; }
    public int? DiaryTemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateOnly Date { get; set; }
    public bool IsPublished { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    /// <summary>JSON: { "removed": [], "added": [] } — per-diary field overrides relative to the base template.</summary>
    public string? FieldOverrides { get; set; }

    // Navigation
    public ConstructionSite ConstructionSite { get; set; } = null!;
    public User Author { get; set; } = null!;
    public DiaryTemplate? DiaryTemplate { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
