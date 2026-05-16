using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Interfaces;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Controllers.Api;

[ApiController]
[Route("api/users")]
public class UsersController(
    IUserService userService,
    ISiteService siteService,
    IDiaryTemplateService templateService) : ControllerBase
{
    [HttpGet]
    [SkipResourceAuthorization]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct) =>
        Ok(await userService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("{userId:int}/sites")]
    public async Task<ActionResult<IReadOnlyList<ConstructionSiteDto>>> GetSitesByUserId(int userId, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();

        var sites = await siteService.GetByUserIdAsync(userId, ct);
        return Ok(sites);
    }

    /// <summary>
    /// Returns the diary template assigned to the user's role.
    /// The API resolves the correct template — the user has no template selector.
    /// </summary>
    [HttpGet("{userId:int}/diary-template")]
    public async Task<ActionResult<DiaryTemplateDto>> GetDiaryTemplateByUserId(int userId, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();

        var template = await templateService.GetByUserRoleAsync(userId, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var created = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var updated = await userService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}

