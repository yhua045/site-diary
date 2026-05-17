using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Services;

public class SiteService(IUnitOfWork uow) : ISiteService
{
    public async Task<IReadOnlyList<ConstructionSite>> GetAllAsync(CancellationToken ct = default)
    {
        var sites = await uow.Sites.Query().ToListAsync(ct);
        return sites;
    }

    public async Task<ConstructionSite?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var site = await uow.Sites.GetByIdAsync(id, ct);
        return site;
    }

    public async Task<ConstructionSite> CreateAsync(ConstructionSite site, CancellationToken ct = default)
    {
        site.CreatedAt = DateTime.UtcNow;
        site.UpdatedAt = DateTime.UtcNow;
        await uow.Sites.AddAsync(site, ct);
        await uow.SaveChangesAsync(ct);
        return site;
    }

    public async Task<ConstructionSite?> UpdateAsync(int id, ConstructionSite updateValues, CancellationToken ct = default)
    {
        var site = await uow.Sites.GetByIdAsync(id, ct);
        if (site is null) return null;

        site.Name = updateValues.Name;
        site.Description = updateValues.Description;
        site.Address = updateValues.Address;
        site.UpdatedAt = DateTime.UtcNow;

        uow.Sites.Update(site);
        await uow.SaveChangesAsync(ct);
        return site;
    }

    public async Task<IReadOnlyList<ConstructionSite>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        // Step 1: resolve the construction site IDs this user is assigned to.
        var siteIds = await uow.SiteUsers.Query()
            .Where(su => su.UserId == userId)
            .Select(su => su.ConstructionSiteId)
            .ToListAsync(ct);

        if (siteIds.Count == 0)
            return Array.Empty<ConstructionSite>();

        // Step 2: fetch sites — global IsArchived filter already excludes archived sites.
        var sites = await uow.Sites.Query()
            .Where(s => siteIds.Contains(s.Id))
            .ToListAsync(ct);

        return sites;
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
}
