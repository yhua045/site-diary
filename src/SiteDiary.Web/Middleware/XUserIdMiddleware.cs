namespace SiteDiary.Web.Middleware;

public sealed class XUserIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-User-Id";

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var raw)
            && int.TryParse(raw, out var userId))
        {
            context.Items[HttpContextKeys.UserId] = userId;
        }
        // Always continue — enforcement is per-action, not global
        return next(context);
    }
}
