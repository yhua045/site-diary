using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<SiteUser> SiteUsers { get; set; } = new List<SiteUser>();
    public ICollection<DiaryTemplate> DiaryTemplates { get; set; } = [];
}
