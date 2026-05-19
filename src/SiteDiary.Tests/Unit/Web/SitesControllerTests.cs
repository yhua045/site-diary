using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Controllers.Api;

namespace SiteDiary.Tests.Unit.Web;

public class SitesControllerTests
{
    private readonly Mock<ISiteService> _svc = new();

    private SitesController MakeController() => new(_svc.Object);

    private static ConstructionSite MakeSite(int id = 1) => new()
    {
        Id = id,
        Name = "Site " + id,
        Address = $"{id} Main St",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAll_ReturnsMappedDtos()
    {
        var entities = new List<ConstructionSite> { MakeSite(1), MakeSite(2) };
        _svc.Setup(s => s.GetAllAsync(default)).ReturnsAsync(entities);

        var result = await MakeController().GetAll(default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var dtos = ((OkObjectResult)result.Result!).Value as IReadOnlyList<ConstructionSiteDto>;
        dtos.Should().NotBeNull();
        dtos!.Should().HaveCount(2);
        dtos[0].Name.Should().Be("Site 1");
        dtos[1].Name.Should().Be("Site 2");
    }

    [Fact]
    public async Task GetById_ExistingSite_Returns200WithMappedDto()
    {
        var entity = MakeSite(1);
        _svc.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(entity);

        var result = await MakeController().GetById(1, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var dto = ((OkObjectResult)result.Result!).Value as ConstructionSiteDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(1);
        dto.Name.Should().Be("Site 1");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetByIdAsync(99, default)).ReturnsAsync((ConstructionSite?)null);

        var result = await MakeController().GetById(99, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ValidRequest_MapsInputCallsServiceMapsOutput()
    {
        var request = new CreateConstructionSiteRequest("New Site", null, "10 New St");
        var savedSite = MakeSite(5);

        _svc.Setup(s => s.CreateAsync(It.Is<ConstructionSite>(site =>
            site.Name == request.Name && site.Description == request.Description && site.Address == request.Address), default))
            .ReturnsAsync(savedSite);

        var result = await MakeController().Create(request, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var dto = ((CreatedAtActionResult)result.Result!).Value as ConstructionSiteDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(5);
    }

    [Fact]
    public async Task Update_ExistingSite_Returns200WithMappedDto()
    {
        var request = new UpdateConstructionSiteRequest("Updated", null, "New St");
        var updatedEntity = MakeSite(1);

        _svc.Setup(s => s.UpdateAsync(1, It.Is<ConstructionSite>(site =>
            site.Name == request.Name && site.Description == request.Description && site.Address == request.Address), default))
            .ReturnsAsync(updatedEntity);

        var result = await MakeController().Update(1, request, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var dto = ((OkObjectResult)result.Result!).Value as ConstructionSiteDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateConstructionSiteRequest("X", null, "Y");
        _svc.Setup(s => s.UpdateAsync(99, It.IsAny<ConstructionSite>(), default)).ReturnsAsync((ConstructionSite?)null);

        var result = await MakeController().Update(99, request, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Archive_ExistingSite_Returns204()
    {
        _svc.Setup(s => s.ArchiveAsync(1, default)).ReturnsAsync(true);

        var result = await MakeController().Archive(1, default);

        result.Should().BeOfType<NoContentResult>();
    }
}
