using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.DiaryTemplates;

public class DiaryTemplateService(IUnitOfWork uow) : IDiaryTemplateService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<DiaryTemplate?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // Use Query() so the global IsArchived filter is applied.
        var template = await uow.DiaryTemplates
            .Query()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return template;
    }

    public async Task<DiaryTemplate?> GetByUserRoleAsync(int userId, CancellationToken ct = default)
    {
        // Resolve: User → active UserRole → Role
        var role = await uow.UserRoles
            .Query()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.Role)
            .FirstOrDefaultAsync(ct);

        // Look up the role-specific template by RoleId
        var template = role is not null
            ? await uow.DiaryTemplates
                   .Query()
                   .FirstOrDefaultAsync(t => t.RoleId == role.Id, ct)
            : null;

        // Fall back to the system default if no role-specific template found
        template ??= await uow.DiaryTemplates
               .Query()
               .FirstOrDefaultAsync(t => t.IsDefault, ct);

        return template;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<SectionDef> DeserializeSections(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return Array.Empty<SectionDef>();

        return JsonSerializer.Deserialize<List<SectionDef>>(json, _jsonOptions)
               ?? (IReadOnlyList<SectionDef>)Array.Empty<SectionDef>();
    }
}
