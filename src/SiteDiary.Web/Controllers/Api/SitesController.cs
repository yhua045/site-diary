using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;

namespace SiteDiary.Web.Controllers.Api;

[ApiController]
[Route("api/sites")]
public class SitesController(ISiteService siteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConstructionSiteDto>>> GetAll(CancellationToken ct) =>
        Ok(await siteService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConstructionSiteDto>> GetById(int id, CancellationToken ct)
    {
        var site = await siteService.GetByIdAsync(id, ct);
        return site is null ? NotFound() : Ok(site);
    }

    [HttpPost]
    public async Task<ActionResult<ConstructionSiteDto>> Create([FromBody] CreateConstructionSiteRequest request, CancellationToken ct)
    {
        var created = await siteService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConstructionSiteDto>> Update(int id, [FromBody] UpdateConstructionSiteRequest request, CancellationToken ct)
    {
        var updated = await siteService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var success = await siteService.ArchiveAsync(id, ct);
        return success ? NoContent() : NotFound();
    }
}
