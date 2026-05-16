namespace SiteDiary.Application.Features.DiaryTemplates;

public record DiaryTemplateDto(
    int Id,
    string Name,
    IReadOnlyList<SectionDef> Sections);
