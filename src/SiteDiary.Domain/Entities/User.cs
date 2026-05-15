using SiteDiary.Domain.Common;

namespace SiteDiary.Domain.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}";

    // Navigation
    public ICollection<SiteUser> SiteUsers { get; set; } = new List<SiteUser>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Diary> AuthoredDiaries { get; set; } = new List<Diary>();
}
