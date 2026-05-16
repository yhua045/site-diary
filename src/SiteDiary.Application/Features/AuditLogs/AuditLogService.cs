using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.AuditLogs;

public class AuditLogService(IUnitOfWork uow) : IAuditLogService
{
    public async Task<AuditLogPageDto> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = uow.AuditHistories.Query();

        var totalCount = await query.CountAsync(ct);

        var rawItems = await query
            .Include(a => a.ChangedBy)
            .OrderBy(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rawItems
            .Select(a => new AuditLogDto(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Action,
                a.ChangedByUserId,
                a.ChangedBy != null
                    ? $"{a.ChangedBy.FirstName} {a.ChangedBy.LastName}"
                    : "System",
                a.Changes,
                a.Timestamp))
            .ToList();

        return new AuditLogPageDto(items, totalCount, page, pageSize);
    }
}
