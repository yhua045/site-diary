using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class SiteUser : BaseEntity
{
    public int ConstructionSiteId { get; set; }
    public int UserId { get; set; }
    public int AssignedRoleId { get; set; }
    public DateOnly JoinedDate { get; set; }
    public bool IsPrimaryContact { get; set; } = false;

    // Navigation
    public ConstructionSite ConstructionSite { get; set; } = null!;
    public User User { get; set; } = null!;
    public Role AssignedRole { get; set; } = null!;
}
