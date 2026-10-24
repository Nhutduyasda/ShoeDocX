using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

// Synthetic office corpus. Shared only by tests and the isolated authenticated test host.
public static class Gate5DFixtures
{
    public static string[] Names(string scenario) => scenario switch {
        "B" => ["NhapKho"], "C" => ["XuatKho"], "D" => ["Packing"],
        "E" => ["Kho", "TongHop"], "F" => ["Kho", "TongHop", "ChiTiet"], _ => ["HoaDon"] };
    public static string[] Labels(string scenario) => scenario=="D" ? ["Mã kiện","Số kiện","Trọng lượng","Tổng trọng lượng","Đơn vị"] :
        scenario=="B" ? ["Mã kho","Số lượng nhập","Đơn giá","Giá trị nhập","Đơn vị"] :
        scenario=="C" ? ["Mã vật tư","Số lượng xuất","Đơn giá","Giá trị xuất","Đơn vị"] : ["Mã hàng","Số lượng","Đơn giá","Thành tiền","Đơn vị"];
    public static byte[] Workbook(string scenario="A",int rows=1,bool filled=false,string? unsupported=null)
    {
        using var wb=new XLWorkbook();var labels=Labels(scenario);
        foreach(var name in Names(scenario)) {
            var ws=wb.AddWorksheet(name);
            ws.Cell("A1").Value="Số phiếu";ws.Cell("B1").Value=filled?"CT-00123":"CT-00000";
            ws.Cell("A2").Value="Ngày chứng từ";ws.Cell("B2").Value=new DateTime(2026,10,24);ws.Cell("B2").Style.NumberFormat.Format="yyyy-mm-dd";
            ws.Cell("A3").Value="Đã đối chiếu";ws.Cell("B3").Value=false;
            // Repeated header label creates a genuine ambiguous candidate, never a fake confidence score.
            ws.Cell("A4").Value="Số phiếu";ws.Cell("B4").Value="KHÔNG DÙNG";
            for(var col=1;col<=5;col++){ws.Cell(5,col).Value=labels[col-1];ws.Cell(5,col).Style.Font.Bold=true;}
            for(var row=6;row<6+rows;row++) {
                ws.Cell(row,1).Value=filled?"0012"+(row-5):"00000";
                ws.Cell(row,2).Value=filled?2.5:0;ws.Cell(row,3).Value=filled?3:0;ws.Cell(row,4).Value=filled?7.5:0;ws.Cell(row,5).Value="cái";
                ws.Range(row,1,row,6).Style.Border.BottomBorder=XLBorderStyleValues.Thin;
                ws.Range(row,2,row,4).Style.NumberFormat.Format="0.00";
                ws.Cell(row,6).FormulaA1=$"B{row}*C{row}";
            }
            ws.Cell(6+rows,1).Value="Tổng cộng";ws.Cell(6+rows,6).FormulaA1=$"SUM(F6:F{5+rows})";
            ws.Range(7+rows,1,7+rows,4).Merge().Value="Người lập phiếu — chữ ký";
        }
        wb.AddWorksheet("ThamChieu").Cell("A1").Value="Tham chiếu giữ nguyên 00123";
        if(unsupported=="cross-sheet")wb.Worksheet(1).Cell("F6").FormulaA1="ThamChieu!A1";
        if(unsupported=="far")wb.Worksheet(1).Cell("XFD1").Value="Ngoài giới hạn";
        if(unsupported=="reference")wb.Worksheet("ThamChieu").Cell("B1").Value="Đã thay cấu trúc";
        if(unsupported=="merge")wb.Worksheet(1).Range("B6:C6").Merge();
        using var stream=new MemoryStream();wb.SaveAs(stream);return stream.ToArray();
    }
    public static StudioSessionView Configure(UniversalDocWorkflowOrchestrator w,string scenario,int exampleRows=0) {
        var s=w.Create("example-template");s=w.Upload(s.Id,s.Version,Workbook(scenario),"mau.xlsx");
        if(exampleRows>0)s=w.UploadLearning(s.Id,s.Version,Workbook(scenario,exampleRows,true),"vidu.xlsx",true);
        var primary=Names(scenario)[0];var labels=Labels(scenario);
        s=w.CorrectSchema(s.Id,s.Version,s.SchemaDraft!.Fields.Select(f=>new SchemaFieldCorrection(f.Key,f.Label,f.Scope,
            f.Scope=="LineItem"&&labels.Skip(1).Take(3).Contains(f.Label)?"Decimal":f.Type,f.Label=="Số phiếu"&&f.Candidates[0].Worksheet==primary,
            f.Candidates[0].Worksheet!=primary,0)).ToImmutableArray());
        s=w.ChangeMultisheetMapping(s.Id,s.Version,Names(scenario).Select(name=>new StudioSheetMapping(name,6,s.Mapping!.Headers,s.Mapping.Columns)).ToImmutableArray());
        string Key(string label)=>s.SchemaDraft!.Fields.Single(f=>!f.Omit&&f.Label==label).Key;
        s=w.ChangeValidationRules(s.Id,s.Version,[new("amount","Arithmetic",Key(labels[1]),"Multiply",Key(labels[2]),Key(labels[3]),0,true,"BlockingError")]);
        return w.ConfirmMapping(s.Id,s.Version);
    }
    public static StudioSessionView Enter(UniversalDocWorkflowOrchestrator w,StudioSessionView s,string scenario,int rows=2,bool bad=false) {
        var labels=Labels(scenario);string Key(string label)=>s.SchemaDraft!.Fields.Single(f=>!f.Omit&&f.Label==label).Key;
        return w.SetManualSample(s.Id,s.Version,new(
            ImmutableDictionary<string,string?>.Empty.Add(Key("Số phiếu"),"CT-00123").Add(Key("Ngày chứng từ"),"2026-10-24").Add(Key("Đã đối chiếu"),"true"),
            Enumerable.Range(0,rows).Select(i=>ImmutableDictionary<string,string?>.Empty.Add(Key(labels[0]),"0012"+(i+1)).Add(Key(labels[1]),"2.50").Add(Key(labels[2]),"3").Add(Key(labels[3]),bad?"8":"7.50").Add(Key(labels[4]),"cái")).ToImmutableArray()));
    }
}
