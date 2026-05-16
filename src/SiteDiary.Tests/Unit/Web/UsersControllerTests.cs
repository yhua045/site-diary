using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Interfaces;
using SiteDiary.Web.Controllers.Api;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Unit tests for UsersController — focuses on the new GET /{userId}/sites endpoint.
/// </summary>
public class UsersControllerTests
{
    private static readonly UserDto _fakeUser =
        new(1, "Alice", "Smith", "alice@example.com", true, false, DateTime.UtcNow, DateTime.UtcNow);

    private static readonly ConstructionSiteDto _fakeSite =
        new(10, "Northland Tower", null, "12 Queen St", false, DateTime.UtcNow, DateTime.UtcNow);

    private static UsersController MakeController(IUserService userSvc, ISiteService siteSvc, IDiaryTemplateService? templateSvc = null) =>
        new(userSvc, siteSvc, templateSvc ?? new Mock<IDiaryTemplateService>().Object);

    [Fact]
    public async Task GetSitesByUserId_ExistingUser_Returns200WithSiteList()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(_fakeUser);

        var mockSites = new Mock<ISiteService>();
        mockSites.Setup(s => s.GetByUserIdAsync(1, default))
                 .ReturnsAsync(new List<ConstructionSiteDto> { _fakeSite });

        var controller = MakeController(mockUsers.Object, mockSites.Object);

        // Act
        var actionResult = await controller.GetSitesByUserId(1, default);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        var sites = ok.Value as IReadOnlyList<ConstructionSiteDto>;
        sites.Should().NotBeNull();
        sites!.Should().HaveCount(1);
        sites[0].Name.Should().Be("Northland Tower");
    }

    [Fact]
    public async Task GetSitesByUserId_EmptyAssignments_Returns200WithEmptyList()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(2, default)).ReturnsAsync(_fakeUser with { Id = 2 });

        var mockSites = new Mock<ISiteService>();
        mockSites.Setup(s => s.GetByUserIdAsync(2, default))
                 .ReturnsAsync(Array.Empty<ConstructionSiteDto>());

        var controller = MakeController(mockUsers.Object, mockSites.Object);

        // Act
        var actionResult = await controller.GetSitesByUserId(2, default);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        var sites = ok.Value as IReadOnlyList<ConstructionSiteDto>;
        sites.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSitesByUserId_UnknownUser_Returns404()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((UserDto?)null);

        var mockSites = new Mock<ISiteService>();

        var controller = MakeController(mockUsers.Object, mockSites.Object);

        // Act
        var actionResult = await controller.GetSitesByUserId(999, default);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundResult>();
        mockSites.Verify(s => s.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GET /{userId}/diary-template tests ───────────────────────────────────

    private static DiaryTemplateDto MakeTemplateDto(int id = 5) =>
        new(id, "Default Template", new List<SectionDef>
        {
            new() { Id = "s1", Label = "General", Fields = new List<FieldDef>
            {
                new() { Id = "f1", Label = "Notes", Type = "textarea" }
            }}
        });

    [Fact]
    public async Task GetDiaryTemplateByUserId_ExistingUserWithTemplate_Returns200WithDto()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(_fakeUser);

        var mockTemplates = new Mock<IDiaryTemplateService>();
        mockTemplates.Setup(s => s.GetByUserRoleAsync(1, default)).ReturnsAsync(MakeTemplateDto());

        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        // Act
        var actionResult = await controller.GetDiaryTemplateByUserId(1, default);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        var dto = ok.Value as DiaryTemplateDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(5);
    }

    [Fact]
    public async Task GetDiaryTemplateByUserId_ExistingUserWithNoTemplate_Returns404()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(_fakeUser);

        var mockTemplates = new Mock<IDiaryTemplateService>();
        mockTemplates.Setup(s => s.GetByUserRoleAsync(1, default)).ReturnsAsync((DiaryTemplateDto?)null);

        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        // Act
        var actionResult = await controller.GetDiaryTemplateByUserId(1, default);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDiaryTemplateByUserId_UnknownUser_Returns404()
    {
        // Arrange
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((UserDto?)null);

        var mockTemplates = new Mock<IDiaryTemplateService>();

        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        // Act
        var actionResult = await controller.GetDiaryTemplateByUserId(999, default);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundResult>();
        mockTemplates.Verify(s => s.GetByUserRoleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
