namespace SiteDiary.Application.Features.DiaryTemplates;

/// <summary>
/// Represents per-diary field overrides relative to a base template.
/// Stored as JSON in Diary.FieldOverrides.
/// </summary>
public record FieldOverridesDto
{
    /// <summary>Field ids from the base template that are hidden for this diary entry.</summary>
    public IReadOnlyList<string> Removed { get; init; } = Array.Empty<string>();

    /// <summary>User-defined custom fields appended to this diary entry.</summary>
    public IReadOnlyList<FieldDef> Added { get; init; } = Array.Empty<FieldDef>();
}
