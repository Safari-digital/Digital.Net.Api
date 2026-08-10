using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Digital.Net.Lib.Accessors;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Mutations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Digital.Net.Lib.Entities.Interceptors;

/// <summary>
///     Captures every mutation of a tracked entity (any <see cref="Entity" /> not marked
///     <see cref="IUntrackedEntity" />) as an <see cref="EntityMutation" /> row, written in the same
///     transaction as the source change, then broadcasts a signal **after commit**.
/// </summary>
public class MutationTrackingInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    private const int MaxValueLength = 512;

    /// <summary>
    ///     Separates a truncated value from its length suffix, so a reader tells it from an empty field.
    ///     Kept ASCII on purpose: the payload is serialized with the default encoder, which escapes
    ///     non-ASCII, and an escaped marker would be unsearchable in the stored JSON.
    /// </summary>
    public const string TruncationMarker = "...";
    private static readonly HashSet<string> IgnoredProperties = ["Id", "CreatedAt", "UpdatedAt"];

    private static readonly ConcurrentDictionary<Type, FrozenSet<string>> SecretProperties = new();
    private readonly ConcurrentDictionary<DbContext, List<MutationSignal>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is { } context && _pending.TryRemove(context, out var signals) && signals.Count > 0)
            await serviceProvider.GetRequiredService<MutationBroadcaster>()
                .PublishAsync(context, signals, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // Sync saves (mostly test fixtures, whose contexts are not interceptor-wired anyway) don't emit a realtime
    // signal, the persisted EntityMutation is recovered by catch-up. We just drop the stash to avoid a leak.
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context) _pending.TryRemove(context, out _);
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) _pending.TryRemove(context, out _);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is { } context) _pending.TryRemove(context, out _);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e =>
                e.Entity is Entity and not IUntrackedEntity
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            )
            .ToList();
        if (entries.Count == 0) return;

        var origin = serviceProvider.GetRequiredService<IOriginAccessor>().TryGetOrigin();
        var userId = serviceProvider.GetRequiredService<IUserAccessor>().TryGetUserId();
        var now = DateTime.UtcNow;

        var mutations = entries.Select(entry =>
        {
            var entity = (Entity)entry.Entity;
            return new EntityMutation
            {
                ChangeType = entry.State switch
                {
                    EntityState.Added => ChangeType.Created,
                    EntityState.Modified => ChangeType.Updated,
                    _ => ChangeType.Deleted
                },
                EntityType = entity.GetCanonicalType().Name,
                EntityId = entity.Id,
                UserId = userId,
                IpAddress = origin?.IpAddress,
                UserAgent = origin?.UserAgent,
                CreatedAt = now,
                Payload = BuildPayload(entry)
            };
        }).ToList();

        context.AddRange(mutations);
        _pending[context] = mutations
            .Select(m => new MutationSignal(
                m.ChangeType, m.EntityType, m.EntityId, m.CreatedAt, m.Id, m.UserId, origin?.ClientId
            ))
            .ToList();
    }

    private static string? BuildPayload(EntityEntry entry)
    {
        if (entry.State is EntityState.Deleted) return null;
        var isCreate = entry.State is EntityState.Added;
        var secrets = SecretProperties.GetOrAdd(entry.Metadata.ClrType, BuildSecretSet);

        var changes = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (IgnoredProperties.Contains(prop.Metadata.Name)) continue;
            if (secrets.Contains(prop.Metadata.Name)) continue;
            if (!isCreate && !prop.IsModified) continue;

            changes[prop.Metadata.Name] = new
            {
                from = isCreate ? null : Cap(prop.OriginalValue),
                to = Cap(prop.CurrentValue)
            };
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes);
    }

    /// <summary>
    ///     Bounds a value so one oversized field cannot blow up the audit row. The kept prefix matters:
    ///     editorial content lives in tracked fields now, and an audit that only says "3000 chars became
    ///     3200" records that something changed without recording what.
    /// </summary>
    private static object? Cap(object? value) =>
        value is string { Length: > MaxValueLength } s
            ? $"{s[..MaxValueLength]}{TruncationMarker}({s.Length} chars)"
            : value;

    private static FrozenSet<string> BuildSecretSet(Type clrType) =>
        clrType.GetProperties()
            .Where(p => p.GetCustomAttribute<SecretAttribute>() is not null)
            .Select(p => p.Name)
            .ToFrozenSet(StringComparer.Ordinal);
}