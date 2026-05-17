using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Features.DiaryTemplates;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Unit tests for DiaryTemplatesController post-refactor (Issue #8).
/// Service now returns DiaryTemplate entity; IMapper translates to DiaryTemplateDto at boundary.
/// </summary>
public class DiaryTemplatesControllerTests
{
    private readonly Mock<IDiaryTemplateService> _svc = new();
    private readonly Mock<IMapper> _mapper = new();

    private DiaryTemplatesController MakeController() => new(_svc.Object, _mapper.Object);

    private static DiaryTemplate MakeTemplate(int id = 1) => new()
    {
        Id = id,
        Name = "Test Template",
        Sections = "[]",
        CreatedByUserId = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static DiaryTemplateDto MakeDto(int id = 1) =>
        new(id, "Test Template", new List<SectionDef>
        {
            new() { Id = "s1", Label = "Section 1", Fields = new List<FieldDef>
            {
                new() { Id = "f1", Label = "Notes", Type = "textarea" }
            }}
        });

    [Fact]
    public async Task GetById_ExistingTemplate_Returns200WithMappedDto()
    {
        var entity = MakeTemplate(1);
        var dto = MakeDto(1);

        _svc.Setup(s => s.GetByIdAsync(1, default)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map<DiaryTemplateDto>(entity)).Returns(dto);

        var actionResult = await MakeController().GetById(1, default);

        actionResult.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult.Result!;
        ok.Value.Should().Be(dto);
        _mapper.Verify(m => m.Map<DiaryTemplateDto>(entity), Times.Once);
    }

    [Fact]
    public async Task GetById_UnknownTemplate_Returns404()
    {
        _svc.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((DiaryTemplate?)null);

        var actionResult = await MakeController().GetById(999, default);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }
}
