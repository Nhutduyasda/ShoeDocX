using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public sealed class ShipmentDispatchService : IShipmentDispatchService
{
    private readonly AppDbContext _db;
    private readonly IExcelImportExportService _excel;
    private readonly ISequenceService _sequences;
    private readonly ILogger<ShipmentDispatchService> _logger;
    private readonly IHttpContextAccessor _http;

    public ShipmentDispatchService(AppDbContext db, IExcelImportExportService excel, ISequenceService sequences,
        ILogger<ShipmentDispatchService> logger, IHttpContextAccessor http)
    {
        _db = db;
        _excel = excel;
        _sequences = sequences;
        _logger = logger;
        _http = http;
    }

    public async Task<MergeShipmentPreviewResponseDto> PreviewMergeAsync(MergeShipmentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SourceDocuments.Count > 0)
        {
            var (documents, ocrFolder) = await LoadOcrMergeSourcesAsync(request, cancellationToken);
            return await BuildMergePreviewAsync(documents.SelectMany(d => d.Items), ocrFolder, request, cancellationToken);
        }
        _logger.LogInformation("Dispatch Merge Started for batches {BatchIds}", request.SourceBatchIds);
        var (batches, folder) = await LoadMergeSourcesAsync(request, cancellationToken);
        return await BuildMergePreviewAsync(batches.SelectMany(b => b.Items.Select(i => new CreateShipmentItemDto
            { StyleCode = i.StyleCode, Quantity = i.Quantity, ProcessType = i.ProcessType })), folder, request, cancellationToken);
    }

    private async Task<MergeShipmentPreviewResponseDto> BuildMergePreviewAsync(IEnumerable<CreateShipmentItemDto> sourceItems,
        MasterDataFolder folder, MergeShipmentRequestDto request, CancellationToken cancellationToken)
    {
        var sourceList = sourceItems.ToList();
        var items = sourceList
            .GroupBy(i => new ItemKey(NormalizeStyle(i.StyleCode), i.ProcessType))
            .Select(g => new CreateShipmentItemDto { StyleCode = g.Key.StyleCode, ProcessType = g.Key.ProcessType, Quantity = g.Sum(x => x.Quantity) })
            .OrderBy(i => i.StyleCode).ThenBy(i => i.ProcessType).ToList();
        var shipment = BuildBaseRequest(folder, request.TemplateId, request.InvoiceDate, request.PoSuffix, request.InvoiceNo, items);
        await ApplyMasterDataAsync(shipment, cancellationToken);
        var pkl = _excel.CalculatePklBreakdown(shipment);
        var result = new MergeShipmentPreviewResponseDto
        {
            SourceItemCount = sourceList.Count,
            TotalQuantity = items.Sum(i => i.Quantity),
            TotalCartons = pkl.TotalCartons,
            MergedItems = items,
            PklBreakdown = pkl.BreakdownItems,
            Warnings = items.Where(i => i.Quantity % i.PairPerCarton!.Value != 0).Select(OddCartonWarning).ToList(),
            ProcessGroups = items.GroupBy(i => i.ProcessType).Select(g => new ProcessGroupPreviewDto
                { ProcessType = g.Key, ItemCount = g.Count(), TotalQuantity = g.Sum(i => i.Quantity) }).ToList()
        };
        result.GeneratedDocumentCount = result.ProcessGroups.Count;
        result.BlockingErrors = ReconciliationErrors(request.SourceDocuments);
        result.IsExportable = result.BlockingErrors.Count == 0;
        _logger.LogInformation("Dispatch Merge Completed preview, quantity {Quantity}", result.TotalQuantity);
        return result;
    }

    public async Task<ValidateSplitResultDto> ValidateSplitAsync(ValidateSplitRequestDto request, CancellationToken cancellationToken)
    {
        var result = new ValidateSplitResultDto();
        Dictionary<ItemKey, int> original;
        if (request.SourceDocuments.Count > 0)
        {
            var documents = request.SourceDocuments.Where(d => !string.IsNullOrWhiteSpace(d.DocumentId))
                .GroupBy(d => d.DocumentId, StringComparer.Ordinal).Select(g => g.First()).ToList();
            if (documents.Count < 2) AddError(result, "MERGED_SOURCE_COUNT_INVALID", "Nguồn gom phải có ít nhất 2 OCR document.");
            original = NormalizeSourceItems(documents.SelectMany(d => d.Items), result);
        }
        else if (request.SourceDocument != null)
        {
            if (string.IsNullOrWhiteSpace(request.SourceDocument.DocumentId))
                AddError(result, "SOURCE_DOCUMENT_INVALID", "OCR document không có documentId hợp lệ.");
            original = NormalizeSourceItems(request.SourceDocument.Items, result);
        }
        else
        {
            var batch = request.SourceBatchId.HasValue ? await _db.WarehouseBatches.AsNoTracking().Include(b => b.Items)
                .SingleOrDefaultAsync(b => b.Id == request.SourceBatchId.Value, cancellationToken) : null;
            if (batch == null)
            {
                AddError(result, "SOURCE_NOT_FOUND", "Không tìm thấy nguồn tách hóa đơn.");
                return Finish(result);
            }
            if (batch.Status == WarehouseBatchStatus.ProcessedByXnk)
                AddError(result, "SOURCE_BATCH_INVALID", "Đợt nguồn đã được xử lý trước đó.");
            original = batch.Items.GroupBy(i => new ItemKey(NormalizeStyle(i.StyleCode), i.ProcessType))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        }
        if (request.SubInvoices.Count is < 2 or > 10)
            AddError(result, "SPLIT_INVOICE_COUNT_INVALID", "Phải phân bổ từ 2 đến 10 hóa đơn con.");
        result.OriginalTotal = original.Values.Sum();

        var allocated = new Dictionary<ItemKey, int>();
        for (var index = 0; index < request.SubInvoices.Count; index++)
        {
            var sub = request.SubInvoices[index];
            if (sub.Items.Count == 0)
            {
                AddError(result, "EMPTY_SUBINVOICE", $"Hóa đơn con {index + 1} không có mặt hàng.");
                continue;
            }
            foreach (var item in sub.Items)
            {
                var code = NormalizeStyle(item.StyleCode);
                var key = new ItemKey(code, item.ProcessType);
                if (item.Quantity <= 0) AddError(result, "INVALID_ITEM_QUANTITY", $"Số lượng mã {code} phải lớn hơn 0.", code);
                if (!Enum.IsDefined(item.ProcessType) || !original.ContainsKey(key))
                    AddError(result, "SOURCE_ITEM_NOT_FOUND", $"Mã {code} / công đoạn {item.ProcessType} không tồn tại trong đợt nguồn.", code);
                if (item.Quantity > 0) allocated[key] = allocated.GetValueOrDefault(key) + item.Quantity;
                if (item.Quantity > 0 && item.Quantity % 12 != 0) result.Warnings.Add(OddCartonWarning(item));
            }
        }
        result.AllocatedTotal = allocated.Values.Sum();
        foreach (var key in original.Keys.Union(allocated.Keys).OrderBy(k => k.StyleCode).ThenBy(k => k.ProcessType))
        {
            var check = new ItemAllocationCheckDto { StyleCode = key.StyleCode, ProcessType = key.ProcessType,
                OriginalQty = original.GetValueOrDefault(key), AllocatedQty = allocated.GetValueOrDefault(key) };
            result.ItemChecks.Add(check);
            if (!check.IsMatched)
            {
                var direction = check.Discrepancy < 0 ? $"còn thiếu {-check.Discrepancy:N0}" : $"thừa {check.Discrepancy:N0}";
                AddError(result, check.Discrepancy < 0 ? "SPLIT_QTY_UNDER_ALLOCATED" : "SPLIT_QTY_OVER_ALLOCATED",
                    $"Mã {key.StyleCode}: số lượng gốc {check.OriginalQty:N0} đôi, đã phân bổ {check.AllocatedQty:N0} đôi, {direction} đôi.", key.StyleCode);
            }
        }
        if (result.AllocatedTotal != result.OriginalTotal)
            AddError(result, "SPLIT_TOTAL_MISMATCH", $"Số lượng gốc {result.OriginalTotal:N0} đôi, đã phân bổ {result.AllocatedTotal:N0} đôi, chênh lệch {result.Discrepancy:N0} đôi.");
        return Finish(result);
    }

    public async Task<ExportFileResult> ExportMergeAsync(MergeShipmentRequestDto request, CancellationToken cancellationToken)
    {
        var preview = await PreviewMergeAsync(request, cancellationToken);
        if (!preview.IsExportable)
            throw new DispatchBusinessException("OCR_DOCUMENT_INVALID", "Dữ liệu OCR chưa đối soát khớp tổng trên phiếu.", preview.BlockingErrors);
        var isOcr = request.SourceDocuments.Count > 0;
        var batches = new List<WarehouseBatch>();
        MasterDataFolder folder;
        if (isOcr) (_, folder) = await LoadOcrMergeSourcesAsync(request, cancellationToken);
        else (batches, folder) = await LoadMergeSourcesAsync(request, cancellationToken);
        await ValidateTemplateAsync(request.TemplateId, folder.Id, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var physicalGroups = preview.MergedItems.GroupBy(i => i.ProcessType).ToList();
            var numbers = await _sequences.ReservePartnerSequenceNumbersAsync(folder.Id, physicalGroups.Count);
            var files = new List<(string Name, byte[] Content)>();
            var orders = new List<ShipmentOrder>();
            for (var i = 0; i < physicalGroups.Count; i++)
            {
                var invoiceNo = PartnerDocumentPatternFormatter.InvoiceNo(folder.InvoiceNoPattern, numbers[i]);
                if (await _db.ShipmentOrders.AnyAsync(o => o.InvoiceNo == invoiceNo, cancellationToken))
                    throw new DispatchBusinessException("INVOICE_NUMBER_CONFLICT", $"Số hóa đơn {invoiceNo} đã tồn tại.");
                var dto = BuildBaseRequest(folder, request.TemplateId, request.InvoiceDate, request.PoSuffix, invoiceNo, physicalGroups[i].ToList());
                await ApplyMasterDataAsync(dto, cancellationToken);
                var content = await _excel.ExportShipmentMultiSheetExcelAsync(dto);
                var order = CreateOrder(dto, ShipmentSourceRelationType.MergeSource, batches.Select(b => b.Id));
                _db.ShipmentOrders.Add(order);
                orders.Add(order);
                files.Add((PartnerDocumentPatternFormatter.FileName(folder.FileNamePattern, numbers[i]), content));
            }
            foreach (var batch in batches) batch.Status = WarehouseBatchStatus.ProcessedByXnk;
            AddAudit("MERGE_EXPORT", isOcr ? request.SourceDocuments.Select(d => d.DocumentId) : request.SourceBatchIds.Select(x => x.ToString()), orders, preview.TotalQuantity);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return files.Count == 1
                ? new ExportFileResult(files[0].Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", files[0].Name)
                : new ExportFileResult(BuildZip(files), "application/zip", $"Bao_Cao_Gom_Dot_{request.InvoiceDate:yyyyMMdd}_{numbers[0]}_to_{numbers[^1]}.zip");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Dispatch Export Failed for merge sources {Sources}", isOcr ? request.SourceDocuments.Select(d => d.DocumentId) : request.SourceBatchIds.Select(x => x.ToString()));
            throw;
        }
    }

    public async Task<ExportFileResult> ExportSplitZipAsync(SplitShipmentRequestDto request, CancellationToken cancellationToken)
    {
        var sourceIds = request.SourceDocuments.Count > 0 ? request.SourceDocuments.Select(d => d.DocumentId).ToList()
            : request.SourceDocument != null ? [request.SourceDocument.DocumentId] : [];
        _logger.LogInformation("Dispatch Split Export Started for source {Source}", sourceIds.Count > 0 ? string.Join(",", sourceIds) : request.SourceBatchId?.ToString());
        var validation = await ValidateSplitAsync(new ValidateSplitRequestDto { SourceBatchId = request.SourceBatchId, SourceDocument = request.SourceDocument, SourceDocuments = request.SourceDocuments, SubInvoices = request.SubInvoices }, cancellationToken);
        if (!validation.IsValid) throw new DispatchValidationException(validation);
        var isOcr = request.SourceDocument != null || request.SourceDocuments.Count > 0;
        WarehouseBatch? batch = null;
        if (!isOcr && request.SourceBatchId.HasValue)
            batch = await _db.WarehouseBatches.Include(b => b.Items).SingleAsync(b => b.Id == request.SourceBatchId.Value, cancellationToken);
        var folderId = request.ContractFolderId ?? batch?.ContractFolderId ?? throw new InvalidOperationException("Nguồn OCR chưa được gán hợp đồng.");
        if (batch?.ContractFolderId.HasValue == true && batch.ContractFolderId != folderId) throw new InvalidOperationException("Hợp đồng không phù hợp với đợt nguồn.");
        var folder = await _db.MasterDataFolders.SingleOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new InvalidOperationException("Hợp đồng không tồn tại.");
        await ValidateTemplateAsync(request.TemplateId, folder.Id, cancellationToken);
        var groups = request.SubInvoices.SelectMany((sub, index) => sub.Items.GroupBy(x => x.ProcessType)
            .Select(g => (Index: index, Items: g.ToList()))).ToList();
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var numbers = await _sequences.ReservePartnerSequenceNumbersAsync(folder.Id, groups.Count);
            var files = new List<(string Name, byte[] Content)>();
            var orders = new List<ShipmentOrder>();
            for (var i = 0; i < groups.Count; i++)
            {
            var invoiceNo = PartnerDocumentPatternFormatter.InvoiceNo(folder.InvoiceNoPattern, numbers[i]);
                if (await _db.ShipmentOrders.AnyAsync(o => o.InvoiceNo == invoiceNo, cancellationToken))
                    throw new DispatchBusinessException("INVOICE_NUMBER_CONFLICT", $"Số hóa đơn {invoiceNo} đã tồn tại.");
                var dto = BuildBaseRequest(folder, request.TemplateId, request.InvoiceDate, request.PoSuffix, invoiceNo, groups[i].Items.Select(CloneItem).ToList());
                await ApplyMasterDataAsync(dto, cancellationToken);
                files.Add((PartnerDocumentPatternFormatter.FileName(folder.FileNamePattern, numbers[i]), await _excel.ExportShipmentMultiSheetExcelAsync(dto)));
                var order = CreateOrder(dto, ShipmentSourceRelationType.SplitSource, batch == null ? [] : [batch.Id]);
                _db.ShipmentOrders.Add(order);
                orders.Add(order);
            }
            if (batch != null) batch.Status = WarehouseBatchStatus.ProcessedByXnk;
            AddAudit("SPLIT_EXPORT", isOcr ? sourceIds : [batch!.Id.ToString()], orders, validation.OriginalTotal);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Dispatch Split Export Completed for source {SourceId}, sequences {Sequences}", sourceIds.Count > 0 ? string.Join(",", sourceIds) : batch?.Id.ToString(), numbers);
            return new ExportFileResult(BuildZip(files), "application/zip", $"Bao_Cao_Tach_Hoa_Don_{request.InvoiceDate:yyyyMMdd}_{numbers[0]}_to_{numbers[^1]}.zip");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError("Dispatch Export Failed for split batch {BatchId}", request.SourceBatchId);
            throw;
        }
    }

    private async Task<(List<WarehouseBatch> Batches, MasterDataFolder Folder)> LoadMergeSourcesAsync(MergeShipmentRequestDto request, CancellationToken ct)
    {
        var ids = request.SourceBatchIds.Distinct().ToList();
        if (ids.Count < 2) throw new InvalidOperationException("Phải chọn ít nhất 2 đợt nguồn để gom.");
        var batches = await _db.WarehouseBatches.Include(b => b.Items).Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        if (batches.Count != ids.Count) throw new InvalidOperationException("Một hoặc nhiều đợt nguồn không tồn tại.");
        if (batches.Any(b => b.Status == WarehouseBatchStatus.ProcessedByXnk)) throw new InvalidOperationException("Một hoặc nhiều đợt nguồn đã được xử lý.");
        if (batches.Any(b => b.Items.Count == 0)) throw new InvalidOperationException("Đợt nguồn không có mặt hàng.");
        var folderIds = batches.Select(b => b.ContractFolderId).Distinct().ToList();
        if (folderIds.Count != 1 || !folderIds[0].HasValue) throw new InvalidOperationException("Các đợt nguồn phải thuộc cùng một hợp đồng.");
        var folderId = request.ContractFolderId ?? folderIds[0]!.Value;
        if (folderId != folderIds[0]) throw new InvalidOperationException("Hợp đồng không phù hợp với đợt nguồn.");
        var folder = await _db.MasterDataFolders.SingleAsync(f => f.Id == folderId, ct);
        await ValidateTemplateAsync(request.TemplateId, folderId, ct);
        return (batches, folder);
    }

    private async Task<(List<OcrDispatchSourceDocumentDto> Documents, MasterDataFolder Folder)> LoadOcrMergeSourcesAsync(
        MergeShipmentRequestDto request, CancellationToken ct)
    {
        var documents = request.SourceDocuments
            .Where(d => !string.IsNullOrWhiteSpace(d.DocumentId))
            .GroupBy(d => d.DocumentId, StringComparer.Ordinal)
            .Select(g => g.First()).ToList();
        if (documents.Count < 2) throw new DispatchBusinessException("OCR_DOCUMENT_INVALID", "Phải chọn ít nhất 2 OCR document để gom.");
        if (documents.Any(d => d.Items.Count == 0 || d.Items.Any(i => i.Quantity <= 0 || string.IsNullOrWhiteSpace(NormalizeStyle(i.StyleCode)) || !Enum.IsDefined(i.ProcessType))))
            throw new DispatchBusinessException("OCR_DOCUMENT_INVALID", "Một hoặc nhiều OCR document không có mặt hàng hợp lệ.");
        var folderId = request.ContractFolderId ?? throw new DispatchBusinessException("OCR_CONTRACT_REQUIRED", "Phải chọn hợp đồng trước khi gom OCR document.");
        var folder = await _db.MasterDataFolders.SingleOrDefaultAsync(f => f.Id == folderId, ct)
            ?? throw new DispatchBusinessException("OCR_CONTRACT_REQUIRED", "Hợp đồng không tồn tại.");
        await ValidateTemplateAsync(request.TemplateId, folderId, ct);
        return (documents, folder);
    }

    private static Dictionary<ItemKey, int> NormalizeSourceItems(IEnumerable<CreateShipmentItemDto> items, ValidateSplitResultDto result)
    {
        var original = new Dictionary<ItemKey, int>();
        foreach (var item in items)
        {
            var code = NormalizeStyle(item.StyleCode);
            if (string.IsNullOrWhiteSpace(code) || item.Quantity <= 0 || !Enum.IsDefined(item.ProcessType))
            {
                AddError(result, "SOURCE_ITEM_INVALID", $"Mặt hàng nguồn '{item.StyleCode}' không hợp lệ.", code);
                continue;
            }
            var key = new ItemKey(code, item.ProcessType);
            original[key] = original.GetValueOrDefault(key) + item.Quantity;
        }
        if (original.Count == 0) AddError(result, "SOURCE_DOCUMENT_EMPTY", "OCR document không có mặt hàng hợp lệ.");
        return original;
    }

    private async Task ApplyMasterDataAsync(CreateShipmentRequestDto request, CancellationToken ct)
    {
        var codes = request.Items.Select(i => NormalizeStyle(i.StyleCode)).Distinct().ToList();
        var products = await _db.ProductMasters.AsNoTracking().Where(p => p.FolderId == request.ContractFolderId && codes.Contains(p.StyleCode)).ToListAsync(ct);
        var map = products.ToDictionary(p => p.StyleCode.Trim().ToUpperInvariant());
        var missing = codes.Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw new DispatchBusinessException("PRODUCT_NOT_IN_CONTRACT", $"Các mã không thuộc hợp đồng đã chọn: [{string.Join(", ", missing)}]");
        foreach (var item in request.Items)
        {
            item.StyleCode = NormalizeStyle(item.StyleCode);
            var product = map[item.StyleCode];
            var go = item.ProcessType == ProcessType.GoKhongMay;
            item.Description = product.Description;
            item.Unit = product.Unit;
            item.PairPerCarton = product.PairPerCarton;
            item.UnitPriceCMT = go && product.UnitPriceCMT_Go.GetValueOrDefault() > 0 ? product.UnitPriceCMT_Go : product.UnitPriceCMT;
            item.UnitPriceDAP = go && product.UnitPriceDAP_Go.GetValueOrDefault() > 0 ? product.UnitPriceDAP_Go : product.UnitPriceDAP;
            if (item.PairPerCarton.GetValueOrDefault() <= 0) throw new DispatchBusinessException("INVALID_PACKING", $"Mã {item.StyleCode} chưa có quy cách đôi/thùng hợp lệ.");
            if (item.UnitPriceDAP.GetValueOrDefault() <= 0) throw new DispatchBusinessException("INVALID_UNIT_PRICE", $"Mã {item.StyleCode} chưa có đơn giá DAP hợp lệ.");
            if (item.UnitPriceCMT.GetValueOrDefault() < 0) throw new DispatchBusinessException("INVALID_UNIT_PRICE", $"Mã {item.StyleCode} có đơn giá CMT không hợp lệ.");
            item.UnitPriceCMT ??= 0;
            item.FullItemCode = go ? $"{item.StyleCode}.G {request.PoSuffix}".Trim() : $"{item.StyleCode} {request.PoSuffix}".Trim();
        }
    }

    private async Task ValidateTemplateAsync(int? templateId, int folderId, CancellationToken ct)
    {
        if (!templateId.HasValue) return;
        if (!await _db.CompanyTemplates.AnyAsync(t => t.Id == templateId && (t.FolderId == null || t.FolderId == folderId), ct))
            throw new DispatchBusinessException("TEMPLATE_INVALID", "Template không tồn tại hoặc không thuộc hợp đồng đã chọn.");
    }

    private static CreateShipmentRequestDto BuildBaseRequest(MasterDataFolder folder, int? templateId, DateTime date, string? poSuffix, string? invoiceNo, List<CreateShipmentItemDto> items) => new()
    {
        ContractFolderId = folder.Id, TemplateId = templateId, InvoiceDate = date, InvoiceNo = invoiceNo ?? "PREVIEW",
        PoSuffix = poSuffix?.Trim() ?? folder.PoSuffix ?? string.Empty, ContractNo = folder.ContractNo ?? string.Empty,
        CustomerName = folder.CustomerName ?? folder.Name, Address = folder.DeliveryAddress ?? string.Empty, Items = items
    };

    private static ShipmentOrder CreateOrder(CreateShipmentRequestDto dto, ShipmentSourceRelationType relation, IEnumerable<int> sourceIds)
    {
        var order = new ShipmentOrder { ContractFolderId = dto.ContractFolderId, InvoiceNo = dto.InvoiceNo, InvoiceDate = dto.InvoiceDate,
            PoSuffix = dto.PoSuffix, ContractNo = dto.ContractNo, CustomerName = dto.CustomerName, Address = dto.Address,
            DeliveryTerms = dto.DeliveryTerms, PaymentTerms = dto.PaymentTerms, Status = ShipmentStatus.Exported };
        foreach (var item in dto.Items) order.Items.Add(new ShipmentOrderItem { StyleCode = item.StyleCode, FullItemCode = item.FullItemCode!,
            Description = item.Description!, Unit = item.Unit, PairPerCarton = item.PairPerCarton!.Value, Quantity = item.Quantity,
            ProcessType = item.ProcessType, SizeBreakdownJson = item.SizeBreakdownJson, UnitPriceCMT = item.UnitPriceCMT!.Value, UnitPriceDAP = item.UnitPriceDAP!.Value });
        foreach (var id in sourceIds) order.SourceBatches.Add(new ShipmentSourceBatch { SourceBatchId = id, RelationType = relation });
        return order;
    }

    private void AddAudit(string action, IEnumerable<string> sources, IEnumerable<ShipmentOrder> orders, int quantity)
    {
        var context = _http.HttpContext;
        _db.BusinessAuditLogs.Add(new BusinessAuditLog { ActorUserId = context?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            ActorUserName = context?.User.Identity?.Name ?? "System", ActorRole = context?.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "System",
            Action = action, ResourceType = "ShipmentDispatch", ResourceId = string.Join(",", sources), TraceId = context?.TraceIdentifier,
            NewStateJson = JsonSerializer.Serialize(new { SourceIds = sources, InvoiceNos = orders.Select(o => o.InvoiceNo), Quantity = quantity }) });
    }

    private static List<DispatchValidationMessageDto> ReconciliationErrors(IEnumerable<OcrDispatchSourceDocumentDto> documents) => documents
        .Where(d => d.ReportedTotal.HasValue && d.ReportedTotal.Value != d.CalculatedTotal)
        .Select(d => new DispatchValidationMessageDto
        {
            Code = "OCR_TOTAL_MISMATCH",
            Message = $"{d.Title}: tổng trên phiếu {d.ReportedTotal:N0} đôi, tổng các dòng OCR {d.CalculatedTotal:N0} đôi, chênh lệch {d.CalculatedTotal - d.ReportedTotal:N0} đôi."
        }).ToList();

    private static byte[] BuildZip(IEnumerable<(string Name, byte[] Content)> files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var file in files) { var entry = archive.CreateEntry(file.Name, CompressionLevel.Fastest); using var target = entry.Open(); target.Write(file.Content); }
        return stream.ToArray();
    }

    private static CreateShipmentItemDto CloneItem(CreateShipmentItemDto i) => new() { StyleCode = i.StyleCode, Quantity = i.Quantity,
        ProcessType = i.ProcessType, SizeBreakdownJson = i.SizeBreakdownJson };
    private static string NormalizeStyle(string? value) { var code = (value ?? string.Empty).Trim().ToUpperInvariant(); return code.EndsWith(".G") ? code[..^2].Trim() : code; }
    private static DispatchValidationMessageDto OddCartonWarning(CreateShipmentItemDto i) => new() { Code = "ODD_CARTON_WARNING", StyleCode = NormalizeStyle(i.StyleCode), Message = $"Số lượng {i.Quantity:N0} đôi sẽ phát sinh thùng lẻ." };
    private static void AddError(ValidateSplitResultDto r, string code, string message, string? style = null) => r.Errors.Add(new() { Code = code, Message = message, StyleCode = style });
    private ValidateSplitResultDto Finish(ValidateSplitResultDto result) { result.IsValid = result.Errors.Count == 0; if (!result.IsValid) _logger.LogWarning("Dispatch Split Validation Failed: {Codes}", result.Errors.Select(e => e.Code)); return result; }
    private readonly record struct ItemKey(string StyleCode, ProcessType ProcessType);
}
