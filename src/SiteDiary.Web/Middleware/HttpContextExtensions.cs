namespace SiteDiary.Web.Middleware;

public static class HttpContextExtensions
{
    /// <summary>Returns the parsed X-User-Id, or null if the header was absent/invalid.</summary>
    public static int? GetCurrentUserId(this HttpContext context)
        => context.Items.TryGetValue(HttpContextKeys.UserId, out var val)
               ? (int?)val
               : null;

    /// <summary>Returns the parsed siteId route value, or null if absent.</summary>
    public static int? GetCurrentSiteId(this HttpContext context)
        => context.Items.TryGetValue(HttpContextKeys.SiteId, out var val)
               ? (int?)val
               : null;

    /// <summary>Returns the parsed diaryId route value, or null if absent.</summary>
    public static int? GetCurrentDiaryId(this HttpContext context)
        => context.Items.TryGetValue(HttpContextKeys.DiaryId, out var val)
               ? (int?)val
               : null;
}
