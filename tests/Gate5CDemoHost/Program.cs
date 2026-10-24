using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;
using ShoeExportInvoice.Tests.UniversalDocEngine;

var connection = Environment.GetEnvironmentVariable("GATE5C_DATABASE") ?? throw new InvalidOperationException("Synthetic database required");
var factory = new Gate5CSqlFixture.Factory(connection);
if (!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("gate5c_",StringComparison.Ordinal)) throw new InvalidOperationException("Only isolated gate5c databases allowed");
if (args.FirstOrDefault()=="--sql-info") {
    using var db=factory.CreateDbContext();db.Database.OpenConnection();using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')),CONVERT(nvarchar(128),SERVERPROPERTY('Edition')),CONVERT(int,SERVERPROPERTY('EngineEdition'))";using var reader=command.ExecuteReader();reader.Read();Console.WriteLine(JsonSerializer.Serialize(new{version=reader.GetString(0),edition=reader.GetString(1),engineEdition=reader.GetInt32(2),ephemeral=true}));return;
}
if (args.FirstOrDefault()=="--drop") {using var db=factory.CreateDbContext();db.Database.EnsureDeleted();return;}
if (args.FirstOrDefault() == "--migrate") { using var db=factory.CreateDbContext();db.Database.Migrate();return; }
if (args.FirstOrDefault() == "--probe") {
    var actor=new Gate5CSqlFixture.Actor();using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,sessions);
    var store=new SqlUniversalDocDraftStore(factory,new DraftCipher(Gate5CSqlFixture.Configuration(connection)));
    if(args[1]=="uncommitted") {
        using var db=factory.CreateDbContext();using var tx=db.Database.BeginTransaction();
        db.Database.ExecuteSqlInterpolated($"UPDATE ude.Drafts SET Revision=Revision+1 WHERE Id={args[2]}");
        Console.WriteLine("UNCOMMITTED_READY");Console.Out.Flush();await Task.Delay(Timeout.Infinite);return;
    }
    if(args[1]=="save-point") {var s=Gate5CSqlFixture.SavePoint(w,args[2]);Console.WriteLine(JsonSerializer.Serialize(w.SaveDraft(store,s.Id,s.Version,null,0).Draft));return;}
    if(args[1]=="recover-point") {
        var persisted=store.Read(actor.Value,args[2],1);var snapshot=JsonSerializer.Deserialize<DraftSnapshot>(persisted.Payload,DraftSnapshot.Json)!;
        var resumed=w.ResumeDraft(store,args[2],1,TimeProvider.System);var restored=sessions.Read(actor.Value,resumed.Id);
        if(!restored.Template.SequenceEqual(snapshot.Template)||!restored.Example.SequenceEqual(snapshot.Example)||restored.Confirmation is not null||restored.Document?.Approval is not null||restored.Document?.ReviewEvidence is not null)throw new Exception("Recovered save point mismatch");
        if(resumed.Mappings.Length!=snapshot.Mappings.Length||!resumed.ValidationRules.SequenceEqual(snapshot.Rules))throw new Exception("Recovered configuration mismatch");
        try {w.Export(resumed.Id,resumed.Version);throw new Exception("Stale export released");}catch(StudioFault){}
        Console.WriteLine("PROCESS_SAVE_POINT_RECOVERY_PASS");return;
    }
    if(args[1]=="save") {var s=Gate5CSqlFixture.Ready(w);var saved=w.SaveDraft(store,s.Id,s.Version,null,0);Console.WriteLine(JsonSerializer.Serialize(saved.Draft));return;}
    var result=w.ResumeDraft(store,args[2],long.Parse(args[3]),TimeProvider.System);
    if(result.MappingState!="DraftSuggested"||result.ReviewState=="Approved")throw new Exception("Stale authority restored");
    try {w.Export(result.Id,result.Version);throw new Exception("Stale export released");}catch(StudioFault e)when(e.Code=="MAPPING_CONFIRMATION_REQUIRED"){}
    result=w.ConfirmMapping(result.Id,result.Version);result=w.Review(result.Id,result.Version);result=w.Approve(result.Id,result.Version);
    using var wb=new XLWorkbook(new MemoryStream(w.Export(result.Id,result.Version).Workbook));
    if(wb.Worksheet("Data2").Cell("A4").GetString()!="0001"||wb.Worksheet("Data2").Cell("E6").FormulaA1!="SUM(E4:E5)"||wb.Worksheet("Reference").Cell("A1").GetString()!="Dữ liệu tham chiếu 00123")throw new Exception("Recovered output mismatch");
    Console.WriteLine("PROCESS_RECOVERY_EXPORT_REOPEN_PASS");return;
}
var port=int.Parse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT")??"5289");
await using var host=await StudioHttpHost.Start(port:port,beforeBuild:builder=>builder.Configuration.AddConfiguration(Gate5CSqlFixture.Configuration(connection)),configure:app=>{
    app.MapGet("/api/__test/gate5c-template",()=>Results.File(Gate5BFixtures.Workbook(),StudioExport.ContentType)).RequireAuthorization();
    app.MapGet("/api/__test/gate5c-example",()=>Results.File(Gate5BFixtures.Workbook(2,2,true),StudioExport.ContentType)).RequireAuthorization();
});
await host.App.WaitForShutdownAsync();
