namespace SiteDiary.Application.Features.DiaryTemplates;

/// <summary>
/// Mirrors the TypeScript FieldDef interface. Deserialized from DiaryTemplate.Sections JSON.
/// </summary>
public record FieldDef
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Required { get; init; } = false;
    public string? Placeholder { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
}

/// <summary>
/// Mirrors the TypeScript SectionDef interface. Deserialized from DiaryTemplate.Sections JSON.
/// </summary>
public record SectionDef
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<FieldDef> Fields { get; init; } = Array.Empty<FieldDef>();
}
