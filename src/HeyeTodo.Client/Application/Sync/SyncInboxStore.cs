using System.Collections.Generic;
using HeyeTodo.Client.Data;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Shared.Contracts.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncInboxStore
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;
    private readonly IClientLogger _logger;

    public SyncInboxStore(IDbContextFactory<LocalDbContext> dbFactory, IClientLogger logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task RecordReceivedAsync(Guid ownerId, IReadOnlyList<SyncChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var addedCount = 0;
        foreach (var change in changes)
        {
            var serverVersion = ExtractServerVersion(change);
            var exists = await db.Inbox.AnyAsync(x =>
                x.OwnerId == ownerId
                && x.EntityType == change.EntityType
                && x.EntityId == change.EntityId
                && x.ServerVersion == serverVersion,
                ct);
            if (exists)
            {
                continue;
            }

            db.Inbox.Add(new LocalInboxItem
            {
                OwnerId = ownerId,
                EntityType = change.EntityType,
                Operation = change.Operation,
                EntityId = change.EntityId,
                ServerVersion = serverVersion,
                PayloadJson = change.PayloadJson,
                UpdatedAt = change.UpdatedAt,
                UpdatedBy = change.UpdatedBy,
                ClientId = change.ClientId,
            });
            addedCount++;
        }

        await db.SaveChangesAsync(ct);
        await _logger.LogSyncOperationAsync("InboxRecordReceived", ClientLogLevel.Information, "Inbox changes recorded.", new Dictionary<string, object?>
        {
            ["ownerId"] = ownerId,
            ["receivedCount"] = changes.Count,
            ["addedCount"] = addedCount,
        }, ct: ct);
    }

    public async Task MarkAppliedAsync(Guid ownerId, IReadOnlyList<SyncChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var markedCount = 0;
        foreach (var change in changes)
        {
            var serverVersion = ExtractServerVersion(change);
            var item = await db.Inbox.FirstOrDefaultAsync(x =>
                x.OwnerId == ownerId
                && x.EntityType == change.EntityType
                && x.EntityId == change.EntityId
                && x.ServerVersion == serverVersion,
                ct);
            if (item is not null)
            {
                item.AppliedAt = now;
                markedCount++;
            }
        }

        await db.SaveChangesAsync(ct);
        await _logger.LogSyncOperationAsync("InboxMarkApplied", ClientLogLevel.Information, "Inbox changes marked as applied.", new Dictionary<string, object?>
        {
            ["ownerId"] = ownerId,
            ["requestedCount"] = changes.Count,
            ["markedCount"] = markedCount,
        }, ct: ct);
    }

    private static long ExtractServerVersion(SyncChange change)
    {
        using var document = System.Text.Json.JsonDocument.Parse(change.PayloadJson);
        if (document.RootElement.TryGetProperty("sync", out var sync)
            && sync.TryGetProperty("serverVersion", out var version)
            && version.TryGetInt64(out var value))
        {
            return value;
        }

        return 0;
    }
}
