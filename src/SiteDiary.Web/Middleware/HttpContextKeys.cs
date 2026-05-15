namespace SiteDiary.Web.Middleware;

public static class HttpContextKeys
{
    // Typed object key — avoids magic strings, prevents accidental key collision
    public static readonly object UserId = new();
}
