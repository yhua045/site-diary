using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Infrastructure.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that emits a structured audit log entry
/// for every Added, Modified, or Deleted entity before the changes are committed.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUserService currentUserService,
    ILogger<AuditSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        LogAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        LogAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void LogAuditEntries(DbContext? context)
    {
        if (context is null) return;

        var userIdStr = currentUserService.AuthenticatedUserId ?? "anonymous";
        var parsedUserId = int.TryParse(currentUserService.AuthenticatedUserId, out var uid)
            ? (int?)uid
            : null;
        var timestamp = DateTime.UtcNow;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditHistory)   // self-audit guard — prevents recursion
            .ToList();

        foreach (var entry in entries)
        {
            var entityName = entry.Metadata.Name;
            var primaryKey = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => $"{p.Metadata.Name}={p.CurrentValue}")
                .FirstOrDefault() ?? "unknown";

            var entityId = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue is int i ? i : 0)
                .FirstOrDefault();

            var state = entry.State switch
            {
                EntityState.Added    => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted  => "Delete",
                _                    => "Unknown"
            };

            string? changesJson = null;
            if (entry.State == EntityState.Modified)
            {
                var changes = entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => new
                    {
                        Property = p.Metadata.Name,
                        OldValue = p.OriginalValue,
                        NewValue = p.CurrentValue
                    })
                    .ToList();

                changesJson = JsonSerializer.Serialize(changes);

                logger.LogInformation(
                    "Audit: {State} on {Entity} [{PrimaryKey}] by User={UserId} at {Timestamp}. Changes: {@Changes}",
                    state, entityName, primaryKey, userIdStr, timestamp, changes);
            }
            else
            {
                logger.LogInformation(
                    "Audit: {State} on {Entity} [{PrimaryKey}] by User={UserId} at {Timestamp}",
                    state, entityName, primaryKey, userIdStr, timestamp);
            }

            // Write audit row into the same DbContext — EF includes it in the current transaction.
            // Use eventData.Context (passed as `context`) rather than an injected field to avoid
            // multi-context mismatches.
            context.Set<AuditHistory>().Add(new AuditHistory
            {
                EntityName  = entry.Metadata.ClrType.Name,
                EntityId    = entityId,
                Action      = state,
                ChangedByUserId = parsedUserId,
                Changes     = changesJson,
                Timestamp   = timestamp,
                CreatedAt   = timestamp,
                UpdatedAt   = timestamp
            });
        }
    }
}
