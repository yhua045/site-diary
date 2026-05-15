using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Tests.Unit.Web.Middleware;

public class XUserIdMiddlewareTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (XUserIdMiddleware middleware, bool pipelineCalled) BuildSut()
    {
        var called = false;
        var middleware = new XUserIdMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        // closure trick: we return the flag via a ref-captured bool
        return (middleware, called);
    }

    // Because the closure captures a value-type we need a small wrapper
    private static async Task<(bool pipelineCalled, DefaultHttpContext ctx)> InvokeAsync(
        string? headerValue)
    {
        var pipelineCalled = false;
        RequestDelegate next = _ =>
        {
            pipelineCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new XUserIdMiddleware(next);
        var ctx = new DefaultHttpContext();

        if (headerValue is not null)
            ctx.Request.Headers["X-User-Id"] = headerValue;

        await middleware.InvokeAsync(ctx);
        return (pipelineCalled, ctx);
    }

    // ── Test 31 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task T31_ValidHeader_PopulatesItemsAndContinuesPipeline()
    {
        var (pipelineCalled, ctx) = await InvokeAsync("42");

        ctx.Items.Should().ContainKey(HttpContextKeys.UserId);
        ctx.Items[HttpContextKeys.UserId].Should().Be(42);
        pipelineCalled.Should().BeTrue();
    }

    // ── Test 32 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task T32_HeaderAbsent_DoesNotPopulateItemsAndContinuesPipeline()
    {
        var (pipelineCalled, ctx) = await InvokeAsync(null);

        ctx.Items.Should().NotContainKey(HttpContextKeys.UserId);
        pipelineCalled.Should().BeTrue();
    }

    // ── Test 33 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task T33_NonIntegerHeader_DoesNotPopulateItemsAndContinuesPipeline()
    {
        var (pipelineCalled, ctx) = await InvokeAsync("abc");

        ctx.Items.Should().NotContainKey(HttpContextKeys.UserId);
        pipelineCalled.Should().BeTrue();
    }

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
