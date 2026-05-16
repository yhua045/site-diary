using FluentAssertions;
using Moq;
using SiteDiary.Web.Middleware;
using SiteDiary.Web.Services;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Unit tests for <see cref="CurrentUserService"/>.
/// Verifies that it correctly adapts <see cref="IRequestSecurityContext"/> into
/// the <see cref="SiteDiary.Application.Interfaces.ICurrentUserService"/> contract.
/// </summary>
public class CurrentUserServiceTests
{
    // ── T01 — Integer user ID is returned as its string representation ────────

    [Fact]
    public void T01_AuthenticatedUserId_WhenContextHasIntId_ReturnsStringRepresentation()
    {
        var secCtx = new Mock<IRequestSecurityContext>();
        secCtx.Setup(x => x.AuthenticatedUserId).Returns(42);

        var svc = new CurrentUserService(secCtx.Object);

        svc.AuthenticatedUserId.Should().Be("42");
    }

    // ── T02 — Null security context ID yields null ────────────────────────────

    [Fact]
    public void T02_AuthenticatedUserId_WhenContextHasNullId_ReturnsNull()
    {
        var secCtx = new Mock<IRequestSecurityContext>();
        secCtx.Setup(x => x.AuthenticatedUserId).Returns((int?)null);

        var svc = new CurrentUserService(secCtx.Object);

        svc.AuthenticatedUserId.Should().BeNull();
    }

    // ── T03 — Boundary: user ID = 1 ──────────────────────────────────────────

    [Fact]
    public void T03_AuthenticatedUserId_MinimumValidId_ReturnsCorrectString()
    {
        var secCtx = new Mock<IRequestSecurityContext>();
        secCtx.Setup(x => x.AuthenticatedUserId).Returns(1);

        var svc = new CurrentUserService(secCtx.Object);

        svc.AuthenticatedUserId.Should().Be("1");
    }
}
