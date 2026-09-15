namespace ShoeExportInvoice.Api.Models.Templates;

public class DocumentTemplateConfig
{
    public string TemplateName { get; set; } = string.Empty;
    public InvSheetConfig InvSheet { get; set; } = new();
    public PklSheetConfig PklSheet { get; set; } = new();
}

public class InvSheetConfig
{
    public string SheetName { get; set; } = "INV";
    public InvHeaderCells Header { get; set; } = new();
    public InvTableColumns Table { get; set; } = new();
    public string? TotalAmountCell { get; set; }
    public string? WordsAmountCell { get; set; }
}

public class InvHeaderCells
{
    public string InvoiceNoCell { get; set; } = "J4";
    public string DateCell { get; set; } = "J5";
    public string ContractNoCell { get; set; } = "J6";
    public string DeliveryTermsCell { get; set; } = "J7";
    public string PaymentTermsCell { get; set; } = "J8";
    public string DestinationCell { get; set; } = "J9";
    public string BuyerNameCell { get; set; } = "D4";
    public string BuyerAddressCell { get; set; } = "D5";
}

public class InvTableColumns
{
    public int StartRow { get; set; } = 13;
    public string SttCol { get; set; } = "B";
    public string ItemCodeCol { get; set; } = "C";
    public string DescriptionCol { get; set; } = "D";
    public string QuantityCol { get; set; } = "E";
    public string UnitCol { get; set; } = "F";
    public string CmtUnitPriceCol { get; set; } = "G";
    public string DapUnitPriceCol { get; set; } = "H";
    public string CmtAmountCol { get; set; } = "I";
    public string DapAmountCol { get; set; } = "J";
}

public class PklSheetConfig
{
    public string SheetName { get; set; } = "PKL";
    public int StartRow { get; set; } = 12;
    public string CartonRangeCol { get; set; } = "A";
    public string ItemCodeCol { get; set; } = "B";
    public string DescriptionCol { get; set; } = "C";
    public string QuantityCol { get; set; } = "D";
    public string UnitCol { get; set; } = "E";
    public string CartonsCol { get; set; } = "F";
    public string NetWeightCol { get; set; } = "G";
    public string GrossWeightCol { get; set; } = "H";
}
