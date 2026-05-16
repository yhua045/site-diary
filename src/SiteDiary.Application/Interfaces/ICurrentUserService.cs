namespace SiteDiary.Application.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user to infrastructure-layer components
/// (e.g. audit interceptors) without coupling them to HTTP or ASP.NET Core concerns.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The ID of the currently authenticated user, or <c>null</c> if the request is unauthenticated.
    /// </summary>
    string? AuthenticatedUserId { get; }
}
