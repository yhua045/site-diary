namespace SiteDiary.Web.Middleware;

public sealed class RequestContextExtractionMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-User-Id";

    public async Task InvokeAsync(HttpContext ctx, IRequestSecurityContext secCtx)
    {
        // 1. Header extraction — keep existing Items key for backward compat
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var raw)
            && int.TryParse(raw, out var userId))
        {
            secCtx.AuthenticatedUserId = userId;
            ctx.Items[HttpContextKeys.UserId] = userId; // backward compat
        }

        // 2. Route value extraction
        if (TryParseRouteInt(ctx, "siteId", out var siteId))
            secCtx.RequestedSiteId = siteId;

        if (TryParseRouteInt(ctx, "diaryId", out var diaryId))
            secCtx.RequestedDiaryId = diaryId;

        if (TryParseRouteInt(ctx, "userId", out var routeUserId))
            secCtx.RequestedUserId = routeUserId;

        await next(ctx);
    }

    private static bool TryParseRouteInt(HttpContext ctx, string key, out int value)
    {
        var routeVal = ctx.GetRouteValue(key);
        if (routeVal is string s && int.TryParse(s, out value))
            return true;
        value = 0;
        return false;
    }
}
