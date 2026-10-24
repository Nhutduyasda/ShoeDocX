using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public sealed class Gate5DTests
{
    private sealed class Actor:IUniversalDocCurrentActor { public UniversalDocActor Value=new("office-owner","office-tenant","Admin",DateTimeOffset.UtcNow.AddHours(1));public UniversalDocActor Resolve()=>Value; }
    [Theory][InlineData("A",0)][InlineData("B",0)][InlineData("C",0)][InlineData("D",0)][InlineData("E",0)][InlineData("F",0)][InlineData("G",1)][InlineData("G",2)][InlineData("G",3)][InlineData("G",4)][InlineData("G",5)]
    public void SyntheticOfficeCorpusExportsReopensAndPreservesIntegrity(string scenario,int examples) {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(new Actor(),sessions);var s=Gate5DFixtures.Configure(w,scenario,examples);s=Gate5DFixtures.Enter(w,s,scenario);
        var preview=w.PreviewOffice(s.Id,s.Version);Assert.True(preview.Readiness.DataValid);Assert.False(preview.Readiness.CanExport);Assert.True(preview.Readiness.CanReviewNow);
        Assert.Equal("CURRENT_APPROVAL_REQUIRED",Assert.Throws<StudioFault>(()=>w.Export(s.Id,s.Version)).Code);
        Assert.Contains(preview.Fields,f=>f.Type=="Text"&&f.Value=="00121");Assert.All(preview.Fields,f=>Assert.InRange(f.Destinations.Length,1,3));
        s=w.Review(s.Id,s.Version);Assert.True(w.PreviewOffice(s.Id,s.Version).Readiness.CanApproveNow);s=w.Approve(s.Id,s.Version);Assert.True(w.PreviewOffice(s.Id,s.Version).Readiness.CanExport);
        using var wb=new XLWorkbook(new MemoryStream(w.Export(s.Id,s.Version).Workbook));
        Assert.Equal(Gate5DFixtures.Names(scenario).Append("ThamChieu"),wb.Worksheets.Select(x=>x.Name));
        foreach(var name in Gate5DFixtures.Names(scenario)) {var ws=wb.Worksheet(name);Assert.Equal("CT-00123",ws.Cell("B1").GetString());Assert.Equal(new DateTime(2026,10,24),ws.Cell("B2").GetDateTime());Assert.True(ws.Cell("B3").GetBoolean());
            for(var row=6;row<=7;row++){Assert.Equal("0012"+(row-5),ws.Cell(row,1).GetString());Assert.Equal(2.5,ws.Cell(row,2).GetDouble());Assert.Equal(3,ws.Cell(row,3).GetDouble());Assert.Equal(7.5,ws.Cell(row,4).GetDouble());Assert.Equal("cái",ws.Cell(row,5).GetString());Assert.Equal($"B{row}*C{row}",ws.Cell(row,6).FormulaA1);Assert.Equal("0.00",ws.Cell(row,2).Style.NumberFormat.Format);Assert.Equal(XLBorderStyleValues.Thin,ws.Cell(row,2).Style.Border.BottomBorder);}
            Assert.Equal("SUM(F6:F7)",ws.Cell("F8").FormulaA1);Assert.Contains(ws.MergedRanges,r=>r.RangeAddress.ToString()=="A9:D9");}
        Assert.Equal("Tham chiếu giữ nguyên 00123",wb.Worksheet("ThamChieu").Cell("A1").GetString());
    }
    [Theory][InlineData("cross-sheet")][InlineData("far")]
    public void UnsupportedCorpusIsRejectedAtomicallyWithoutOutput(string kind) {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(new Actor(),sessions);var s=w.Create("example-template");
        Assert.Throws<StudioFault>(()=>w.Upload(s.Id,s.Version,Gate5DFixtures.Workbook(unsupported:kind),"mau.xlsx"));Assert.Equal(s.Version,w.Read(s.Id).Version);Assert.Empty(w.Read(s.Id).Sheets);Assert.Throws<StudioFault>(()=>w.Export(s.Id,s.Version));
    }
    [Theory][InlineData("reference")][InlineData("merge")]
    public void ChangedReferenceOrMergedExampleFailsClosed(string kind) {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(new Actor(),sessions);var s=w.Create("example-template");s=w.Upload(s.Id,s.Version,Gate5DFixtures.Workbook(),"mau.xlsx");
        Assert.Throws<StudioFault>(()=>w.UploadLearning(s.Id,s.Version,Gate5DFixtures.Workbook(rows:2,filled:true,unsupported:kind),"vidu.xlsx",true));Assert.Equal(s.Version,w.Read(s.Id).Version);
    }
    [Fact] public void InformationalPreviewCannotElevateApprovalOrModifySession() {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var actor=new Actor();var w=new UniversalDocWorkflowOrchestrator(actor,sessions);var s=Gate5DFixtures.Configure(w,"A");s=Gate5DFixtures.Enter(w,s,"A",bad:true);
        var p=w.PreviewOffice(s.Id,s.Version);Assert.False(p.Readiness.DataValid);Assert.False(p.Readiness.CanReviewNow);Assert.False(p.Readiness.CanApproveNow);Assert.False(p.Readiness.CanExport);Assert.Equal(s.Version,w.Read(s.Id).Version);
        Assert.Equal("DOCUMENT_VALIDATION_REQUIRED",Assert.Throws<StudioFault>(()=>w.Review(s.Id,s.Version)).Code);
        Assert.Equal(409,Assert.Throws<StudioFault>(()=>w.PreviewOffice(s.Id,s.Version-1)).Status);
        actor.Value=actor.Value with{TenantId="other"};Assert.Equal(404,Assert.Throws<StudioFault>(()=>w.PreviewOffice(s.Id,s.Version)).Status);
    }
    [Fact] public void PreviewIsBoundedAtTwentyRowsAndCannotKeepApprovalAfterChanges() {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var w=new UniversalDocWorkflowOrchestrator(new Actor(),sessions);var s=Gate5DFixtures.Configure(w,"F");s=Gate5DFixtures.Enter(w,s,"F",20);var p=w.PreviewOffice(s.Id,s.Version);
        Assert.InRange(p.Fields.Length,1,640);Assert.All(p.Fields,f=>Assert.InRange(f.Destinations.Length,1,3));Assert.Contains(p.Fields,f=>f.Row==19&&f.Destinations.Any(d=>d.Cell=="A25"));
        s=w.Review(s.Id,s.Version);s=w.Approve(s.Id,s.Version);Assert.True(w.PreviewOffice(s.Id,s.Version).Readiness.CanExport);
        s=w.ChangeValidationRules(s.Id,s.Version,s.ValidationRules.SetItem(0,s.ValidationRules[0] with{Tolerance=0.1m}));Assert.False(w.PreviewOffice(s.Id,s.Version).Readiness.CanExport);Assert.False(w.PreviewOffice(s.Id,s.Version).Readiness.MappingConfirmed);
    }
    [Theory][InlineData("anonymous")][InlineData("owner")][InlineData("tenant")]
    public async Task ActualHttpPreviewRequiresTrustedAuthorization(string kind) {
        await using var host=await StudioHttpHost.Start();using var owner=host.Client();Assert.Equal(HttpStatusCode.OK,(await owner.PostAsJsonAsync("/api/auth/login",new{username="admin-a",password="Studio@Test2026!"})).StatusCode);
        var boot=await owner.GetFromJsonAsync<JsonElement>("/api/universal-doc/bootstrap");owner.DefaultRequestHeaders.Add("X-Studio-CSRF",boot.GetProperty("csrfToken").GetString());
        var s=await(await owner.PostAsJsonAsync("/api/universal-doc/sessions",new{workflow="example-template"})).Content.ReadFromJsonAsync<StudioSessionView>();
        using var attacker=host.Client();if(kind!="anonymous")await attacker.PostAsJsonAsync("/api/auth/login",new{username=kind=="owner"?"other-a":"admin-b",password="Studio@Test2026!"});
        Assert.Equal(kind=="anonymous"?HttpStatusCode.Unauthorized:HttpStatusCode.NotFound,(await attacker.GetAsync($"/api/universal-doc/sessions/{s!.Id}/office-preview?expectedVersion={s.Version}&canExport=true")).StatusCode);
        var own=await owner.GetFromJsonAsync<OfficePreview>($"/api/universal-doc/sessions/{s!.Id}/office-preview?expectedVersion={s.Version}&canExport=true");Assert.False(own!.Readiness.CanExport);Assert.Empty(own.Fields);
    }
    [Fact] public void ReadOnlyRoleCannotElevatePreviewAndTwentyOneRowsAreRejected() {
        using var sessions=new InMemoryUniversalDocSessionStore(TimeProvider.System);var actor=new Actor();var w=new UniversalDocWorkflowOrchestrator(actor,sessions);var s=Gate5DFixtures.Configure(w,"A");
        Assert.Equal("SAMPLE_LIMIT",Assert.Throws<StudioFault>(()=>Gate5DFixtures.Enter(w,s,"A",21)).Code);Assert.Equal(s.Version,w.Read(s.Id).Version);
        s=Gate5DFixtures.Enter(w,s,"A");s=w.Review(s.Id,s.Version);s=w.Approve(s.Id,s.Version);actor.Value=actor.Value with{Role="Xnk"};var p=w.PreviewOffice(s.Id,s.Version);
        Assert.True(p.Readiness.DataValid);Assert.False(p.Readiness.CanReviewNow);Assert.False(p.Readiness.CanApproveNow);Assert.False(p.Readiness.CanExport);Assert.Contains("EXPORT_PERMISSION_REQUIRED",p.Readiness.Blockers);Assert.Equal(403,Assert.Throws<StudioFault>(()=>w.Export(s.Id,s.Version)).Status);
    }

}
