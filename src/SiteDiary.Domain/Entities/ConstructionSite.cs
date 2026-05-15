using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class ConstructionSite : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsArchived { get; set; } = false;

    // Navigation
    public ICollection<SiteUser> SiteUsers { get; set; } = new List<SiteUser>();
    public ICollection<Diary> Diaries { get; set; } = new List<Diary>();
}
