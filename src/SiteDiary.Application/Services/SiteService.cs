using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Services;

public class SiteService(IUnitOfWork uow) : ISiteService
{
    public async Task<IReadOnlyList<ConstructionSiteDto>> GetAllAsync(CancellationToken ct = default)
    {
        var sites = await uow.Sites.Query().ToListAsync(ct);
        return sites.Select(MapToDto).ToList();
    }

    public async Task<ConstructionSiteDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var site = await uow.Sites.GetByIdAsync(id, ct);
        return site is null ? null : MapToDto(site);
    }

    public async Task<ConstructionSiteDto> CreateAsync(CreateConstructionSiteRequest request, CancellationToken ct = default)
    {
        var site = new ConstructionSite
        {
            Name = request.Name,
            Description = request.Description,
            Address = request.Address,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await uow.Sites.AddAsync(site, ct);
        await uow.SaveChangesAsync(ct);
        return MapToDto(site);
    }

    public async Task<ConstructionSiteDto?> UpdateAsync(int id, UpdateConstructionSiteRequest request, CancellationToken ct = default)
    {
        var site = await uow.Sites.GetByIdAsync(id, ct);
        if (site is null) return null;

        site.Name = request.Name;
        site.Description = request.Description;
        site.Address = request.Address;
        site.UpdatedAt = DateTime.UtcNow;

        uow.Sites.Update(site);
        await uow.SaveChangesAsync(ct);
        return MapToDto(site);
    }

    public async Task<bool> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var site = await uow.Sites.GetByIdAsync(id, ct);
        if (site is null) return false;

        site.IsArchived = true;
        site.UpdatedAt = DateTime.UtcNow;
        uow.Sites.Update(site);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private static ConstructionSiteDto MapToDto(ConstructionSite s) =>
        new(s.Id, s.Name, s.Description, s.Address, s.IsArchived, s.CreatedAt, s.UpdatedAt);
}
