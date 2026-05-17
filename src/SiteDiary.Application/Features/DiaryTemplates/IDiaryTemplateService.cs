using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Features.DiaryTemplates;

public interface IDiaryTemplateService
{
    /// <summary>
    /// Returns the template entity, or null if not found / archived.
    /// </summary>
    Task<DiaryTemplate?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns the template assigned to the user's active role.
    /// For POC: returns the single IsDefault=true template for all roles.
    /// The user cannot choose which template they receive.
    /// </summary>
    Task<DiaryTemplate?> GetByUserRoleAsync(int userId, CancellationToken ct = default);
}
