using SiteDiary.Application.Interfaces;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Services;

/// <summary>
/// Adapts the request-scoped <see cref="IRequestSecurityContext"/> into the
/// <see cref="ICurrentUserService"/> abstraction consumed by infrastructure components.
/// </summary>
public sealed class CurrentUserService(IRequestSecurityContext securityContext) : ICurrentUserService
{
    /// <inheritdoc/>
    public string? AuthenticatedUserId =>
        securityContext.AuthenticatedUserId?.ToString();
}
