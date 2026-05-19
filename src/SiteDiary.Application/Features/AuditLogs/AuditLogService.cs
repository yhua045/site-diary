using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Features.AuditLogs;

public class AuditLogService(IUnitOfWork uow) : IAuditLogService
{
    public async Task<PagedResult<AuditHistory>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = uow.AuditHistories.Query();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Include(u => u.ChangedBy)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditHistory>(items, totalCount, page, pageSize);
    }
}
