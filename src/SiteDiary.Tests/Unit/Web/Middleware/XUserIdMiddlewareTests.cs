using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Tests.Unit.Web.Middleware;

/// <summary>
/// Tests for HttpContextExtensions helpers (T34, T35).
/// T31–T33 (XUserIdMiddleware) are superseded by T36–T44 in RequestContextExtractionMiddlewareTests.
/// </summary>
public class HttpContextExtensionsTests
{
    // ── Test 34 ───────────────────────────────────────────────────────────────

    [Fact]
    public void T34_GetCurrentUserId_KeyPopulated_ReturnsUserId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Items[HttpContextKeys.UserId] = 42;

        ctx.GetCurrentUserId().Should().Be(42);
    }

    // ── Test 35 ───────────────────────────────────────────────────────────────

    [Fact]
    public void T35_GetCurrentUserId_KeyAbsent_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();

        ctx.GetCurrentUserId().Should().BeNull();
    }
}
