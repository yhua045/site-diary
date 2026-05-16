using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using SiteDiary.Application.Interfaces;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Tests.Unit.Web.Middleware;

public class ResourceAuthorizationMiddlewareTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class AllowAnonymousStub : IAllowAnonymous { }

    private static Endpoint MakeEndpoint(params object[] metadata)
        => new(null, new EndpointMetadataCollection(metadata), "TestEndpoint");

    private static async Task<(bool pipelineCalled, int statusCode)> InvokeAsync(
        IRequestSecurityContext secCtx,
        ISiteAuthorizationService authSvc,
        Endpoint? endpoint = null)
    {
        var pipelineCalled = false;
        RequestDelegate next = _ =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ResourceAuthorizationMiddleware(next);
        var ctx = new DefaultHttpContext();
        ctx.Response.StatusCode = 200; // default

        if (endpoint is not null)
            ctx.SetEndpoint(endpoint);

        await middleware.InvokeAsync(ctx, secCtx, authSvc);
        return (pipelineCalled, ctx.Response.StatusCode);
    }

    private static Mock<ISiteAuthorizationService> NoCallAuthSvc()
    {
        var mock = new Mock<ISiteAuthorizationService>(MockBehavior.Strict);
        return mock;
    }

    // T45: [SkipResourceAuthorization], no header → bypass
    [Fact]
    public async Task T45_SkipAttribute_NoHeader_BypassesAll()
    {
        var secCtx = new RequestSecurityContext(); // no userId
        var authSvc = NoCallAuthSvc();
        var ep = MakeEndpoint(new SkipResourceAuthorizationAttribute());

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object, ep);

        called.Should().BeTrue();
        status.Should().NotBe(401).And.NotBe(403);
    }

    // T46: [AllowAnonymous], no header → bypass
    [Fact]
    public async Task T46_AllowAnonymous_NoHeader_BypassesAll()
    {
        var secCtx = new RequestSecurityContext();
        var authSvc = NoCallAuthSvc();
        var ep = MakeEndpoint(new AllowAnonymousStub());

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object, ep);

        called.Should().BeTrue();
        status.Should().NotBe(401).And.NotBe(403);
    }

    // T47: [SkipResourceAuthorization], siteId present, no header → bypass
    [Fact]
    public async Task T47_SkipAttribute_SiteIdPresent_NoHeader_BypassesAll()
    {
        var secCtx = new RequestSecurityContext { RequestedSiteId = 1 };
        var authSvc = NoCallAuthSvc();
        var ep = MakeEndpoint(new SkipResourceAuthorizationAttribute());

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object, ep);

        called.Should().BeTrue();
        status.Should().NotBe(401).And.NotBe(403);
    }

    // T48: No bypass, no header, no siteId/diaryId → 401
    [Fact]
    public async Task T48_NoBypas_NoHeader_NoSiteOrDiary_Returns401()
    {
        var secCtx = new RequestSecurityContext();
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(401);
    }

    // T49: No bypass, has header, no siteId/diaryId, no routeUserId → pass through
    [Fact]
    public async Task T49_HasHeader_NoSiteOrDiary_NoRouteUserId_PassesThrough()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeTrue();
        status.Should().Be(200);
    }

    // T50: siteId present, no header → 401
    [Fact]
    public async Task T50_SiteIdPresent_NoHeader_Returns401()
    {
        var secCtx = new RequestSecurityContext { RequestedSiteId = 1 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(401);
    }

    // T51: siteId present, valid header, member → pass through
    [Fact]
    public async Task T51_SiteIdPresent_ValidHeader_Member_PassesThrough()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1, RequestedSiteId = 5 };
        var authSvc = new Mock<ISiteAuthorizationService>();
        authSvc.Setup(s => s.IsUserMemberOfSiteAsync(1, 5, It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeTrue();
        status.Should().Be(200);
    }

    // T52: siteId present, valid header, NOT member → 403
    [Fact]
    public async Task T52_SiteIdPresent_ValidHeader_NotMember_Returns403()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 2, RequestedSiteId = 5 };
        var authSvc = new Mock<ISiteAuthorizationService>();
        authSvc.Setup(s => s.IsUserMemberOfSiteAsync(2, 5, It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(403);
    }

    // T53: diaryId only, no header → 401
    [Fact]
    public async Task T53_DiaryIdOnly_NoHeader_Returns401()
    {
        var secCtx = new RequestSecurityContext { RequestedDiaryId = 10 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(401);
    }

    // T54: diaryId only, valid header, authorized → pass through
    [Fact]
    public async Task T54_DiaryIdOnly_ValidHeader_Authorized_PassesThrough()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1, RequestedDiaryId = 10 };
        var authSvc = new Mock<ISiteAuthorizationService>();
        authSvc.Setup(s => s.IsUserAuthorizedForDiaryAsync(1, 10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeTrue();
        status.Should().Be(200);
    }

    // T55: diaryId only, valid header, NOT authorized → 403
    [Fact]
    public async Task T55_DiaryIdOnly_ValidHeader_NotAuthorized_Returns403()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 2, RequestedDiaryId = 10 };
        var authSvc = new Mock<ISiteAuthorizationService>();
        authSvc.Setup(s => s.IsUserAuthorizedForDiaryAsync(2, 10, It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(403);
    }

    // T56: diaryId only, diary not found → 403
    [Fact]
    public async Task T56_DiaryIdOnly_DiaryNotFound_Returns403()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1, RequestedDiaryId = 999 };
        var authSvc = new Mock<ISiteAuthorizationService>();
        authSvc.Setup(s => s.IsUserAuthorizedForDiaryAsync(1, 999, It.IsAny<CancellationToken>()))
               .ReturnsAsync(false); // service returns false when diary not found

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(403);
    }

    // T57: siteId + diaryId present → uses siteId path, diary check NOT called
    [Fact]
    public async Task T57_SiteIdAndDiaryId_UsesSiteIdPath_DiaryCheckNotCalled()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1, RequestedSiteId = 5, RequestedDiaryId = 10 };
        var authSvc = new Mock<ISiteAuthorizationService>(MockBehavior.Strict);
        authSvc.Setup(s => s.IsUserMemberOfSiteAsync(1, 5, It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);
        // IsUserAuthorizedForDiaryAsync is NOT set up — strict mock would throw if called

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeTrue();
        status.Should().Be(200);
        authSvc.Verify(s => s.IsUserMemberOfSiteAsync(1, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    // T58: userId in route matches X-User-Id, no siteId/diaryId → pass through
    [Fact]
    public async Task T58_RouteUserIdMatchesHeader_PassesThrough()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 3, RequestedUserId = 3 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeTrue();
        status.Should().Be(200);
    }

    // T59: userId in route mismatches X-User-Id → 403
    [Fact]
    public async Task T59_RouteUserIdMismatchesHeader_Returns403()
    {
        var secCtx = new RequestSecurityContext { AuthenticatedUserId = 1, RequestedUserId = 2 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(403);
    }

    // T60: userId in route, X-User-Id absent → 401
    [Fact]
    public async Task T60_RouteUserIdPresent_NoHeader_Returns401()
    {
        var secCtx = new RequestSecurityContext { RequestedUserId = 2 };
        var authSvc = NoCallAuthSvc();

        var (called, status) = await InvokeAsync(secCtx, authSvc.Object);

        called.Should().BeFalse();
        status.Should().Be(401);
    }
}
