using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Controllers.Api;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Controller unit tests for SitesController post-refactor (Issue #8).
/// Service mock returns domain entities; IMapper mock translates at boundary.
/// Phase B: these tests fail until the controller is updated in Phase C.
/// </summary>
public class SitesControllerTests
{
    private readonly Mock<ISiteService> _svc = new();
    private readonly Mock<IMapper> _mapper = new();

    private SitesController MakeController() => new(_svc.Object, _mapper.Object);

    private static ConstructionSite MakeSite(int id = 1) => new()
    {
        Id = id,
        Name = "Site " + id,
        Address = $"{id} Main St",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedDtos()
    {
        var entities = new List<ConstructionSite> { MakeSite(1), MakeSite(2) };
        var dtos = new List<ConstructionSiteDto>
        {
            new(1, "Site 1", null, "1 Main St", false, DateTime.UtcNow, DateTime.UtcNow),
            new(2, "Site 2", null, "2 Main St", false, DateTime.UtcNow, DateTime.UtcNow)
        };

        _svc.Setup(s => s.GetAllAsync(default)).ReturnsAsync(entities);
        _mapper.Setup(m => m.Map<IReadOnlyList<ConstructionSiteDto>>(entities)).Returns(dtos);

        var result = await MakeController().GetAll(default);

        result.Result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dtos);
        _mapper.Verify(m => m.Map<IReadOnlyList<ConstructionSiteDto>>(entities), Times.Once);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingSite_Returns200WithMappedDto()
    {
        var entity = MakeSite(1);
        var dto = new ConstructionSiteDto(1, "Site 1", null, "1 Main St", false, DateTime.UtcNow, DateTime.UtcNow);

        _svc.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map<ConstructionSiteDto>(entity)).Returns(dto);

        var result = await MakeController().GetById(1, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetByIdAsync(99, default)).ReturnsAsync((ConstructionSite?)null);

        var result = await MakeController().GetById(99, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_MapsInputCallsServiceMapsOutput()
    {
        var request = new CreateConstructionSiteRequest("New Site", null, "10 New St");
        var domainSite = MakeSite(0);
        var savedSite = MakeSite(5);
        var responseDto = new ConstructionSiteDto(5, "New Site", null, "10 New St", false, DateTime.UtcNow, DateTime.UtcNow);

        _mapper.Setup(m => m.Map<ConstructionSite>(request)).Returns(domainSite);
        _svc.Setup(s => s.CreateAsync(domainSite, default)).ReturnsAsync(savedSite);
        _mapper.Setup(m => m.Map<ConstructionSiteDto>(savedSite)).Returns(responseDto);

        var result = await MakeController().Create(request, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mapper.Verify(m => m.Map<ConstructionSite>(request), Times.Once);
        _svc.Verify(s => s.CreateAsync(domainSite, default), Times.Once);
        _mapper.Verify(m => m.Map<ConstructionSiteDto>(savedSite), Times.Once);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingSite_Returns200WithMappedDto()
    {
        var request = new UpdateConstructionSiteRequest("Updated", null, "New St");
        var updateValues = MakeSite(0);
        var updatedEntity = MakeSite(1);
        var responseDto = new ConstructionSiteDto(1, "Updated", null, "New St", false, DateTime.UtcNow, DateTime.UtcNow);

        _mapper.Setup(m => m.Map<ConstructionSite>(request)).Returns(updateValues);
        _svc.Setup(s => s.UpdateAsync(1, updateValues, default)).ReturnsAsync(updatedEntity);
        _mapper.Setup(m => m.Map<ConstructionSiteDto>(updatedEntity)).Returns(responseDto);

        var result = await MakeController().Update(1, request, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        _mapper.Verify(m => m.Map<ConstructionSite>(request), Times.Once);
        _mapper.Verify(m => m.Map<ConstructionSiteDto>(updatedEntity), Times.Once);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateConstructionSiteRequest("X", null, "Y");
        _mapper.Setup(m => m.Map<ConstructionSite>(request)).Returns(MakeSite(0));
        _svc.Setup(s => s.UpdateAsync(99, It.IsAny<ConstructionSite>(), default)).ReturnsAsync((ConstructionSite?)null);

        var result = await MakeController().Update(99, request, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ── Archive ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Archive_ExistingSite_Returns204()
    {
        _svc.Setup(s => s.ArchiveAsync(1, default)).ReturnsAsync(true);

        var result = await MakeController().Archive(1, default);

        result.Should().BeOfType<NoContentResult>();
    }
}
