namespace SiteDiary.Application.Interfaces;

public interface ISiteAuthorizationService
{
    /// <summary>
    /// Returns true if <paramref name="userId"/> has an active SiteUser row for
    /// <paramref name="siteId"/> (ConstructionSite.IsArchived == false).
    /// </summary>
    Task<bool> IsUserMemberOfSiteAsync(int userId, int siteId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the ConstructionSiteId for the given <paramref name="diaryId"/>,
    /// then delegates to <see cref="IsUserMemberOfSiteAsync"/>.
    /// Returns false if the diary does not exist (treats absence as unauthorized).
    /// </summary>
    Task<bool> IsUserAuthorizedForDiaryAsync(int userId, int diaryId, CancellationToken ct = default);
}
