using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Features.AuditLogs;

public interface IAuditLogService
{
    /// <summary>
    /// Returns a page of audit log entries ordered oldest → newest (ascending Timestamp).
    /// </summary>
    Task<PagedResult<AuditHistory>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
}
