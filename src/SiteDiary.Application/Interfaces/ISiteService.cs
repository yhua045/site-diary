using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Interfaces;

public interface ISiteService
{
    Task<IReadOnlyList<ConstructionSite>> GetAllAsync(CancellationToken ct = default);
    Task<ConstructionSite?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ConstructionSite>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<ConstructionSite> CreateAsync(ConstructionSite site, CancellationToken ct = default);
    Task<ConstructionSite?> UpdateAsync(int id, ConstructionSite updateValues, CancellationToken ct = default);
    Task<bool> ArchiveAsync(int id, CancellationToken ct = default);
}
