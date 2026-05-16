using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Web.Features.DiaryTemplates;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Unit tests for DiaryTemplatesController using Moq.
/// </summary>
public class DiaryTemplatesControllerTests
{
    private static DiaryTemplatesController MakeController(IDiaryTemplateService svc) =>
        new(svc);

    private static DiaryTemplateDto MakeDto(int id = 1) =>
        new(id, "Test Template", new List<SectionDef>
        {
            new() { Id = "s1", Label = "Section 1", Fields = new List<FieldDef>
            {
                new() { Id = "f1", Label = "Notes", Type = "textarea" }
            }}
        });

    [Fact]
    public async Task GetById_ExistingTemplate_Returns200WithDto()
    {
        // Arrange
        var dto = MakeDto(1);
        var mockSvc = new Mock<IDiaryTemplateService>();
        mockSvc.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(dto);

        var controller = MakeController(mockSvc.Object);

        // Act
        var actionResult = await controller.GetById(1, default);

        // Assert
        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetById_UnknownTemplate_Returns404()
    {
        // Arrange
        var mockSvc = new Mock<IDiaryTemplateService>();
        mockSvc.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((DiaryTemplateDto?)null);

        var controller = MakeController(mockSvc.Object);

        // Act
        var actionResult = await controller.GetById(999, default);

        // Assert
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }
}
