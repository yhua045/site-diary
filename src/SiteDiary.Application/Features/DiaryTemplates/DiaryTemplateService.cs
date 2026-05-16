using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.DiaryTemplates;

public class DiaryTemplateService(IUnitOfWork uow) : IDiaryTemplateService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<DiaryTemplateDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // Use Query() so the global IsArchived filter is applied.
        var template = await uow.DiaryTemplates
            .Query()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template is null) return null;

        var sections = DeserializeSections(template.Sections);
        return new DiaryTemplateDto(template.Id, template.Name, sections);
    }

    public async Task<DiaryTemplateDto?> GetByUserRoleAsync(int userId, CancellationToken ct = default)
    {
        // POC: return the single IsDefault=true template for all roles.
        // Future: resolve via UserRole.DiaryTemplateId FK.
        var template = await uow.DiaryTemplates
            .Query()
            .FirstOrDefaultAsync(t => t.IsDefault, ct);

        if (template is null) return null;

        var sections = DeserializeSections(template.Sections);
        return new DiaryTemplateDto(template.Id, template.Name, sections);
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
