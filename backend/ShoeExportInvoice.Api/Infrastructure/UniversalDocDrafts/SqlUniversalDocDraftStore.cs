using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
namespace ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

public sealed class SqlUniversalDocDraftStore(IDbContextFactory<UniversalDocDraftDb> factory, DraftCipher cipher) : IUniversalDocDraftStore
{
    private T Transaction<T>(UniversalDocActor? actor, Func<UniversalDocDraftDb, DateTimeOffset, T> action)
    {
        try {
            using var db = factory.CreateDbContext(); using var transaction = db.Database.BeginTransaction();
            db.Database.ExecuteSqlRaw("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'ude.draft-admission.v1', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @r<0 THROW 51000,'Draft lock unavailable',1;");
            DateTimeOffset Now() => db.Database.SqlQueryRaw<DateTimeOffset>("SELECT SYSDATETIMEOFFSET() AS [Value]").Single();
            // SQL string equality pads trailing ASCII spaces even under BIN2. Reject ambiguous trusted identifiers.
            void Identity(DateTimeOffset now) { if (actor is not null && (actor.IdentityExpiresAt <= now || string.IsNullOrWhiteSpace(actor.UserId) || string.IsNullOrWhiteSpace(actor.TenantId) || actor.UserId.Length > 256 || actor.TenantId.Length > 256 || actor.UserId.EndsWith(' ') || actor.TenantId.EndsWith(' '))) throw new StudioFault(401, "IDENTITY_EXPIRED"); }
            var now = Now(); Identity(now); var result = action(db, now); db.SaveChanges(); Identity(Now()); transaction.Commit(); return result;
        } catch (DbUpdateConcurrencyException) { throw new StudioFault(409, "DRAFT_REVISION_CONFLICT"); }
        catch (Exception e) when (e is SqlException or DbUpdateException or TimeoutException) { throw new StudioFault(503, "DRAFT_STORAGE_UNAVAILABLE"); }
    }
    private static void Purge(UniversalDocDraftDb db, DateTimeOffset now)
    {
        db.Drafts.RemoveRange(db.Drafts.Where(x => x.ExpiresAt <= now).OrderBy(x => x.ExpiresAt).Take(16));
        db.Leases.RemoveRange(db.Leases.Where(x => x.ExpiresAt <= now).OrderBy(x => x.ExpiresAt).Take(16)); db.SaveChanges();
    }
    private static DraftRecord Find(UniversalDocDraftDb db, UniversalDocActor actor, string id, long revision, DateTimeOffset now)
    {
        if (id.Length != 64 || !id.All(Uri.IsHexDigit)) throw new StudioFault(404, "DRAFT_NOT_FOUND");
        var row = db.Drafts.SingleOrDefault(x => x.Id == id && x.OwnerId == actor.UserId && x.TenantId == actor.TenantId) ?? throw new StudioFault(404, "DRAFT_NOT_FOUND");
        if (row.ExpiresAt <= now) throw new StudioFault(410, "DRAFT_EXPIRED");
        if (row.Revision != revision) throw new StudioFault(409, "DRAFT_REVISION_CONFLICT"); return row;
    }
    public DraftInfo[] List(UniversalDocActor actor) => Transaction(actor, (db, now) => db.Drafts.AsNoTracking().Where(x => x.OwnerId == actor.UserId && x.TenantId == actor.TenantId && x.ExpiresAt > now).OrderByDescending(x => x.UpdatedAt).Take(2).Select(x => new DraftInfo(x.Id, x.Workflow, x.Revision, x.CreatedAt, x.UpdatedAt, x.ExpiresAt)).ToArray());
    public DraftInfo Save(UniversalDocActor actor, string? id, long revision, byte[] payload) => Transaction(actor, (db, now) => {
        if (payload.Length is <= 0 or > DraftSnapshot.MaximumPayload || (id is null ? revision != 0 : revision < 1)) throw new StudioFault(400, "DRAFT_REQUEST_INVALID");
        DraftRecord row;
        if (id is null) {
            Purge(db, now);
            if (db.Drafts.Count() >= 16 || db.Drafts.Count(x => x.TenantId == actor.TenantId) >= 6 || db.Drafts.Count(x => x.TenantId == actor.TenantId && x.OwnerId == actor.UserId) >= 2) throw new StudioFault(429, "DRAFT_CAPACITY_REACHED");
            row = new() { Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), OwnerId = actor.UserId, TenantId = actor.TenantId, CreatedAt = now, ExpiresAt = now.AddDays(7) }; db.Drafts.Add(row);
        } else row = Find(db, actor, id, revision, now);
        row.Revision = checked(revision + 1); row.UpdatedAt = now; row.KeyId = cipher.ActiveKeyId; row.Ciphertext = cipher.Encrypt(row, payload); return row.Info();
    });
    public StoredDraft Read(UniversalDocActor actor, string id, long revision) => Transaction(actor, (db, now) => { var row = Find(db, actor, id, revision, now); return new StoredDraft(row.Info(), cipher.Decrypt(row)); });
    public void Delete(UniversalDocActor actor, string id, long revision) => Transaction(actor, (db, now) => { db.Drafts.Remove(Find(db, actor, id, revision, now)); return 0; });
    public string AcquireResume(UniversalDocActor actor, string id, long revision) => Transaction(actor, (db, now) => {
        Find(db, actor, id, revision, now); Purge(db, now);
        if (db.Leases.Count() >= 16 || db.Leases.Count(x => x.TenantId == actor.TenantId) >= 6 || db.Leases.Count(x => x.TenantId == actor.TenantId && x.OwnerId == actor.UserId) >= 2) throw new StudioFault(429, "SESSION_CAPACITY_REACHED");
        var lease = new ResumeLease { Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), OwnerId = actor.UserId, TenantId = actor.TenantId, ExpiresAt = now + InMemoryUniversalDocSessionStore.Lifetime }; db.Leases.Add(lease); return lease.Id;
    });
    public void CompleteResume(UniversalDocActor actor, string lease, string id, long revision, DateTimeOffset sessionExpiresAt) => Transaction(actor, (db, now) => {
        Find(db, actor, id, revision, now);
        var row = db.Leases.SingleOrDefault(x => x.Id == lease && x.OwnerId == actor.UserId && x.TenantId == actor.TenantId && x.ExpiresAt > now) ?? throw new StudioFault(409, "DRAFT_RESUME_EXPIRED");
        if (sessionExpiresAt <= now || sessionExpiresAt > now + InMemoryUniversalDocSessionStore.Lifetime + TimeSpan.FromSeconds(5)) throw new StudioFault(409, "DRAFT_RESUME_EXPIRED");
        row.ExpiresAt = sessionExpiresAt; return 0;
    });
    public void ReleaseResume(string lease) => Transaction<int>(null, (db, now) => { db.Leases.Where(x => x.Id == lease).ExecuteDelete(); return 0; });
    public void Cleanup() => Transaction<int>(null, (db, now) => { Purge(db, now); return 0; });
}
