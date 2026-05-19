using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Controllers.Api;

namespace SiteDiary.Tests.Unit.Web;

public class UsersControllerTests
{
    private static readonly User FakeUser = new()
    {
        Id = 1,
        FirstName = "Alice",
        LastName = "Smith",
        Email = "alice@example.com",
        IsActive = true,
        IsArchived = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static readonly ConstructionSite FakeSite = new()
    {
        Id = 10,
        Name = "Northland Tower",
        Description = null,
        Address = "12 Queen St",
        IsArchived = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static UsersController MakeController(
        IUserService userSvc,
        ISiteService siteSvc,
        IDiaryTemplateService? templateSvc = null) =>
        new(userSvc, siteSvc, templateSvc ?? new Mock<IDiaryTemplateService>().Object);

    [Fact]
    public async Task GetSitesByUserId_ExistingUser_Returns200WithSiteList()
    {
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(FakeUser);

        var mockSites = new Mock<ISiteService>();
        mockSites.Setup(s => s.GetByUserIdAsync(1, default)).ReturnsAsync(new List<ConstructionSite> { FakeSite });

        var controller = MakeController(mockUsers.Object, mockSites.Object);

        var actionResult = await controller.GetSitesByUserId(1, default);

        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var sites = ((OkObjectResult)actionResult.Result!).Value as IReadOnlyList<ConstructionSiteDto>;
        sites.Should().NotBeNull();
        sites!.Should().HaveCount(1);
        sites[0].Name.Should().Be("Northland Tower");
    }

    [Fact]
    public async Task GetSitesByUserId_UnknownUser_Returns404()
    {
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((User?)null);

        var mockSites = new Mock<ISiteService>();
        var controller = MakeController(mockUsers.Object, mockSites.Object);

        var actionResult = await controller.GetSitesByUserId(999, default);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
        mockSites.Verify(s => s.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DiaryTemplate MakeTemplateEntity(int id = 5) => new()
    {
        Id = id,
        Name = "Default Template",
        Sections = "[{\"Id\":\"s1\",\"Label\":\"General\",\"Fields\":[{\"Id\":\"f1\",\"Label\":\"Notes\",\"Type\":\"textarea\"}]}]",
        CreatedByUserId = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetDiaryTemplateByUserId_ExistingUserWithTemplate_Returns200WithDto()
    {
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(FakeUser);

        var mockTemplates = new Mock<IDiaryTemplateService>();
        mockTemplates.Setup(s => s.GetByUserRoleAsync(1, default)).ReturnsAsync(MakeTemplateEntity());

        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        var actionResult = await controller.GetDiaryTemplateByUserId(1, default);

        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var dto = ((OkObjectResult)actionResult.Result!).Value as DiaryTemplateDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(5);
    }

    [Fact]
    public async Task GetDiaryTemplateByUserId_ExistingUserWithNoTemplate_Returns404()
    {
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(FakeUser);

        var mockTemplates = new Mock<IDiaryTemplateService>();
        mockTemplates.Setup(s => s.GetByUserRoleAsync(1, default)).ReturnsAsync((DiaryTemplate?)null);

        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        var actionResult = await controller.GetDiaryTemplateByUserId(1, default);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDiaryTemplateByUserId_UnknownUser_Returns404()
    {
        var mockUsers = new Mock<IUserService>();
        mockUsers.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((User?)null);

        var mockTemplates = new Mock<IDiaryTemplateService>();
        var controller = MakeController(mockUsers.Object, new Mock<ISiteService>().Object, mockTemplates.Object);

        var actionResult = await controller.GetDiaryTemplateByUserId(999, default);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
        mockTemplates.Verify(s => s.GetByUserRoleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
