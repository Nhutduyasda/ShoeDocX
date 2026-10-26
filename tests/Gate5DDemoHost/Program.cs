using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Tests.UniversalDocEngine;
var connection=Environment.GetEnvironmentVariable("GATE5C_DATABASE")??throw new InvalidOperationException("Synthetic SQL database required");
if(!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("gate5c_",StringComparison.Ordinal))throw new InvalidOperationException("Only isolated synthetic databases allowed");
var factory=new Gate5CSqlFixture.Factory(connection);
if(args.FirstOrDefault()=="--drop-visual-legacy"){
    var legacy=Environment.GetEnvironmentVariable("GATE6A_VISUAL_LEGACY")??throw new InvalidOperationException("Synthetic legacy SQL required");
    if(!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(legacy).InitialCatalog.StartsWith("gate6a_visual_",StringComparison.Ordinal))throw new InvalidOperationException("Only isolated visual SQL allowed");
    using var db=new ShoeExportInvoice.Api.Data.AppDbContext(new DbContextOptionsBuilder<ShoeExportInvoice.Api.Data.AppDbContext>().UseSqlServer(legacy).Options);
    db.Database.EnsureDeleted();return;
}
if(args.FirstOrDefault()=="--migrate"){using var db=factory.CreateDbContext();db.Database.Migrate();return;}
if(args.FirstOrDefault()=="--drop"){using var db=factory.CreateDbContext();db.Database.EnsureDeleted();return;}
if(args.FirstOrDefault()=="--corpus"){
    var directory=args[1];Directory.CreateDirectory(directory);var actor=new Gate5CSqlFixture.Actor();var rows=new List<object>();
    foreach(var scenario in new[]{"A","B","C","D","E","F","G"}){
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,sessions);var s=Gate5DFixtures.Configure(w,scenario,scenario=="G"?5:0);s=Gate5DFixtures.Enter(w,s,scenario);s=w.Review(s.Id,s.Version);s=w.Approve(s.Id,s.Version);File.WriteAllBytes(Path.Combine(directory,scenario+".xlsx"),w.Export(s.Id,s.Version).Workbook);
        rows.Add(new{scenario,sheets=Gate5DFixtures.Names(scenario),rows=2,status="SUPPORTED_EXPORTED"});
    }
    foreach(var kind in new[]{"cross-sheet","far","reference","merge"}) {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(actor,sessions);var s=w.Create("example-template");
        try {
            if(kind is "reference" or "merge") {s=w.Upload(s.Id,s.Version,Gate5DFixtures.Workbook(),"mau.xlsx");w.UploadLearning(s.Id,s.Version,Gate5DFixtures.Workbook(rows:2,filled:true,unsupported:kind),"vidu.xlsx",true);}
            else w.Upload(s.Id,s.Version,Gate5DFixtures.Workbook(unsupported:kind),"mau.xlsx");
            throw new Exception("Unsupported corpus accepted");
        } catch(StudioFault fault) {
            if(w.Read(s.Id).Version!=s.Version)throw new Exception("Unsupported corpus mutated state");
            try{w.Export(s.Id,s.Version);throw new Exception("Unsupported corpus exported");}catch(StudioFault){}
            rows.Add(new{scenario="H-"+kind,status="REJECTED",code=fault.Code,outputBytesReleased=false});
        }
    }
    File.WriteAllText(Path.Combine(directory,"corpus-manifest.json"),JsonSerializer.Serialize(rows));return;
}
var port=int.Parse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT")??"5291");
await using var host=await StudioHttpHost.Start(port:port,beforeBuild:b=>b.Configuration.AddConfiguration(Gate5CSqlFixture.Configuration(connection)),configure:app=>{
    app.MapGet("/api/__test/gate5d/{scenario}",(string scenario,int? rows,bool? filled,string? unsupported)=>Results.File(Gate5DFixtures.Workbook(scenario,rows??1,filled??false,unsupported),StudioExport.ContentType)).RequireAuthorization();
});
await host.App.WaitForShutdownAsync();
