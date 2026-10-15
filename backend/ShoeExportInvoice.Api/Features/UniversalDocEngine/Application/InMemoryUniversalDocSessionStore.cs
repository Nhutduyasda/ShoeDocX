using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public interface IUniversalDocSessionStore
{
    UniversalDocSession Create(UniversalDocActor actor, string workflow);
    UniversalDocSession Read(UniversalDocActor actor, string id);
    UniversalDocSession Update(UniversalDocActor actor, string id, long expectedVersion,
        Func<UniversalDocSession, UniversalDocSession> transition);
    void Remove(UniversalDocActor actor, string id, long expectedVersion);
    void Cleanup();
}
public sealed class InMemoryUniversalDocSessionStore(TimeProvider time) : IUniversalDocSessionStore, IDisposable
{
    private sealed class Entry(UniversalDocSession snapshot)
    {
        internal readonly object Gate = new();
        internal UniversalDocSession? Snapshot = snapshot;
    }
    private readonly ConcurrentDictionary<string, Entry> sessions = new(StringComparer.Ordinal);
    private readonly object admission = new();
    private bool disposed;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);
    public const int MaximumSessions = 16, PerTenant = 6, PerOwner = 2, MaximumTemplateBytes = 2 * 1024 * 1024;

    public UniversalDocSession Create(UniversalDocActor actor, string workflow)
    {
        lock (admission)
        {
            if (disposed) throw new ObjectDisposedException(nameof(InMemoryUniversalDocSessionStore));
            RequireIdentity(actor);
            Cleanup();
            var current = sessions.Values.Select(e => { lock (e.Gate) return e.Snapshot; }).Where(s => s is not null).ToArray();
            if (current.Length >= MaximumSessions || current.Count(s => s!.TenantId == actor.TenantId) >= PerTenant ||
                current.Count(s => s!.TenantId == actor.TenantId && s.OwnerId == actor.UserId) >= PerOwner)
                throw new StudioFault(429, "SESSION_CAPACITY_REACHED");
            var profile = StudioDemoCatalog.Profile(workflow, actor.TenantId);
            var now = time.GetUtcNow(); var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var snapshot = new UniversalDocSession(id, actor.UserId, actor.TenantId, workflow, now, now + Lifetime, 1,
                profile, profile, System.Collections.Immutable.ImmutableArray<byte>.Empty, null, null, null, null, null,
                WorkflowMappingState.NoTemplate, null, null, null);
            if (!sessions.TryAdd(id, new(snapshot))) throw new StudioFault(503, "SESSION_ADMISSION_FAILED");
            return snapshot;
        }
    }
    public UniversalDocSession Read(UniversalDocActor actor, string id)
    {
        var entry = Find(id);
        lock (entry.Gate) return Authorized(entry, actor);
    }
    public UniversalDocSession Update(UniversalDocActor actor, string id, long expectedVersion,
        Func<UniversalDocSession, UniversalDocSession> transition)
    {
        var entry = Find(id);
        lock (entry.Gate)
        {
            var before = Authorized(entry, actor);
            if (before.Version != expectedVersion) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
            var after = transition(before);
            // Authorization/expiry is checked again after expensive analysis before linearization.
            Authorized(entry, actor);
            if (after.Id != before.Id || after.OwnerId != before.OwnerId || after.TenantId != before.TenantId ||
                after.ExpiresAt != before.ExpiresAt || after.Template.Length > MaximumTemplateBytes)
                throw new StudioFault(400, "INVALID_SESSION_TRANSITION");
            entry.Snapshot = after with { Version = checked(before.Version + 1) };
            return entry.Snapshot;
        }
    }
    public void Remove(UniversalDocActor actor, string id, long expectedVersion)
    {
        var entry = Find(id);
        lock (entry.Gate)
        {
            var snapshot = Authorized(entry, actor);
            if (snapshot.Version != expectedVersion) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
            entry.Snapshot = null; sessions.TryRemove(id, out _);
        }
    }
    private Entry Find(string id) => id is { Length: 64 } && sessions.TryGetValue(id, out var entry)
        ? entry : throw new StudioFault(404, "SESSION_NOT_FOUND");
    private void RequireIdentity(UniversalDocActor actor)
    {
        if (actor.IdentityExpiresAt <= time.GetUtcNow()) throw new StudioFault(401, "IDENTITY_EXPIRED");
    }
    private UniversalDocSession Authorized(Entry entry, UniversalDocActor actor)
    {
        RequireIdentity(actor);
        var snapshot = entry.Snapshot;
        if (snapshot is null || snapshot.OwnerId != actor.UserId || snapshot.TenantId != actor.TenantId)
            throw new StudioFault(404, "SESSION_NOT_FOUND");
        if (snapshot.ExpiresAt <= time.GetUtcNow())
        {
            entry.Snapshot = null; sessions.TryRemove(snapshot.Id, out _);
            throw new StudioFault(410, "SESSION_EXPIRED");
        }
        return snapshot;
    }
    public void Cleanup()
    {
        foreach (var (id, entry) in sessions)
            lock (entry.Gate)
                if (entry.Snapshot is null || entry.Snapshot.ExpiresAt <= time.GetUtcNow())
                { entry.Snapshot = null; sessions.TryRemove(id, out _); }
    }
    public void Dispose()
    {
        lock (admission)
        {
            disposed = true;
            foreach (var entry in sessions.Values) lock (entry.Gate) entry.Snapshot = null;
            sessions.Clear();
        }
    }
}
