namespace SiteDiary.Domain.Interfaces;

public interface IAuditService
{
    Task RecordAsync(
        string entityName,
        int entityId,
        string action,
        int changedByUserId,
        object? changes,
        CancellationToken ct = default);
}
