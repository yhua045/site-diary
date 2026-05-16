using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Shared;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Features.Diaries;

[ApiController]
[Route("api/sites/{siteId:int}/diaries")]
public class DiariesController(IDiaryService diaryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiaryDto>>> GetAll(int siteId, CancellationToken ct) =>
        Ok(await diaryService.GetBySiteIdAsync(siteId, ct));

    [HttpGet("timeline")]
    public async Task<ActionResult<IReadOnlyList<DiaryTimelineEntryDto>>> GetTimeline(int siteId, CancellationToken ct) =>
        Ok(await diaryService.GetTimelineAsync(siteId, ct));

    [HttpGet("{diaryId:int}")]
    public async Task<ActionResult<DiaryDetailDto>> GetById(int siteId, int diaryId, CancellationToken ct)
    {
        var diary = await diaryService.GetByIdWithAttachmentsAsync(siteId, diaryId, ct);
        return diary is null ? NotFound() : Ok(diary);
    }

    [HttpPost]
    public async Task<ActionResult<DiaryDto>> Create(int siteId, [FromBody] CreateDiaryDto dto, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var created = await diaryService.CreateAsync(siteId, userId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { siteId, diaryId = created.Id }, created);
    }

    [HttpPut("{diaryId:int}")]
    public async Task<ActionResult<DiaryDto>> Update(int siteId, int diaryId,
        [FromBody] UpdateDiaryDto dto, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var result = await diaryService.UpdateAsync(siteId, diaryId, userId, dto, ct);
        return result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{diaryId:int}")]
    public async Task<IActionResult> Delete(int siteId, int diaryId, CancellationToken ct)
    {
        if (HttpContext.GetCurrentUserId() is not { } userId)
            return BadRequest("X-User-Id header is required and must be a valid integer.");

        var result = await diaryService.DeleteAsync(siteId, diaryId, userId, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
