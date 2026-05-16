namespace SiteDiary.Web.Middleware;

public interface IRequestSecurityContext
{
    /// <summary>Parsed value from the X-User-Id header. Null if header absent or non-integer.</summary>
    int? AuthenticatedUserId { get; set; }

    /// <summary>siteId route parameter, if present in the current route.</summary>
    int? RequestedSiteId { get; set; }

    /// <summary>diaryId route parameter, if present in the current route.</summary>
    int? RequestedDiaryId { get; set; }

    /// <summary>userId route parameter from routes such as api/users/{userId:int}/…. Validated against AuthenticatedUserId by ResourceAuthorizationMiddleware.</summary>
    int? RequestedUserId { get; set; }
}
