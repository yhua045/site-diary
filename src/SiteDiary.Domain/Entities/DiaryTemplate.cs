using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class DiaryTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>JSON string representing section definitions.</summary>
    public string Sections { get; set; } = "[]";
    public bool IsDefault { get; set; } = false;
    public int CreatedByUserId { get; set; }
    public bool IsArchived { get; set; } = false;

    // Navigation
    public User CreatedBy { get; set; } = null!;
    public ICollection<Diary> Diaries { get; set; } = new List<Diary>();
}
