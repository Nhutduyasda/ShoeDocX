using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Xnk,KeToan")]
public class CustomsController : ControllerBase
{
    private readonly ICustomsDeclarationService _customsService;
    private readonly AppDbContext _context;
    private readonly ILogger<CustomsController> _logger;
    private readonly IBusinessAuditService? _audit;

    public CustomsController(
        ICustomsDeclarationService customsService,
        AppDbContext context,
        ILogger<CustomsController> logger, IBusinessAuditService? audit = null)
    {
        _customsService = customsService;
        _context = context;
        _logger = logger;
        _audit = audit;
    }

    /// <summary>
    /// Nhận file .xls / .xlsx kết xuất từ phần mềm VNACCS, tự động bóc tách và đối soát chéo với đơn hàng trong CSDL.
    /// Không lưu vào database ở bước này.
    /// </summary>
    [HttpPost("parse-and-compare")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CustomsReconciliationResultDto>> ParseAndCompare(
        [FromForm] IFormFile file,
        [FromQuery] int? orderId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file tờ khai hải quan (.xls hoặc .xlsx)." });
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xls" && ext != ".xlsx")
        {
            return BadRequest(new { message = "Định dạng file không được hỗ trợ. Vui lòng tải file Excel kết xuất từ VNACCS (.xls hoặc .xlsx)." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var parsedDeclaration = _customsService.ParseDeclarationFile(stream, file.FileName);

            var reconciliation = await _customsService.ReconcileAsync(parsedDeclaration, orderId);
            return Ok(reconciliation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi bóc tách và đối soát file tờ khai: {FileName}", file.FileName);
            return StatusCode(500, new
            {
                message = "Không thể bóc tách file tờ khai hải quan. Vui lòng kiểm tra định dạng file kết xuất từ VNACCS."
            });
        }
    }

    /// <summary>
    /// Xác nhận đồng bộ dữ liệu hải quan vào đơn hàng và lưu trữ file tờ khai thực tế vào server.
    /// Cập nhật trạng thái đơn hàng sang Cleared (Đã thông quan) hoặc Discrepancy (Sai lệch).
    /// Khóa chỉnh sửa hồ sơ (IsLocked = true) khi thông quan thành công.
    /// </summary>
    [HttpPost("confirm-sync")]
    [HttpPost("confirm-sync/{orderId:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConfirmSync(
        [FromRoute] int? orderId,
        [FromForm] ConfirmCustomsSyncRequestDto request,
        [FromForm] IFormFile? file = null,
        [FromForm] IFormFile? customsFile = null)
    {
        int targetOrderId = orderId.HasValue && orderId.Value > 0 ? orderId.Value : request.OrderId;
        if (targetOrderId <= 0)
        {
            return BadRequest(new { message = "Vui lòng cung cấp OrderId hợp lệ để đồng bộ hồ sơ hải quan." });
        }

        try
        {
            var actualFile = file ?? customsFile ?? request.CustomsFile;
            using Stream? fileStream = actualFile?.OpenReadStream();
            string? fileName = null;

            if (actualFile != null && actualFile.Length > 0)
            {

                fileName = actualFile.FileName;
            }

            var updatedOrder = await _customsService.ConfirmSyncAsync(targetOrderId, request, fileStream, fileName);
            _audit?.Add(HttpContext, "Customs.ConfirmSync", "ShipmentOrder", updatedOrder.Id,
                next: new { updatedOrder.DeclarationNo, updatedOrder.Status, updatedOrder.IsLocked });
            if (_audit != null) await _context.SaveChangesAsync();

            return Ok(new
            {
                message = updatedOrder.IsLocked
                    ? $"Đồng bộ tờ khai {updatedOrder.DeclarationNo} thành công! Đơn hàng đã chuyển sang trạng thái Đã thông quan."
                    : "Đã cập nhật thông tin tờ khai. Đơn hàng được ghi nhận có sai lệch số liệu so với tờ khai hải quan.",
                orderId = updatedOrder.Id,
                invoiceNo = updatedOrder.InvoiceNo,
                declarationNo = updatedOrder.DeclarationNo,
                clearanceDate = updatedOrder.ClearanceDate,
                customsDeclarationType = updatedOrder.CustomsDeclarationType,
                customsChannel = updatedOrder.CustomsChannel,
                customsOffice = updatedOrder.CustomsOffice,
                customsPackageQty = updatedOrder.CustomsPackageQty,
                customsGrossWeight = updatedOrder.CustomsGrossWeight,
                customsTotalDap = updatedOrder.CustomsTotalDap,
                customsTotalCmt = updatedOrder.CustomsTotalCmt,
                customsAttachmentFileName = updatedOrder.CustomsAttachmentFileName,
                isLocked = updatedOrder.IsLocked,
                status = updatedOrder.Status,
                statusName = updatedOrder.Status.ToString()
            });
        }
        catch (KeyNotFoundException knf)
        {
            return NotFound(new { message = knf.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xác nhận đồng bộ hải quan cho đơn hàng #{OrderId}", targetOrderId);
            return StatusCode(500, new
            {
                message = "Lỗi khi lưu thông tin hải quan vào đơn hàng."
            });
        }
    }

    /// <summary>
    /// Tải xuống file tờ khai gốc đã đính kèm theo đơn hàng
    /// </summary>
    [HttpGet("download/{orderId:int}")]
    public async Task<IActionResult> DownloadAttachment(int orderId)
    {
        try
        {
            var attachment = await _customsService.GetAttachmentAsync(orderId);
            if (attachment == null)
            {
                return NotFound(new { message = "Đơn hàng chưa có file tờ khai hải quan đính kèm hoặc file không tồn tại trên server." });
            }

            return File(attachment.Value.Bytes, attachment.Value.ContentType, attachment.Value.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file tờ khai đính kèm đơn hàng #{OrderId}", orderId);
            return StatusCode(500, new { message = "Lỗi khi tải file tờ khai." });
        }
    }

    /// <summary>
    /// Lấy Cây Thư Mục Lưu Trữ Hồ Sơ Hải Quan phân cấp 4 tầng:
    /// Đối tác -> Năm -> Hợp đồng -> Luồng xử lý / Chờ đối soát
    /// </summary>
    [HttpGet("archive-tree")]
    public async Task<ActionResult<List<CustomsArchiveTreeNodeDto>>> GetArchiveTree()
    {
        var allFolders = await _context.MasterDataFolders.AsNoTracking().ToListAsync();
        var folderMap = allFolders.ToDictionary(f => f.Id);

        MasterDataFolder? GetRootFolder(int folderId)
        {
            if (!folderMap.TryGetValue(folderId, out var curr)) return null;
            var visited = new HashSet<int>();
            while (curr.ParentId.HasValue && folderMap.TryGetValue(curr.ParentId.Value, out var parent) && visited.Add(curr.Id))
            {
                curr = parent;
            }
            return curr;
        }

        var rootFolders = allFolders.Where(f => !f.ParentId.HasValue).OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name).ToList();

        var shipments = await _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .OrderByDescending(s => s.InvoiceDate)
            .ThenByDescending(s => s.Id)
            .ToListAsync();

        // PartnerKey -> (Title, PartnerFolderId, YearsMap)
        // YearsMap: Year -> ContractsMap
        // ContractsMap: ContractNo -> List<ShipmentOrder>
        var partnerGroup = new Dictionary<string, (string Title, int? FolderId, Dictionary<int, Dictionary<string, List<ShipmentOrder>>> Years)>(StringComparer.OrdinalIgnoreCase);

        // Khởi tạo các root folder từ Master Data trước (để đối tác mới tạo cũng xuất hiện nhánh riêng)
        foreach (var rf in rootFolders)
        {
            var pKey = $"partner_{rf.Id}";
            var title = !string.IsNullOrWhiteSpace(rf.CustomerName) ? rf.CustomerName : rf.Name;
            if (!partnerGroup.ContainsKey(pKey))
            {
                partnerGroup[pKey] = (title, rf.Id, new Dictionary<int, Dictionary<string, List<ShipmentOrder>>>());
            }
        }

        foreach (var s in shipments)
        {
            int? partnerFolderId = null;
            string partnerKey;
            string partnerTitle;

            if (s.ContractFolderId.HasValue && folderMap.ContainsKey(s.ContractFolderId.Value))
            {
                var root = GetRootFolder(s.ContractFolderId.Value);
                if (root != null)
                {
                    partnerFolderId = root.Id;
                    partnerKey = $"partner_{root.Id}";
                    partnerTitle = !string.IsNullOrWhiteSpace(root.CustomerName) ? root.CustomerName : root.Name;
                }
                else
                {
                    partnerKey = "partner_other";
                    partnerTitle = !string.IsNullOrWhiteSpace(s.CustomerName) ? s.CustomerName : "Đối tác khác";
                }
            }
            else
            {
                var matched = rootFolders.FirstOrDefault(r =>
                    (!string.IsNullOrWhiteSpace(r.CustomerName) && string.Equals(r.CustomerName, s.CustomerName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(r.Name) && string.Equals(r.Name, s.CustomerName, StringComparison.OrdinalIgnoreCase)));

                if (matched != null)
                {
                    partnerFolderId = matched.Id;
                    partnerKey = $"partner_{matched.Id}";
                    partnerTitle = !string.IsNullOrWhiteSpace(matched.CustomerName) ? matched.CustomerName : matched.Name;
                }
                else
                {
                    var safeName = !string.IsNullOrWhiteSpace(s.CustomerName) ? s.CustomerName.Trim() : "Đối tác khác";
                    partnerKey = $"partner_cust_{Math.Abs(safeName.GetHashCode())}";
                    partnerTitle = safeName;
                }
            }

            if (!partnerGroup.TryGetValue(partnerKey, out var pData))
            {
                pData = (partnerTitle, partnerFolderId, new Dictionary<int, Dictionary<string, List<ShipmentOrder>>>());
                partnerGroup[partnerKey] = pData;
            }

            int year = s.ClearanceDate?.Year ?? s.InvoiceDate.Year;
            if (!pData.Years.TryGetValue(year, out var contractsMap))
            {
                contractsMap = new Dictionary<string, List<ShipmentOrder>>(StringComparer.OrdinalIgnoreCase);
                pData.Years[year] = contractsMap;
            }

            string contractNo = !string.IsNullOrWhiteSpace(s.ContractNo) ? s.ContractNo.Trim() : "Không có HĐ";
            if (!contractsMap.TryGetValue(contractNo, out var contractOrders))
            {
                contractOrders = new List<ShipmentOrder>();
                contractsMap[contractNo] = contractOrders;
            }

            contractOrders.Add(s);
        }

        var resultNodes = new List<CustomsArchiveTreeNodeDto>();

        foreach (var (pKey, (pTitle, pFolderId, yearsMap)) in partnerGroup)
        {
            var yearNodes = new List<CustomsArchiveTreeNodeDto>();
            int partnerTotalCount = 0;

            var sortedYears = yearsMap.Keys.OrderByDescending(y => y).ToList();
            foreach (var year in sortedYears)
            {
                var contractsMap = yearsMap[year];
                var contractNodes = new List<CustomsArchiveTreeNodeDto>();
                int yearTotalCount = 0;

                var sortedContracts = contractsMap.Keys.OrderBy(c => c).ToList();
                foreach (var contractNo in sortedContracts)
                {
                    var orders = contractsMap[contractNo];
                    var safeContractSlug = contractNo.Replace("/", "_").Replace("\\", "_").Replace(" ", "_").Replace(":", "_");
                    var contractKey = $"{pKey}_year_{year}_contract_{safeContractSlug}";

                    int greenCount = 0;
                    int yellowCount = 0;
                    int redCount = 0;
                    int pendingCount = 0;

                    foreach (var o in orders)
                    {
                        if (o.Status == ShipmentStatus.Cleared && o.CustomsChannel == 1)
                            greenCount++;
                        else if (o.Status == ShipmentStatus.Cleared && o.CustomsChannel == 2)
                            yellowCount++;
                        else if (o.Status == ShipmentStatus.Cleared && o.CustomsChannel == 3)
                            redCount++;
                        else
                            pendingCount++;
                    }

                    var channelChildren = new List<CustomsArchiveTreeNodeDto>
                    {
                        new()
                        {
                            Key = $"{contractKey}_channel_green",
                            Title = "Luồng 1 (Xanh) - Đã thông quan",
                            Count = greenCount,
                            PartnerFolderId = pFolderId,
                            PartnerName = pTitle,
                            Year = year,
                            ContractNo = contractNo,
                            FilterType = "Green",
                            Channel = 1,
                            CustomsStatus = "Cleared"
                        },
                        new()
                        {
                            Key = $"{contractKey}_channel_yellow",
                            Title = "Luồng 2 (Vàng) - Kiểm tra hồ sơ",
                            Count = yellowCount,
                            PartnerFolderId = pFolderId,
                            PartnerName = pTitle,
                            Year = year,
                            ContractNo = contractNo,
                            FilterType = "Yellow",
                            Channel = 2,
                            CustomsStatus = "Cleared"
                        },
                        new()
                        {
                            Key = $"{contractKey}_channel_red",
                            Title = "Luồng 3 (Đỏ) - Kiểm hóa",
                            Count = redCount,
                            PartnerFolderId = pFolderId,
                            PartnerName = pTitle,
                            Year = year,
                            ContractNo = contractNo,
                            FilterType = "Red",
                            Channel = 3,
                            CustomsStatus = "Cleared"
                        },
                        new()
                        {
                            Key = $"{contractKey}_channel_pending",
                            Title = "Chờ đối soát",
                            Count = pendingCount,
                            PartnerFolderId = pFolderId,
                            PartnerName = pTitle,
                            Year = year,
                            ContractNo = contractNo,
                            FilterType = "Pending",
                            CustomsStatus = "Pending"
                        }
                    };

                    int contractTotal = orders.Count;
                    yearTotalCount += contractTotal;

                    var displayContractTitle = contractNo.StartsWith("HĐ", StringComparison.OrdinalIgnoreCase)
                        ? contractNo
                        : $"HĐ: {contractNo}";

                    contractNodes.Add(new CustomsArchiveTreeNodeDto
                    {
                        Key = contractKey,
                        Title = displayContractTitle,
                        Count = contractTotal,
                        PartnerFolderId = pFolderId,
                        PartnerName = pTitle,
                        Year = year,
                        ContractNo = contractNo,
                        FilterType = "Contract",
                        Children = channelChildren
                    });
                }

                partnerTotalCount += yearTotalCount;

                var yearKey = $"{pKey}_year_{year}";
                yearNodes.Add(new CustomsArchiveTreeNodeDto
                {
                    Key = yearKey,
                    Title = $"Năm {year}",
                    Count = yearTotalCount,
                    PartnerFolderId = pFolderId,
                    PartnerName = pTitle,
                    Year = year,
                    FilterType = "Year",
                    Children = contractNodes
                });
            }

            resultNodes.Add(new CustomsArchiveTreeNodeDto
            {
                Key = pKey,
                Title = pTitle,
                Count = partnerTotalCount,
                PartnerFolderId = pFolderId,
                PartnerName = pTitle,
                FilterType = "Partner",
                Children = yearNodes
            });
        }

        return Ok(resultNodes);
    }

    /// <summary>
    /// Lọc danh sách hồ sơ tờ khai hải quan theo các tiêu chí: Đối tác, Năm, Hợp đồng, Luồng, Trạng thái, Từ khóa
    /// </summary>
    [HttpGet("declarations")]
    public async Task<ActionResult<PagedResultDto<CustomsDeclarationSummaryDto>>> GetDeclarations(
        [FromQuery] CustomsDeclarationFilterDto filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1 || pageSize is < 1 or > 200)
            return BadRequest(new { message = "page phải >= 1 và pageSize phải từ 1 đến 200." });

        var query = _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .AsQueryable();

        if (filter.PartnerFolderId.HasValue && filter.PartnerFolderId.Value > 0)
        {
            var descendantFolderIds = await GetFolderAndDescendantIdsAsync(filter.PartnerFolderId.Value);
            var partnerFolder = await _context.MasterDataFolders.FindAsync(filter.PartnerFolderId.Value);
            var partnerCustomerName = partnerFolder?.CustomerName;

            query = query.Where(s =>
                (s.ContractFolderId.HasValue && descendantFolderIds.Contains(s.ContractFolderId.Value)) ||
                (!string.IsNullOrEmpty(partnerCustomerName) && s.CustomerName == partnerCustomerName));
        }

        if (filter.Year.HasValue)
        {
            int y = filter.Year.Value;
            query = query.Where(s => (s.ClearanceDate.HasValue ? s.ClearanceDate.Value.Year : s.InvoiceDate.Year) == y);
        }

        if (!string.IsNullOrWhiteSpace(filter.ContractNo))
        {
            var cNo = filter.ContractNo.Trim();
            query = query.Where(s => s.ContractNo != null && s.ContractNo.ToLower() == cNo.ToLower());
        }

        if (filter.Channel.HasValue)
        {
            query = query.Where(s => s.CustomsChannel == filter.Channel.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomsStatus))
        {
            var status = filter.CustomsStatus.Trim();
            if (status.Equals("Cleared", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.Status == ShipmentStatus.Cleared);
            }
            else if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.Status != ShipmentStatus.Cleared || s.CustomsChannel == null || string.IsNullOrEmpty(s.DeclarationNo));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(s =>
                s.InvoiceNo.ToLower().Contains(kw) ||
                (s.DeclarationNo != null && s.DeclarationNo.ToLower().Contains(kw)) ||
                s.CustomerName.ToLower().Contains(kw) ||
                (s.ContractNo != null && s.ContractNo.ToLower().Contains(kw)) ||
                s.Items.Any(i => i.StyleCode.ToLower().Contains(kw) || (i.FullItemCode != null && i.FullItemCode.ToLower().Contains(kw))));
        }

        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderByDescending(s => s.InvoiceDate)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResultDto<CustomsDeclarationSummaryDto>
        {
            Items = rows.Select(ToShipmentSummary).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private async Task<List<int>> GetFolderAndDescendantIdsAsync(int rootId)
    {
        var folders = await _context.MasterDataFolders.AsNoTracking()
            .Select(f => new { f.Id, f.ParentId }).ToListAsync();
        var result = new HashSet<int> { rootId };
        var pending = new Queue<int>();
        pending.Enqueue(rootId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in folders.Where(f => f.ParentId == current))
                if (result.Add(child.Id)) pending.Enqueue(child.Id);
        }
        return result.ToList();
    }

    private static CustomsDeclarationSummaryDto ToShipmentSummary(ShipmentOrder s) => new()
    {
        Id = s.Id,
        InvoiceNo = s.InvoiceNo,
        InvoiceDate = s.InvoiceDate,
        PoSuffix = s.PoSuffix,
        ContractFolderId = s.ContractFolderId,
        ContractNo = s.ContractNo,
        CustomerName = s.CustomerName,
        DeliveryTerms = s.DeliveryTerms,
        PaymentTerms = s.PaymentTerms,
        CreatedAt = s.CreatedAt,
        Status = (int)s.Status,
        StatusName = s.Status.ToString(),
        DeclarationNo = s.DeclarationNo,
        ClearanceDate = s.ClearanceDate,
        CustomsDeclarationType = s.CustomsDeclarationType,
        CustomsChannel = s.CustomsChannel,
        CustomsOffice = s.CustomsOffice,
        CustomsPackageQty = s.CustomsPackageQty,
        CustomsGrossWeight = s.CustomsGrossWeight,
        CustomsTotalDap = s.CustomsTotalDap,
        CustomsTotalCmt = s.CustomsTotalCmt,
        CustomsAttachmentFileName = s.CustomsAttachmentFileName,
        HasCustomsAttachment = !string.IsNullOrWhiteSpace(s.CustomsAttachmentFileName),
        IsLocked = s.IsLocked || s.Status == ShipmentStatus.Cleared,
        ItemCount = s.Items.Count,
        TotalQuantity = s.Items.Sum(i => i.Quantity),
        TotalAmountCMT = s.Items.Sum(i => i.UnitPriceCMT * i.Quantity),
        TotalAmountDAP = s.Items.Sum(i => i.UnitPriceDAP * i.Quantity),
        TotalCartons = s.Items.Sum(i => (int)Math.Ceiling((double)i.Quantity / Math.Max(i.PairPerCarton, 1)))
    };
}

