namespace SiteDiary.Web.Middleware;

public static class HttpContextKeys
{
    // Typed object keys — avoids magic strings, prevents accidental key collision
    public static readonly object UserId  = new();
    public static readonly object SiteId  = new();
    public static readonly object DiaryId = new();
}
