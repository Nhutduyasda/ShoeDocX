using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public sealed class Gate5CTests : IAsyncLifetime
{
    private readonly Gate5CSqlFixture sql=new();
    public Task InitializeAsync()=>sql.Initialize();
    public Task DisposeAsync()=>sql.DisposeAsync().AsTask();
    private static Gate5CSqlFixture.Actor Actor()=>new();
    private DraftInfo SaveReady(int sheets=2) {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(Actor(),sessions);var s=Gate5CSqlFixture.Ready(w,sheets);return w.SaveDraft(sql.Store,s.Id,s.Version,null,0).Draft;
    }
    [Theory][InlineData(2)][InlineData(3)]
    public void RealSqlRecoveryRestoresValuesTypesRulesAndSheetsWithoutApproval(int sheets) {
        var draft=SaveReady(sheets);using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var actor=Actor();var w=new UniversalDocWorkflowOrchestrator(actor,sessions);
        var s=w.ResumeDraft(sql.Store,draft.Id,draft.Revision,TimeProvider.System);var restored=sessions.Read(actor.Resolve(),s.Id);
        Assert.Equal("DraftSuggested",s.MappingState);Assert.Null(restored.Confirmation);Assert.Null(restored.Document!.Approval);Assert.Null(restored.Document.ReviewEvidence);
        Assert.Equal(sheets,s.Mappings.Length);Assert.Equal(2,s.ValidationRules.Length);Assert.Equal("0001",s.Fields.First(f=>f.Row==0&&f.Type=="Text").Value);
        Assert.Contains(s.Fields,f=>f.Type=="Decimal"&&f.Value=="2.5");
        Assert.Equal(Convert.ToHexString(SHA256.HashData(restored.Template.ToArray())),restored.TemplateSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(restored.Example.ToArray())),restored.SchemaDraft!.ExampleSha256);
        Assert.Equal("MAPPING_CONFIRMATION_REQUIRED",Assert.Throws<StudioFault>(()=>w.Export(s.Id,s.Version)).Code);
        s=w.ConfirmMapping(s.Id,s.Version);Assert.Equal("CURRENT_APPROVAL_REQUIRED",Assert.Throws<StudioFault>(()=>w.Export(s.Id,s.Version)).Code);
        s=w.Review(s.Id,s.Version);s=w.Approve(s.Id,s.Version);using var wb=new XLWorkbook(new MemoryStream(w.Export(s.Id,s.Version).Workbook));
        foreach(var n in Enumerable.Range(1,sheets)){var ws=wb.Worksheet("Data"+n);Assert.Equal("0002",ws.Cell("A5").GetString());Assert.Equal("B5*C5",ws.Cell("E5").FormulaA1);Assert.Equal("SUM(E4:E5)",ws.Cell("E6").FormulaA1);Assert.Equal("0.00",ws.Cell("B5").Style.NumberFormat.Format);Assert.Contains(ws.MergedRanges,r=>r.RangeAddress.ToString()=="A7:C7");}
        Assert.Equal("Dữ liệu tham chiếu 00123",wb.Worksheet("Reference").Cell("A1").GetString());
    }
    [Theory][InlineData("owner")][InlineData("tenant")]
    public void RealSqlOwnerTenantDeniedBeforeDecrypt(string kind) {
        var d=SaveReady();var a=Actor().Value; a=kind=="owner"?a with{UserId="other"}:a with{TenantId="other"};
        using(var db=sql.DbFactory.CreateDbContext()){var row=db.Drafts.Single();var bad=row.Ciphertext.ToArray();bad[0]^=1;row.Ciphertext=bad;db.SaveChanges();}
        Assert.Empty(sql.Store.List(a));Assert.Equal(404,Assert.Throws<StudioFault>(()=>sql.Store.Read(a,d.Id,d.Revision)).Status);
        Assert.Equal(404,Assert.Throws<StudioFault>(()=>sql.Store.Delete(a,d.Id,d.Revision)).Status);Assert.Equal(404,Assert.Throws<StudioFault>(()=>sql.Store.Save(a,d.Id,d.Revision,[1])).Status);
    }
    [Theory][InlineData("tamper")][InlineData("swap")][InlineData("format")][InlineData("revision")][InlineData("owner")][InlineData("key")]
    public void RealSqlAuthenticatedCipherBindsImmutableIdentity(string kind) {
        var d=SaveReady();if(kind=="swap")SaveReady();
        using(var db=sql.DbFactory.CreateDbContext()){var row=db.Drafts.Single(x=>x.Id==d.Id);switch(kind){case "tamper":var bad=row.Ciphertext.ToArray();bad[13]^=1;row.Ciphertext=bad;break;case "swap":row.Ciphertext=db.Drafts.Single(x=>x.Id!=d.Id).Ciphertext;break;case "format":row.Format=99;break;case "revision":row.Revision++;break;case "owner":row.OwnerId="other";break;case "key":row.KeyId="missing";break;}db.SaveChanges();}
        var actor=Actor().Value;if(kind=="owner")actor=actor with{UserId="other"};
        Assert.Equal("DRAFT_PAYLOAD_INVALID",Assert.Throws<StudioFault>(()=>sql.Store.Read(actor,d.Id,kind=="revision"?2:1)).Code);
    }
    [Theory][InlineData("missing")][InlineData("invalid")][InlineData("wrong")]
    public void MissingInvalidOrWrongKeyFailsClosed(string kind) {
        var d=SaveReady();if(kind!="wrong"){Assert.Equal(503,Assert.Throws<StudioFault>(()=>new DraftCipher(Gate5CSqlFixture.Configuration(sql.Connection,kind=="missing"?null:"bad"))).Status);return;}
        var wrong=new DraftCipher(Gate5CSqlFixture.Configuration(sql.Connection,Convert.ToBase64String(new byte[32])));var store=new SqlUniversalDocDraftStore(sql.DbFactory,wrong);
        Assert.Equal("DRAFT_PAYLOAD_INVALID",Assert.Throws<StudioFault>(()=>store.Read(Actor().Value,d.Id,1)).Code);
    }
    [Theory][InlineData("json")][InlineData("version")][InlineData("hash")][InlineData("workbook")][InlineData("rule")][InlineData("fingerprint")][InlineData("unknown")]
    public void CorruptOrUnsupportedSnapshotCannotPublishRuntimeSession(string kind) {
        var d=SaveReady();var stored=sql.Store.Read(Actor().Value,d.Id,1);var snap=JsonSerializer.Deserialize<DraftSnapshot>(stored.Payload,DraftSnapshot.Json)!;
        byte[] bad=kind switch{"json"=>[1,2,3],"version"=>JsonSerializer.SerializeToUtf8Bytes(snap with{Format=2},DraftSnapshot.Json),"hash"=>JsonSerializer.SerializeToUtf8Bytes(snap with{TemplateSha256="stale"},DraftSnapshot.Json),"workbook"=>JsonSerializer.SerializeToUtf8Bytes(snap with{Template=[1],TemplateSha256=Convert.ToHexString(SHA256.HashData(new byte[]{1}))},DraftSnapshot.Json),"fingerprint"=>JsonSerializer.SerializeToUtf8Bytes(snap with{ConfigurationSha256="stale"},DraftSnapshot.Json),"rule"=>JsonSerializer.SerializeToUtf8Bytes(snap with{Rules=snap.Rules.SetItem(0,snap.Rules[0] with{FieldA="missing"})},DraftSnapshot.Json),_=>System.Text.Encoding.UTF8.GetBytes("{\"Approved\":true}")};
        var newer=sql.Store.Save(Actor().Value,d.Id,1,bad);using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(Actor(),sessions);
        Assert.Throws<StudioFault>(()=>w.ResumeDraft(sql.Store,d.Id,newer.Revision,TimeProvider.System));
        // Failed reconstruction releases both runtime and durable resume admission.
        Assert.NotNull(w.Create("example-template"));Assert.NotNull(w.Create("example-template"));using var db=sql.DbFactory.CreateDbContext();Assert.Empty(db.Leases);
    }
    [Fact]public void NoncesAreUniqueAndDatabaseContainsOnlyAuthenticatedCiphertext() {
        var d=SaveReady();byte[] first;using(var db=sql.DbFactory.CreateDbContext())first=db.Drafts.Single().Ciphertext;
        var plain=sql.Store.Read(Actor().Value,d.Id,1).Payload;var next=sql.Store.Save(Actor().Value,d.Id,1,plain);
        using var db2=sql.DbFactory.CreateDbContext();var second=db2.Drafts.Single().Ciphertext;Assert.False(first.AsSpan(0,12).SequenceEqual(second.AsSpan(0,12)));Assert.False(second.AsSpan().IndexOf("Printed total"u8)>=0);Assert.Equal(2,next.Revision);Assert.Single(db2.Drafts);
    }
    [Fact]public async Task IndependentRepositoriesHaveOneRevisionWinnerAndSafeRetry() {
        var d=SaveReady();var payload=sql.Store.Read(Actor().Value,d.Id,1).Payload;
        var outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{try{return sql.Store.Save(Actor().Value,d.Id,1,payload).Revision;}catch(StudioFault e){Assert.Equal(409,e.Status);return -1;}})));
        Assert.Equal(new long[]{-1,2},outcomes.Order());Assert.Equal(3,sql.Store.Save(Actor().Value,d.Id,2,payload).Revision);
    }
    [Fact]public async Task ConcurrentSaveDeleteCannotResurrectDeletedDraft() {
        var d=SaveReady();var payload=sql.Store.Read(Actor().Value,d.Id,1).Payload;
        await Task.WhenAll(Task.Run(()=>{try{sql.Store.Save(Actor().Value,d.Id,1,payload);}catch(StudioFault e){Assert.Contains(e.Status,new[]{404,409});}}),Task.Run(()=>{try{sql.Store.Delete(Actor().Value,d.Id,1);}catch(StudioFault e){Assert.Equal(409,e.Status);}}));
        var rows=sql.Store.List(Actor().Value);if(rows.Length==1)sql.Store.Delete(Actor().Value,d.Id,rows[0].Revision);
        Assert.Equal(404,Assert.Throws<StudioFault>(()=>sql.Store.Save(Actor().Value,d.Id,1,payload)).Status);
    }
    [Fact]public async Task CrossProcessResumeAdmissionNeverExceedsOwnerQuota() {
        var d=SaveReady();var result=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>{try{sql.Store.AcquireResume(Actor().Value,d.Id,1);return 200;}catch(StudioFault e){return e.Status;}})));
        Assert.Equal(new[]{200,200,429,429},result.Order());using var db=sql.DbFactory.CreateDbContext();Assert.Equal(2,db.Leases.Count());
    }
    [Theory][InlineData("owner")][InlineData("tenant")][InlineData("global")]
    public void DatabaseAdmissionEnforcesAllDraftQuotas(string level) {
        int limit=level=="owner"?2:level=="tenant"?6:16;
        UniversalDocActor Person(int i)=>Actor().Value with{UserId=level=="owner"?"owner":"owner"+i,TenantId=level=="global"?"tenant"+i:"tenant"};
        for(int i=0;i<limit;i++)sql.Store.Save(Person(i),null,0,[1]);Assert.Equal(429,Assert.Throws<StudioFault>(()=>sql.Store.Save(Person(limit),null,0,[1])).Status);
    }
    [Fact]public void ExpiryIsImmediateAndCleanupActuallyDeletesRowsWithoutRenewal() {
        var d=SaveReady();var payload=sql.Store.Read(Actor().Value,d.Id,1).Payload;var updated=sql.Store.Save(Actor().Value,d.Id,1,payload);Assert.Equal(d.ExpiresAt,updated.ExpiresAt);
        using(var db=sql.DbFactory.CreateDbContext()){db.Drafts.Single().ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);db.SaveChanges();}
        Assert.Empty(sql.Store.List(Actor().Value));Assert.Equal(410,Assert.Throws<StudioFault>(()=>sql.Store.Read(Actor().Value,d.Id,2)).Status);
        sql.Store.Cleanup();using var check=sql.DbFactory.CreateDbContext();Assert.Empty(check.Drafts);
    }
    [Fact]public void OversizeAndUnavailableDatabaseNeverReportDurableSuccess() {
        Assert.Throws<StudioFault>(()=>sql.Store.Save(Actor().Value,null,0,new byte[DraftSnapshot.MaximumPayload+1]));using(var db=sql.DbFactory.CreateDbContext())Assert.Empty(db.Drafts);
        var unavailable=new SqlUniversalDocDraftStore(new Gate5CSqlFixture.Factory("Server=127.0.0.1,1;User ID=synthetic;Password=synthetic;Encrypt=False;Connect Timeout=1"),sql.Cipher);
        Assert.Equal(503,Assert.Throws<StudioFault>(()=>unavailable.Save(Actor().Value,null,0,[1])).Status);
    }
    [Fact]public void SourceBytesAreReAdmittedAndRemainEncrypted() {
        using var runtime=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(Actor(),runtime);var s=Gate5CSqlFixture.Ready(w);
        s=w.AddSource(s.Id,s.Version,Gate4B1Tests.Pdf(),"source.pdf","application/pdf",TimeProvider.System);var d=w.SaveDraft(sql.Store,s.Id,s.Version,null,0).Draft;
        using var fresh=new InMemoryUniversalDocSessionStore(TimeProvider.System);var resumed=new UniversalDocWorkflowOrchestrator(Actor(),fresh).ResumeDraft(sql.Store,d.Id,1,TimeProvider.System);
        Assert.Single(resumed.Sources);Assert.Equal(Convert.ToHexString(SHA256.HashData(Gate4B1Tests.Pdf())),resumed.Sources[0].Sha256);Assert.Equal("AdmittedNotRecognized",resumed.Sources[0].Status);
    }
    [Fact]public void StaleSessionVersionCannotSaveAndExpiredIdentityCannotRead() {
        using var runtime=new InMemoryUniversalDocSessionStore(TimeProvider.System);var actor=Actor();var w=new UniversalDocWorkflowOrchestrator(actor,runtime);var s=Gate5CSqlFixture.Ready(w);var saved=w.SaveDraft(sql.Store,s.Id,s.Version,null,0);
        Assert.Equal(409,Assert.Throws<StudioFault>(()=>w.SaveDraft(sql.Store,s.Id,s.Version,null,0)).Status);
        Assert.Equal(401,Assert.Throws<StudioFault>(()=>sql.Store.Read(actor.Value with{IdentityExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1)},saved.Draft.Id,1)).Status);
    }
    [Fact]public async Task RealSeparateProcessesRecoverAfterRestartAndKilledTransactionRollsBack() {
        var executable=Environment.GetEnvironmentVariable("GATE5C_PROBE")??throw new Exception("Mandatory process probe missing");
        Process Start(params string[] args){var info=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};info.ArgumentList.Add(executable);foreach(var a in args)info.ArgumentList.Add(a);info.Environment["GATE5C_DATABASE"]=sql.Connection;return Process.Start(info)!;}
        using var save=Start("--probe","save");var saved=await save.StandardOutput.ReadToEndAsync();await save.WaitForExitAsync();Assert.Equal(0,save.ExitCode);var d=JsonSerializer.Deserialize<DraftInfo>(saved.Trim())!;
        using var interrupted=Start("--probe","uncommitted",d.Id);Assert.Equal("UNCOMMITTED_READY",await interrupted.StandardOutput.ReadLineAsync());interrupted.Kill(true);await interrupted.WaitForExitAsync();
        Assert.Equal(1,sql.Store.Read(Actor().Value,d.Id,1).Info.Revision);
        using var resume=Start("--probe","resume",d.Id,"1");var output=await resume.StandardOutput.ReadToEndAsync();await resume.WaitForExitAsync();Assert.Equal(0,resume.ExitCode);Assert.Contains("PROCESS_RECOVERY_EXPORT_REOPEN_PASS",output);
    }
    [Theory][InlineData("anonymous")][InlineData("csrf")][InlineData("invalid-csrf")][InlineData("forged")]
    public async Task ActualHttpRejectsAnonymousCsrfAndForgedAuthority(string kind) {
        await using var host=await StudioHttpHost.Start(beforeBuild:b=>b.Configuration.AddConfiguration(Gate5CSqlFixture.Configuration(sql.Connection)));using var client=host.Client();
        if(kind=="anonymous"){Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/universal-doc/drafts")).StatusCode);return;}
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/login",new{username="admin-a",password="Studio@Test2026!"})).StatusCode);
        if(kind=="invalid-csrf")client.DefaultRequestHeaders.Add("X-Studio-CSRF","invalid");
        if(kind=="forged"){var boot=await client.GetFromJsonAsync<JsonElement>("/api/universal-doc/bootstrap");client.DefaultRequestHeaders.Add("X-Studio-CSRF",boot.GetProperty("csrfToken").GetString());}
        var result=await client.PostAsJsonAsync("/api/universal-doc/sessions/any/save-draft",new{expectedVersion=1,expectedRevision=0,ownerId="other",approved=true});Assert.Equal(HttpStatusCode.BadRequest,result.StatusCode);
    }
    [Fact] public void ResumeLeaseCoversFullNewSessionLifetimeAndRejectsConcurrentDeletion() {
        var d=SaveReady();var lease=sql.Store.AcquireResume(Actor().Value,d.Id,1);var expiry=DateTimeOffset.UtcNow.AddMinutes(20);
        sql.Store.CompleteResume(Actor().Value,lease,d.Id,1,expiry);
        using(var db=sql.DbFactory.CreateDbContext()) Assert.Equal(expiry,db.Leases.Single().ExpiresAt);
        sql.Store.Delete(Actor().Value,d.Id,1);Assert.Equal(404,Assert.Throws<StudioFault>(()=>sql.Store.CompleteResume(Actor().Value,lease,d.Id,1,expiry)).Status);
        sql.Store.ReleaseResume(lease);using var check=sql.DbFactory.CreateDbContext();Assert.Empty(check.Leases);
    }
    [Fact] public async Task AdditiveMigrationCanRollbackAndReapplyOnlyFeatureTables() {
        using var db=sql.DbFactory.CreateDbContext();var migrator=db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("0");Assert.Equal(0,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id=SCHEMA_ID('ude') AND name IN ('Drafts','ResumeLeases')").SingleAsync());
        await db.Database.MigrateAsync();Assert.Empty(sql.Store.List(Actor().Value));Assert.Single(await db.Database.GetAppliedMigrationsAsync());
    }
    private sealed class Clock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    [Fact] public void ExpiredRuntimeCannotSaveButDurableRecoveryHasNoPreviousAuthority() {
        var clock=new Clock();using var old=new InMemoryUniversalDocSessionStore(clock);var actor=Actor();var w=new UniversalDocWorkflowOrchestrator(actor,old);var s=Gate5CSqlFixture.Ready(w);var d=w.SaveDraft(sql.Store,s.Id,s.Version,null,0).Draft;
        clock.Now=clock.Now.AddMinutes(21);Assert.Equal("SESSION_EXPIRED",Assert.Throws<StudioFault>(()=>w.SaveDraft(sql.Store,s.Id,s.Version+1,d.Id,1)).Code);
        using var fresh=new InMemoryUniversalDocSessionStore(TimeProvider.System);var restored=new UniversalDocWorkflowOrchestrator(actor,fresh).ResumeDraft(sql.Store,d.Id,1,TimeProvider.System);Assert.NotEqual(s.Id,restored.Id);Assert.NotEqual("Approved",restored.ReviewState);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void PartialTemplateAndOptionalExampleDraftsRemainSafelyResumable(bool example) {
        using var runtime=new InMemoryUniversalDocSessionStore(TimeProvider.System);var actor=Actor();var w=new UniversalDocWorkflowOrchestrator(actor,runtime);var s=w.Create("example-template");s=w.Upload(s.Id,s.Version,Gate5BFixtures.Workbook(),"template.xlsx");
        if(example)s=w.UploadLearning(s.Id,s.Version,Gate5BFixtures.Workbook(2,2,true),"example.xlsx",true);
        var d=w.SaveDraft(sql.Store,s.Id,s.Version,null,0).Draft;using var fresh=new InMemoryUniversalDocSessionStore(TimeProvider.System);var restored=new UniversalDocWorkflowOrchestrator(actor,fresh).ResumeDraft(sql.Store,d.Id,1,TimeProvider.System);
        Assert.NotNull(restored.SchemaDraft);Assert.Equal("NoDocument",restored.ReviewState);Assert.Equal("DraftSuggested",restored.MappingState);
    }

    [Theory][InlineData("A")][InlineData("B")][InlineData("C")][InlineData("D")][InlineData("E")][InlineData("F")][InlineData("G")]
    public void EverySupportedSavePointRecoversWithoutApproval(string point) {
        var actor=Actor();using var original=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,original);
        var s=w.Create("example-template");s=w.Upload(s.Id,s.Version,Gate5BFixtures.Workbook(),"template.xlsx");
        if(point!="A")s=w.UploadLearning(s.Id,s.Version,Gate5BFixtures.Workbook(2,2,true),"example.xlsx",true);
        if(point is "C" or "D" or "E") {
            // C deliberately retains distinct business fields on both sheets. Common-row correspondence is not yet confirmed.
            var corrections=s.SchemaDraft!.Fields.Select(f=>new SchemaFieldCorrection(f.Key,f.Label,f.Scope,f.Label=="Item ID"?"Text":"Decimal",false,point!="C"&&f.Candidates[0].Worksheet!="Data1",0)).ToImmutableArray();
            s=w.CorrectSchema(s.Id,s.Version,corrections);
            if(point is "D" or "E")s=w.ChangeMultisheetMapping(s.Id,s.Version,Enumerable.Range(1,2).Select(i=>new StudioSheetMapping("Data"+i,4,s.Mapping!.Headers,s.Mapping.Columns)).ToImmutableArray());
            if(point=="E") {
                string Key(string label)=>s.SchemaDraft!.Fields.Single(f=>!f.Omit&&f.Label==label).Key;
                s=w.ChangeValidationRules(s.Id,s.Version,[new("amount","Arithmetic",Key("Quantity"),"Multiply",Key("Price"),Key("Amount"),0,true,"BlockingError")]);
            }
        }
        if(point is "F" or "G") {w.Remove(s.Id,s.Version);s=Gate5CSqlFixture.Ready(w,approved:point=="G");}
        var before=original.Read(actor.Value,s.Id);var saved=w.SaveDraft(sql.Store,s.Id,s.Version,null,0);
        original.Dispose();using var restarted=new InMemoryUniversalDocSessionStore(TimeProvider.System);var freshActor=Actor();var recovery=new UniversalDocWorkflowOrchestrator(freshActor,restarted);
        var resumed=recovery.ResumeDraft(sql.Store,saved.Draft.Id,1,TimeProvider.System);var after=restarted.Read(freshActor.Value,resumed.Id);
        Assert.NotEqual(s.Id,resumed.Id);Assert.Equal(before.Template.ToArray(),after.Template.ToArray());Assert.Equal(before.Example.ToArray(),after.Example.ToArray());
        Assert.Equal(JsonSerializer.Serialize(saved.Session.Mappings),JsonSerializer.Serialize(resumed.Mappings));Assert.Equal(saved.Session.ValidationRules.ToArray(),resumed.ValidationRules.ToArray());Assert.Equal(saved.Session.Fields.Select(f=>(f.Key,f.Row,f.Value)),resumed.Fields.Select(f=>(f.Key,f.Row,f.Value)));
        Assert.Null(after.Confirmation);Assert.Null(after.Document?.Approval);Assert.Null(after.Document?.ReviewEvidence);
        Assert.Throws<StudioFault>(()=>recovery.Export(resumed.Id,resumed.Version));
        if(point=="C")Assert.Equal("EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED",Assert.Throws<StudioFault>(()=>recovery.ConfirmMapping(resumed.Id,resumed.Version)).Code);
    }
    [Fact] public void MissingAndObservedEmptySampleValuesRemainDistinctAfterRecovery() {
        var actor=Actor();using var original=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,original);var s=Gate5CSqlFixture.Ready(w,approved:false);
        string Key(string label)=>s.SchemaDraft!.Fields.Single(f=>!f.Omit&&f.Label==label).Key;
        s=w.SetManualSample(s.Id,s.Version,new(ImmutableDictionary<string,string?>.Empty,[ImmutableDictionary<string,string?>.Empty.Add(Key("Item ID"),"0001").Add(Key("Quantity"),"")]));
        var saved=w.SaveDraft(sql.Store,s.Id,s.Version,null,0);using var restarted=new InMemoryUniversalDocSessionStore(TimeProvider.System);var fresh=new UniversalDocWorkflowOrchestrator(actor,restarted);var resumed=fresh.ResumeDraft(sql.Store,saved.Draft.Id,1,TimeProvider.System);
        var doc=restarted.Read(actor.Value,resumed.Id).Document!;Assert.Empty(doc.Header);Assert.False(doc.LineItems[0].ContainsKey(Key("Price")));Assert.True(doc.LineItems[0].ContainsKey(Key("Quantity")));Assert.Null(doc.LineItems[0][Key("Quantity")].Number);Assert.Null(doc.Approval);
        Assert.Throws<StudioFault>(()=>fresh.Export(resumed.Id,resumed.Version));
    }

}
