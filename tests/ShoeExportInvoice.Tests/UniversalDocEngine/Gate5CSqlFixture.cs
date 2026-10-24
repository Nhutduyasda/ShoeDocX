using System.Collections.Immutable;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

// Only synthetic SQL connections explicitly supplied by isolated CI; no production configuration is read.
public sealed class Gate5CSqlFixture : IAsyncDisposable
{
    public const string Key = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    public string Connection { get; }
    public sealed class Factory(string connection) : IDbContextFactory<UniversalDocDraftDb>
    {
        public UniversalDocDraftDb CreateDbContext() => new(new DbContextOptionsBuilder<UniversalDocDraftDb>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__DraftMigrations", "ude").CommandTimeout(15)).Options);
    }
    public Factory DbFactory => new(Connection);
    public DraftCipher Cipher => new(Configuration(Connection));
    public SqlUniversalDocDraftStore Store => new(DbFactory, Cipher);
    public Gate5CSqlFixture()
    {
        var raw = Environment.GetEnvironmentVariable("GATE5C_SQL") ?? throw new InvalidOperationException("Real ephemeral SQL Server is mandatory: GATE5C_SQL missing");
        var builder = new SqlConnectionStringBuilder(raw) { InitialCatalog = "gate5c_" + Guid.NewGuid().ToString("N"), ConnectTimeout = 15 }; Connection = builder.ConnectionString;
    }
    public static IConfiguration Configuration(string connection, string? key = Key) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["UniversalDocDrafts:Enabled"]="true", ["UniversalDocDrafts:ConnectionString"]=connection,
        ["UniversalDocDrafts:ActiveKeyId"]="synthetic-v1", ["UniversalDocDrafts:Keys:synthetic-v1"]=key }).Build();
    public async Task Initialize() { await using var db = DbFactory.CreateDbContext(); await db.Database.MigrateAsync(); }
    public async ValueTask DisposeAsync() { await using var db = DbFactory.CreateDbContext(); await db.Database.EnsureDeletedAsync(); }
    public sealed class Actor : IUniversalDocCurrentActor
    {
        public UniversalDocActor Value = new("owner", "tenant", "Admin", DateTimeOffset.UtcNow.AddHours(1));
        public UniversalDocActor Resolve() => Value;
    }
    public static StudioSessionView Ready(UniversalDocWorkflowOrchestrator w, int sheets = 2, bool example = true, bool approved = true)
    {
        var s = w.Create("example-template"); s = w.Upload(s.Id,s.Version,Gate5BFixtures.Workbook(sheets),"template.xlsx");
        if(example) s = w.UploadLearning(s.Id,s.Version,Gate5BFixtures.Workbook(sheets,2,true),"example.xlsx",true);
        var fields = s.SchemaDraft!.Fields.Select(f => new SchemaFieldCorrection(f.Key,f.Label,f.Scope,f.Label=="Item ID"?"Text":"Decimal",false,f.Candidates[0].Worksheet!="Data1",0)).ToImmutableArray();
        s = w.CorrectSchema(s.Id,s.Version,fields);
        s = w.ChangeMultisheetMapping(s.Id,s.Version,Enumerable.Range(1,sheets).Select(i => new StudioSheetMapping("Data"+i,4,s.Mapping!.Headers,s.Mapping.Columns)).ToImmutableArray());
        string KeyOf(string label) => s.SchemaDraft!.Fields.Single(f=>!f.Omit&&f.Label==label).Key;
        s = w.ChangeValidationRules(s.Id,s.Version,[new("amount","Arithmetic",KeyOf("Quantity"),"Multiply",KeyOf("Price"),KeyOf("Amount"),0,true,"BlockingError"),new("total","CrossFoot",KeyOf("Amount"),null,null,KeyOf("Printed total"),0,true,"BlockingError")]);
        s = w.ConfirmMapping(s.Id,s.Version);
        var row=ImmutableDictionary<string,string?>.Empty.Add(KeyOf("Item ID"),"0001").Add(KeyOf("Quantity"),"2.5").Add(KeyOf("Price"),"3").Add(KeyOf("Amount"),"7.5");
        s = w.SetManualSample(s.Id,s.Version,new(ImmutableDictionary<string,string?>.Empty.Add(KeyOf("Printed total"),"15"),[row,row.SetItem(KeyOf("Item ID"),"0002")]));
        if(approved) {s=w.Review(s.Id,s.Version);s=w.Approve(s.Id,s.Version);} return s;
    }
    public static StudioSessionView SavePoint(UniversalDocWorkflowOrchestrator w,string point) {
        if(point is "F" or "G") return Ready(w,approved:point=="G");
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
        return s;
    }

}
