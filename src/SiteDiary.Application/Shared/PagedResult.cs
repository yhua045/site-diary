namespace SiteDiary.Application.Shared;

/// <summary>Generic pagination wrapper returned by services. T is always a domain entity.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
