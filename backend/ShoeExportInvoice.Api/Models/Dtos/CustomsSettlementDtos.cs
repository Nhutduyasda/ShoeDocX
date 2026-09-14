using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class CalculateSettlementRequestDto
{
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
}

public class SettlementItemDto
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "đôi";
    public string HsCode { get; set; } = "64041990";
    public decimal OpeningBalance { get; set; } = 0;
    public decimal InPeriodProduction { get; set; } = 0;
    public decimal InPeriodExport { get; set; } = 0;
    public decimal OtherExport { get; set; } = 0;
    public decimal ClosingBalance { get; set; } = 0;
    public bool IsNegative => ClosingBalance < 0;
    public decimal Discrepancy => IsNegative ? Math.Abs(ClosingBalance) : 0;
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
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
    public string CustomsOffice { get; set; } = "Chi cục Hải quan Quản lý Hàng gia công";
    public string Status { get; set; } = "Draft"; // Draft, Finalized
    public string CompanyName { get; set; } = "CÔNG TY TNHH HẢI AN NEW MATERIAL HẬU GIANG";
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
    public long Version { get; set; }
}

public class SaveSettlementPeriodRequestDto
{
    public int? Id { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
    public string CustomsOffice { get; set; } = "Chi cục Hải quan Quản lý Hàng gia công";
    public string Status { get; set; } = "Draft";
    public string? CompanyName { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public List<SettlementItemDto> Items { get; set; } = new();
    public long? ExpectedVersion { get; set; }
}

public class SettlementPeriodSummaryDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
    public string CustomsOffice { get; set; } = "Chi cục Hải quan Quản lý Hàng gia công";
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalExportQuantity { get; set; }
    public decimal TotalClosingBalance { get; set; }
    public long Version { get; set; }
}

public class SettlementDrillDownItemDto
{
    public int OrderId { get; set; }
    public string DeclarationNo { get; set; } = string.Empty;
    public DateTime? ClearanceDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public string FullItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

public class AnalyticsExportStatsDto
{
    public int Year { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalDap { get; set; }
    public decimal TotalCmt { get; set; }
    public int ClearedOrderCount { get; set; }
    public List<MonthlyExportStatDto> MonthlyStats { get; set; } = new();
    public List<TopExportStyleDto> TopStyles { get; set; } = new();
    public CustomsChannelStatDto ChannelStats { get; set; } = new();
}

public class MonthlyExportStatDto
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalDap { get; set; }
    public decimal TotalCmt { get; set; }
    public int OrderCount { get; set; }
}

public class TopExportStyleDto
{
    public int Rank { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalDap { get; set; }
    public decimal TotalCmt { get; set; }
    public decimal Percentage { get; set; }
}

public class CustomsChannelStatDto
{
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
    public int TotalDeclarations { get; set; }
    public decimal GreenPercentage => TotalDeclarations > 0 ? Math.Round((decimal)GreenCount * 100 / TotalDeclarations, 1) : 0;
    public decimal YellowPercentage => TotalDeclarations > 0 ? Math.Round((decimal)YellowCount * 100 / TotalDeclarations, 1) : 0;
    public decimal RedPercentage => TotalDeclarations > 0 ? Math.Round((decimal)RedCount * 100 / TotalDeclarations, 1) : 0;
}

public class WarehouseDataRowDto
{
    public string ProductCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public decimal OpeningBalance { get; set; } = 0;
    public decimal InPeriodProduction { get; set; } = 0;
}

public class WarehouseImportResultDto
{
    public int MatchedCount { get; set; }
    public int AddedFromWarehouseCount { get; set; }
    public int TotalRows { get; set; }
    public int NegativeItemCount { get; set; }
    public List<SettlementItemDto> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class MatchWarehouseDataRequestDto
{
    public List<WarehouseDataRowDto> Rows { get; set; } = new();
    public List<SettlementItemDto> CurrentItems { get; set; } = new();
}

