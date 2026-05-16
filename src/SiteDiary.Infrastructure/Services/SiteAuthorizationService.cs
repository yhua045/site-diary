using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Infrastructure.Services;

public sealed class SiteAuthorizationService(IUnitOfWork uow) : ISiteAuthorizationService
{
    public Task<bool> IsUserMemberOfSiteAsync(int userId, int siteId, CancellationToken ct = default)
        => uow.SiteUsers.Query()
               .Where(su => su.UserId == userId && su.ConstructionSiteId == siteId)
               .Join(uow.Sites.Query(), su => su.ConstructionSiteId, s => s.Id, (su, s) => su)
               .AnyAsync(ct);

    public async Task<bool> IsUserAuthorizedForDiaryAsync(int userId, int diaryId, CancellationToken ct = default)
    {
        var siteId = await uow.Diaries.Query()
                               .Where(d => d.Id == diaryId)
                               .Select(d => d.ConstructionSiteId)
                               .FirstOrDefaultAsync(ct);

        if (siteId == 0) return false; // diary not found

        return await IsUserMemberOfSiteAsync(userId, siteId, ct);
    }
}
