namespace ShoeExportInvoice.Api.Models.Dtos;

public class CalculateSettlementRequestDto
{
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? ContractNo { get; set; }
}

public class SettlementItemDto
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "đôi";
    public decimal OpeningBalance { get; set; } = 0;
    public decimal InPeriodProduction { get; set; } = 0;
    public decimal InPeriodExport { get; set; } = 0;
    public decimal OtherExport { get; set; } = 0;
    public decimal ClosingBalance { get; set; } = 0;
    public string? Note { get; set; }
    public int ExportedOrderCount { get; set; }
    public List<string> RelatedDeclarationNos { get; set; } = new();
}

public class SettlementReportDto
{
    public int? PeriodId { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? ContractNo { get; set; }
    public string CompanyName { get; set; } = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";
    public string TaxCode { get; set; } = "4300326888";
    public string Address { get; set; } = "KCN VSIP Quảng Ngãi, Xã Tịnh Phong, Huyện Sơn Tịnh, Tỉnh Quảng Ngãi";
    public string? Note { get; set; }

    public List<SettlementItemDto> Items { get; set; } = new();

    // Tổng cộng các cột
    public decimal TotalOpeningBalance => Items.Sum(i => i.OpeningBalance);
    public decimal TotalInPeriodProduction => Items.Sum(i => i.InPeriodProduction);
    public decimal TotalInPeriodExport => Items.Sum(i => i.InPeriodExport);
    public decimal TotalOtherExport => Items.Sum(i => i.OtherExport);
    public decimal TotalClosingBalance => Items.Sum(i => i.ClosingBalance);
    public int ClearedOrderCount { get; set; }
}

public class SaveSettlementPeriodRequestDto
{
    public int? Id { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? ContractNo { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public List<SettlementItemDto> Items { get; set; } = new();
}

public class SettlementPeriodSummaryDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? ContractNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalExportQuantity { get; set; }
    public decimal TotalClosingBalance { get; set; }
}

public class SettlementDrillDownItemDto
{
    public int OrderId { get; set; }
    public string DeclarationNo { get; set; } = string.Empty;
    public DateTime? ClearanceDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string? ContractNo { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string FullItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

