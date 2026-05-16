using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Tests.Unit.Web.Middleware;

public class RequestContextExtractionMiddlewareTests
{
    private static async Task<(bool pipelineCalled, DefaultHttpContext ctx, IRequestSecurityContext secCtx)> InvokeAsync(
        string? headerValue = null,
        Dictionary<string, string>? routeValues = null)
    {
        var pipelineCalled = false;
        RequestDelegate next = _ =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestContextExtractionMiddleware(next);
        var ctx = new DefaultHttpContext();

        if (headerValue is not null)
            ctx.Request.Headers["X-User-Id"] = headerValue;

        if (routeValues is not null)
            foreach (var (key, val) in routeValues)
                ctx.Request.RouteValues[key] = val;

        var secCtx = new RequestSecurityContext();
        await middleware.InvokeAsync(ctx, secCtx);
        return (pipelineCalled, ctx, secCtx);
    }

    // T36
    [Fact]
    public async Task T36_ValidHeader_SetsAuthenticatedUserIdAndItemsAndCallsPipeline()
    {
        var (pipelineCalled, ctx, secCtx) = await InvokeAsync("42");

        secCtx.AuthenticatedUserId.Should().Be(42);
        ctx.Items.Should().ContainKey(HttpContextKeys.UserId);
        ctx.Items[HttpContextKeys.UserId].Should().Be(42);
        pipelineCalled.Should().BeTrue();
    }

    // T37
    [Fact]
    public async Task T37_HeaderAbsent_AuthenticatedUserIdIsNullAndCallsPipeline()
    {
        var (pipelineCalled, _, secCtx) = await InvokeAsync(null);

        secCtx.AuthenticatedUserId.Should().BeNull();
        pipelineCalled.Should().BeTrue();
    }

    // T38
    [Fact]
    public async Task T38_NonIntegerHeader_AuthenticatedUserIdIsNullAndCallsPipeline()
    {
        var (pipelineCalled, _, secCtx) = await InvokeAsync("abc");

        secCtx.AuthenticatedUserId.Should().BeNull();
        pipelineCalled.Should().BeTrue();
    }

    // T39
    [Fact]
    public async Task T39_RouteHasSiteId_SetsRequestedSiteId()
    {
        var (_, _, secCtx) = await InvokeAsync(routeValues: new() { ["siteId"] = "7" });

        secCtx.RequestedSiteId.Should().Be(7);
    }

    // T40
    [Fact]
    public async Task T40_RouteHasDiaryId_SetsRequestedDiaryId()
    {
        var (_, _, secCtx) = await InvokeAsync(routeValues: new() { ["diaryId"] = "99" });

        secCtx.RequestedDiaryId.Should().Be(99);
    }

    // T41
    [Fact]
    public async Task T41_RouteHasSiteIdAndDiaryId_BothSet()
    {
        var (_, _, secCtx) = await InvokeAsync(routeValues: new() { ["siteId"] = "3", ["diaryId"] = "15" });

        secCtx.RequestedSiteId.Should().Be(3);
        secCtx.RequestedDiaryId.Should().Be(15);
    }

    // T42
    [Fact]
    public async Task T42_NoRouteValues_BothNullAndCallsPipeline()
    {
        var (pipelineCalled, _, secCtx) = await InvokeAsync();

        secCtx.RequestedSiteId.Should().BeNull();
        secCtx.RequestedDiaryId.Should().BeNull();
        pipelineCalled.Should().BeTrue();
    }

    // T43
    [Fact]
    public async Task T43_RouteHasUserId_SetsRequestedUserId()
    {
        var (_, _, secCtx) = await InvokeAsync(routeValues: new() { ["userId"] = "5" });

        secCtx.RequestedUserId.Should().Be(5);
    }

    // T44
    [Fact]
    public async Task T44_AllFourPresent_AllFieldsPopulated()
    {
        var (_, _, secCtx) = await InvokeAsync(
            headerValue: "1",
            routeValues: new() { ["siteId"] = "2", ["diaryId"] = "3", ["userId"] = "1" });

        secCtx.AuthenticatedUserId.Should().Be(1);
        secCtx.RequestedSiteId.Should().Be(2);
        secCtx.RequestedDiaryId.Should().Be(3);
        secCtx.RequestedUserId.Should().Be(1);
    }
}
