using System.Text.Json;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Infrastructure.Services;

public class AuditService(IUnitOfWork uow) : IAuditService
{
    public async Task RecordAsync(
        string entityName,
        int entityId,
        string action,
        int changedByUserId,
        object? changes,
        CancellationToken ct = default)
    {
        var entry = new AuditHistory
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangedByUserId = changedByUserId,
            Changes = changes is null ? null : JsonSerializer.Serialize(changes),
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await uow.AuditHistories.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);
    }
}
