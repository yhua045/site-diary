using Microsoft.AspNetCore.Authorization;
using SiteDiary.Application.Interfaces;

namespace SiteDiary.Web.Middleware;

public sealed class ResourceAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext ctx,
        IRequestSecurityContext secCtx,
        ISiteAuthorizationService authSvc)
    {
        // Rule -1: bypass — endpoint explicitly opted out of resource authorization
        var endpoint = ctx.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipResourceAuthorizationAttribute>() is not null
            || endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(ctx);
            return;
        }

        // Rule 0: X-User-Id is required on ALL non-bypassed endpoints
        if (secCtx.AuthenticatedUserId is not { } userId)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Rule 1: userId route parameter must exactly match the authenticated user
        if (secCtx.RequestedUserId is { } routeUserId && userId != routeUserId)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Rule 2: no site/diary context → pass through (caller is authenticated)
        if (secCtx.RequestedSiteId is null && secCtx.RequestedDiaryId is null)
        {
            await next(ctx);
            return;
        }

        // Rule 3: siteId available → membership check
        if (secCtx.RequestedSiteId is { } siteId)
        {
            if (!await authSvc.IsUserMemberOfSiteAsync(userId, siteId, ctx.RequestAborted))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(ctx);
            return;
        }

        // Rule 4: diaryId only → diary-based check
        if (!await authSvc.IsUserAuthorizedForDiaryAsync(userId, secCtx.RequestedDiaryId!.Value, ctx.RequestAborted))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(ctx);
    }
}
