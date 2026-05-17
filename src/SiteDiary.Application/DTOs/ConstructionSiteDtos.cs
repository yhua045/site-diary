namespace SiteDiary.Application.DTOs;

public record ConstructionSiteDto(
    int Id,
    string Name,
    string? Description,
    string Address,
    bool IsArchived);

public record CreateConstructionSiteRequest(
    string Name,
    string? Description,
    string Address);

public record UpdateConstructionSiteRequest(
    string Name,
    string? Description,
    string Address);
