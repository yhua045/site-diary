using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Features.DiaryTemplates;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Unit tests for DiaryTemplatesController.
/// </summary>
public class DiaryTemplatesControllerTests
{
    private readonly Mock<IDiaryTemplateService> _svc = new();

    private DiaryTemplatesController MakeController() => new(_svc.Object);

    private static DiaryTemplate MakeTemplate(int id = 1) => new()
    {
        Id = id,
        Name = "Test Template",
        Sections = "[]",
        CreatedByUserId = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetById_ExistingTemplate_Returns200WithEntity()
    {
        var entity = MakeTemplate(1);

        _svc.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(entity);

        var actionResult = await MakeController().GetById(1, default);

        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        var template = ok.Value as DiaryTemplate;
        template.Should().NotBeNull();
        template!.Id.Should().Be(1);
        template.Name.Should().Be("Test Template");
    }

    [Fact]
    public async Task GetById_UnknownTemplate_Returns404()
    {
        _svc.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((DiaryTemplate?)null);

        var actionResult = await MakeController().GetById(999, default);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }
}
