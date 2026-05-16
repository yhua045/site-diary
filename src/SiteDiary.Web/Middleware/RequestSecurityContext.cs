namespace SiteDiary.Web.Middleware;

public sealed class RequestSecurityContext : IRequestSecurityContext
{
    public int? AuthenticatedUserId { get; set; }
    public int? RequestedSiteId { get; set; }
    public int? RequestedDiaryId { get; set; }
    public int? RequestedUserId { get; set; }
}
