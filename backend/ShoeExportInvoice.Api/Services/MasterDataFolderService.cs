using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class MasterDataFolderService : IMasterDataFolderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MasterDataFolderService> _logger;

    public MasterDataFolderService(AppDbContext context, ILogger<MasterDataFolderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<MasterDataFolderDto>> GetTreeAsync()
    {
        var allFolders = await _context.MasterDataFolders
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Id)
            .ToListAsync();

        // Đếm số lượng sản phẩm trực tiếp trong từng folder
        var productCounts = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => p.FolderId != null)
            .GroupBy(p => p.FolderId!.Value)
            .Select(g => new { FolderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count);

        // Tạo map các DTO
        var dtoMap = allFolders.ToDictionary(f => f.Id, f => new MasterDataFolderDto
        {
            Id = f.Id,
            Name = f.Name,
            ParentId = f.ParentId,
            CustomerName = f.CustomerName,
            DeliveryAddress = f.DeliveryAddress,
            ContractNo = f.ContractNo,
            PoSuffix = f.PoSuffix,
            DefaultPairsPerCarton = f.DefaultPairsPerCarton > 0 ? f.DefaultPairsPerCarton : 12,
            DefaultUnit = f.DefaultUnit ?? "đôi",
            DisplayOrder = f.DisplayOrder,
            InvoiceNoPattern = f.InvoiceNoPattern,
            FileNamePattern = f.FileNamePattern,
            CurrentSequenceNumber = f.CurrentSequenceNumber,
            CreatedAt = f.CreatedAt,
            ProductCount = productCounts.TryGetValue(f.Id, out var cnt) ? cnt : 0,
            Children = new List<MasterDataFolderDto>()
        });

        // Xây dựng cây phân cấp
        var rootFolders = new List<MasterDataFolderDto>();

        foreach (var folder in allFolders)
        {
            var dto = dtoMap[folder.Id];
            if (folder.ParentId.HasValue && dtoMap.TryGetValue(folder.ParentId.Value, out var parentDto))
            {
                parentDto.Children.Add(dto);
            }
            else
            {
                rootFolders.Add(dto);
            }
        }

        // Tính TotalProductCount đệ quy
        void CalculateTotalCount(MasterDataFolderDto node)
        {
            int sum = node.ProductCount;
            foreach (var child in node.Children)
            {
                CalculateTotalCount(child);
                sum += child.TotalProductCount;
            }
            node.TotalProductCount = sum;
        }

        foreach (var root in rootFolders)
        {
            CalculateTotalCount(root);
        }

        return rootFolders;
    }

    public async Task<MasterDataFolderDto?> GetByIdAsync(int id)
    {
        var f = await _context.MasterDataFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f == null) return null;

        var count = await _context.ProductMasters
            .AsNoTracking()
            .CountAsync(p => p.FolderId == id);

        return new MasterDataFolderDto
        {
            Id = f.Id,
            Name = f.Name,
            ParentId = f.ParentId,
            CustomerName = f.CustomerName,
            DeliveryAddress = f.DeliveryAddress,
            ContractNo = f.ContractNo,
            PoSuffix = f.PoSuffix,
            DefaultPairsPerCarton = f.DefaultPairsPerCarton > 0 ? f.DefaultPairsPerCarton : 12,
            DefaultUnit = f.DefaultUnit ?? "đôi",
            DisplayOrder = f.DisplayOrder,
            InvoiceNoPattern = f.InvoiceNoPattern,
            FileNamePattern = f.FileNamePattern,
            CurrentSequenceNumber = f.CurrentSequenceNumber,
            CreatedAt = f.CreatedAt,
            ProductCount = count,
            TotalProductCount = count
        };
    }

    public async Task<MasterDataFolderDto> CreateFolderAsync(CreateFolderDto dto)
    {
        await EnsureValidParentAsync(null, dto.ParentId);
        var folder = new MasterDataFolder
        {
            Name = dto.Name.Trim(),
            ParentId = dto.ParentId,
            CustomerName = dto.CustomerName?.Trim(),
            DeliveryAddress = dto.DeliveryAddress?.Trim(),
            ContractNo = dto.ContractNo?.Trim(),
            PoSuffix = dto.PoSuffix?.Trim(),
            DefaultPairsPerCarton = dto.DefaultPairsPerCarton > 0 ? dto.DefaultPairsPerCarton : 12,
            DefaultUnit = !string.IsNullOrWhiteSpace(dto.DefaultUnit) ? dto.DefaultUnit.Trim() : "đôi",
            DisplayOrder = dto.DisplayOrder,
            InvoiceNoPattern = dto.InvoiceNoPattern.Trim(),
            FileNamePattern = dto.FileNamePattern.Trim(),
            CurrentSequenceNumber = dto.CurrentSequenceNumber,
            CreatedAt = DateTime.UtcNow
        };

        _context.MasterDataFolders.Add(folder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tạo thư mục Master Data mới: {Id} - {Name} (Default {Ppc} pairs/ctn)",
            folder.Id, folder.Name, folder.DefaultPairsPerCarton);

        return new MasterDataFolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentId = folder.ParentId,
            CustomerName = folder.CustomerName,
            DeliveryAddress = folder.DeliveryAddress,
            ContractNo = folder.ContractNo,
            PoSuffix = folder.PoSuffix,
            DefaultPairsPerCarton = folder.DefaultPairsPerCarton,
            DefaultUnit = folder.DefaultUnit,
            DisplayOrder = folder.DisplayOrder,
            InvoiceNoPattern = folder.InvoiceNoPattern,
            FileNamePattern = folder.FileNamePattern,
            CurrentSequenceNumber = folder.CurrentSequenceNumber,
            CreatedAt = folder.CreatedAt,
            ProductCount = 0,
            TotalProductCount = 0
        };
    }

    public async Task<MasterDataFolderDto?> UpdateFolderAsync(int id, UpdateFolderDto dto)
    {
        var folder = await _context.MasterDataFolders.FindAsync(id);
        if (folder == null) return null;

        await EnsureValidParentAsync(id, dto.ParentId);

        folder.Name = dto.Name.Trim();
        folder.ParentId = dto.ParentId;
        folder.CustomerName = dto.CustomerName?.Trim();
        folder.DeliveryAddress = dto.DeliveryAddress?.Trim();
        folder.ContractNo = dto.ContractNo?.Trim();
        folder.PoSuffix = dto.PoSuffix?.Trim();
        folder.DefaultPairsPerCarton = dto.DefaultPairsPerCarton > 0 ? dto.DefaultPairsPerCarton : 12;
        folder.DefaultUnit = !string.IsNullOrWhiteSpace(dto.DefaultUnit) ? dto.DefaultUnit.Trim() : "đôi";
        folder.DisplayOrder = dto.DisplayOrder;
        folder.InvoiceNoPattern = dto.InvoiceNoPattern.Trim();
        folder.FileNamePattern = dto.FileNamePattern.Trim();
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cập nhật thư mục Master Data: {Id} - {Name}", folder.Id, folder.Name);

        return await GetByIdAsync(id);
    }

    public async Task<bool> MoveFolderAsync(int id, MoveFolderDto dto)
    {
        var folder = await _context.MasterDataFolders.FindAsync(id);
        if (folder == null) return false;

        await EnsureValidParentAsync(id, dto.TargetParentId);

        folder.ParentId = dto.TargetParentId;
        folder.DisplayOrder = dto.DisplayOrder;
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã di chuyển thư mục {Id} sang cha mới {ParentId} với thứ tự {Order}",
            id, dto.TargetParentId, dto.DisplayOrder);

        return true;
    }

    public async Task<bool> DeleteFolderAsync(int id, bool cascadeProducts = false)
    {
        var folder = await _context.MasterDataFolders
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null) return false;

        var allFolders = await _context.MasterDataFolders.AsNoTracking().Select(f => new { f.Id, f.ParentId }).ToListAsync();
        var protectedIds = new HashSet<int> { id };
        var queue = new Queue<int>(); queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            foreach (var childId in allFolders.Where(f => f.ParentId == currentId).Select(f => f.Id))
                if (protectedIds.Add(childId)) queue.Enqueue(childId);
        }
        if (await _context.ShipmentOrders.AnyAsync(o => o.ContractFolderId.HasValue && protectedIds.Contains(o.ContractFolderId.Value)) ||
            await _context.CustomsSettlementPeriods.AnyAsync(p => p.ContractFolderId.HasValue && protectedIds.Contains(p.ContractFolderId.Value)) ||
            await _context.WarehouseBatches.AnyAsync(b => b.ContractFolderId.HasValue && protectedIds.Contains(b.ContractFolderId.Value)))
            throw new InvalidOperationException("Cây thư mục đã có đơn hàng, lô kho hoặc kỳ quyết toán, không thể xóa.");
        if (await _context.ProductMasters.AnyAsync(p => p.FolderId.HasValue && protectedIds.Contains(p.FolderId.Value)))
            throw new InvalidOperationException("Cây thư mục còn Master Data. Hãy di chuyển dữ liệu trước khi xóa.");
        // Chuyển thư mục con lên cấp cha của folder bị xóa
        foreach (var child in folder.Children)
        {
            child.ParentId = folder.ParentId;
        }

        // Xử lý các sản phẩm trong thư mục
        var products = await _context.ProductMasters.Where(p => p.FolderId == id).ToListAsync();
        if (cascadeProducts)
        {
            _context.ProductMasters.RemoveRange(products);
        }
        else
        {
            foreach (var p in products)
            {
                p.FolderId = folder.ParentId; // Chuyển sang thư mục cha hoặc null
            }
        }

        _context.MasterDataFolders.Remove(folder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đã xóa thư mục Master Data #{Id} ({Name})", id, folder.Name);
        return true;
    }

    public async Task<int> BulkMoveProductsAsync(BulkMoveProductsDto dto)
    {
        if (dto.ProductIds == null || dto.ProductIds.Count == 0) return 0;

        var products = await _context.ProductMasters
            .Where(p => dto.ProductIds.Contains(p.Id))
            .ToListAsync();

        foreach (var p in products)
        {
            p.FolderId = dto.TargetFolderId;
            p.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã chuyển {Count} mã sản phẩm sang thư mục #{FolderId}", products.Count, dto.TargetFolderId);
        return products.Count;
    }

    private async Task EnsureValidParentAsync(int? folderId, int? targetParentId)
    {
        if (!targetParentId.HasValue) return;
        if (folderId == targetParentId)
            throw new InvalidOperationException("Thư mục cha không thể là chính nó.");

        var current = await _context.MasterDataFolders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == targetParentId.Value)
            ?? throw new InvalidOperationException("Thư mục cha không tồn tại.");
        var visited = new HashSet<int>();
        while (true)
        {
            if (!visited.Add(current.Id))
                throw new InvalidOperationException("Cây thư mục hiện có chứa chu kỳ và phải được xử lý trước.");
            if (folderId.HasValue && current.Id == folderId.Value)
                throw new InvalidOperationException("Không thể chuyển thư mục vào thư mục con của nó.");
            if (!current.ParentId.HasValue) break;
            current = await _context.MasterDataFolders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == current.ParentId.Value)
                ?? throw new InvalidOperationException("Cây thư mục chứa liên kết cha không hợp lệ.");
        }
    }

    public async Task SetSequenceAsync(int id, int nextNumber)
    {
        if (nextNumber <= 0) throw new ArgumentOutOfRangeException(nameof(nextNumber));
        await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var folder = await _context.MasterDataFolders.FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy thư mục #{id}.");
        if (nextNumber < folder.CurrentSequenceNumber)
            throw new InvalidOperationException("Không thể giảm số thứ tự hóa đơn.");
        folder.CurrentSequenceNumber = nextNumber;
        folder.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
