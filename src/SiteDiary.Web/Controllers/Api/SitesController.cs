using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Web.Controllers.Api;

[ApiController]
[Route("api/sites")]
public class SitesController(ISiteService siteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConstructionSiteDto>>> GetAll(CancellationToken ct)
    {
        var sites = await siteService.GetAllAsync(ct);
        return Ok(sites.Select(MapToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConstructionSiteDto>> GetById(int id, CancellationToken ct)
    {
        var site = await siteService.GetByIdAsync(id, ct);
        return site is null ? NotFound() : Ok(MapToDto(site));
    }

    [HttpPost]
    public async Task<ActionResult<ConstructionSiteDto>> Create([FromBody] CreateConstructionSiteRequest request, CancellationToken ct)
    {
        var site = new ConstructionSite
        {
            Name = request.Name,
            Description = request.Description,
            Address = request.Address
        };
        var created = await siteService.CreateAsync(site, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConstructionSiteDto>> Update(int id, [FromBody] UpdateConstructionSiteRequest request, CancellationToken ct)
    {
        var updateValues = new ConstructionSite
        {
            Name = request.Name,
            Description = request.Description,
            Address = request.Address
        };
        var updated = await siteService.UpdateAsync(id, updateValues, ct);
        return updated is null ? NotFound() : Ok(MapToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var success = await siteService.ArchiveAsync(id, ct);
        return success ? NoContent() : NotFound();
    }

    private static ConstructionSiteDto MapToDto(ConstructionSite s) =>
        new(s.Id, s.Name, s.Description, s.Address, s.IsArchived);
}
