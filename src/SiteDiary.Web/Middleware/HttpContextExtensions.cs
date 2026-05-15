namespace SiteDiary.Web.Middleware;

public static class HttpContextExtensions
{
    /// <summary>Returns the parsed X-User-Id, or null if the header was absent/invalid.</summary>
    public static int? GetCurrentUserId(this HttpContext context)
        => context.Items.TryGetValue(HttpContextKeys.UserId, out var val)
               ? (int?)val
               : null;
}
