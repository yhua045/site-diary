using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.Features.DiaryTemplates;

namespace SiteDiary.Web.Features.DiaryTemplates;

[ApiController]
[Route("api/diary-templates")]
public class DiaryTemplatesController(IDiaryTemplateService templateService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DiaryTemplateDto>> GetById(int id, CancellationToken ct)
    {
        var template = await templateService.GetByIdAsync(id, ct);
        return template is null ? NotFound() : Ok(template);
    }
}
