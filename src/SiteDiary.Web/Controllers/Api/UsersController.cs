using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
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
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
    {
        var users = await userService.GetAllAsync(ct);
        return Ok(users.Select(MapUserToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(MapUserToDto(user));
    }

    [HttpGet("{userId:int}/sites")]
    public async Task<ActionResult<IReadOnlyList<ConstructionSiteDto>>> GetSitesByUserId(int userId, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();

        var sites = await siteService.GetByUserIdAsync(userId, ct);
        return Ok(sites.Select(MapSiteToDto).ToList());
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
        return template is null ? NotFound() : Ok(MapTemplateToDto(template));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email
        };
        var created = await userService.CreateAsync(user, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapUserToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var updateValues = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email
        };
        var updated = await userService.UpdateAsync(id, updateValues, ct);
        return updated is null ? NotFound() : Ok(MapUserToDto(updated));
    }

    private static UserDto MapUserToDto(User u) =>
        new(u.Id, u.FirstName, u.LastName, u.Email, u.IsArchived);

    private static ConstructionSiteDto MapSiteToDto(ConstructionSite s) =>
        new(s.Id, s.Name, s.Description, s.Address, s.IsArchived);

    private static DiaryTemplateDto MapTemplateToDto(DiaryTemplate t)
    {
        var sections = DeserializeSections(t.Sections);
        return new DiaryTemplateDto(t.Id, t.Name, sections);
    }

    private static IReadOnlyList<SectionDef> DeserializeSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SectionDef>();
        return System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<SectionDef>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? Array.Empty<SectionDef>();
    }
}

