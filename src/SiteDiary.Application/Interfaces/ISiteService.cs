using SiteDiary.Application.DTOs;

namespace SiteDiary.Application.Interfaces;

public interface ISiteService
{
    Task<IReadOnlyList<ConstructionSiteDto>> GetAllAsync(CancellationToken ct = default);
    Task<ConstructionSiteDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ConstructionSiteDto>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<ConstructionSiteDto> CreateAsync(CreateConstructionSiteRequest request, CancellationToken ct = default);
    Task<ConstructionSiteDto?> UpdateAsync(int id, UpdateConstructionSiteRequest request, CancellationToken ct = default);
    Task<bool> ArchiveAsync(int id, CancellationToken ct = default);
}
