using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Tests.UniversalDocEngine;

var connection = Environment.GetEnvironmentVariable("GATE5C_DATABASE") ?? throw new InvalidOperationException("Synthetic database required");
var factory = new Gate5CSqlFixture.Factory(connection);
if (args.FirstOrDefault() == "--migrate") { using var db=factory.CreateDbContext();db.Database.Migrate();return; }
if (args.FirstOrDefault() == "--probe") {
    var actor=new Gate5CSqlFixture.Actor();using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,sessions);
    var store=new SqlUniversalDocDraftStore(factory,new DraftCipher(Gate5CSqlFixture.Configuration(connection)));
    if(args[1]=="uncommitted") {
        using var db=factory.CreateDbContext();using var tx=db.Database.BeginTransaction();
        db.Database.ExecuteSqlInterpolated($"UPDATE ude.Drafts SET Ciphertext=0x0102 WHERE Id={args[2]}");
        Console.WriteLine("UNCOMMITTED_READY");Console.Out.Flush();await Task.Delay(Timeout.Infinite);return;
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
