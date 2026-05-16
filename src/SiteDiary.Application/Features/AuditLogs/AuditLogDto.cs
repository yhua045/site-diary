namespace SiteDiary.Application.Features.AuditLogs;

public sealed record AuditLogDto(
    int      Id,
    string   EntityName,
    int      EntityId,
    string   Action,
    int?     ChangedByUserId,
    string   ChangedByUserName,
    string?  Changes,
    DateTime Timestamp);

public sealed record AuditLogPageDto(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
