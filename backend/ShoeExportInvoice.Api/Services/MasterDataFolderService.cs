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
            CreatedAt = f.CreatedAt,
            ProductCount = count,
            TotalProductCount = count
        };
    }

    public async Task<MasterDataFolderDto> CreateFolderAsync(CreateFolderDto dto)
    {
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
            CreatedAt = folder.CreatedAt,
            ProductCount = 0,
            TotalProductCount = 0
        };
    }

    public async Task<MasterDataFolderDto?> UpdateFolderAsync(int id, UpdateFolderDto dto)
    {
        var folder = await _context.MasterDataFolders.FindAsync(id);
        if (folder == null) return null;

        // Tránh vòng lặp: không cho phép ParentId = chính nó
        if (dto.ParentId.HasValue && dto.ParentId.Value == id)
        {
            throw new InvalidOperationException("Thư mục cha không thể là chính nó.");
        }

        folder.Name = dto.Name.Trim();
        folder.ParentId = dto.ParentId;
        folder.CustomerName = dto.CustomerName?.Trim();
        folder.DeliveryAddress = dto.DeliveryAddress?.Trim();
        folder.ContractNo = dto.ContractNo?.Trim();
        folder.PoSuffix = dto.PoSuffix?.Trim();
        folder.DefaultPairsPerCarton = dto.DefaultPairsPerCarton > 0 ? dto.DefaultPairsPerCarton : 12;
        folder.DefaultUnit = !string.IsNullOrWhiteSpace(dto.DefaultUnit) ? dto.DefaultUnit.Trim() : "đôi";
        folder.DisplayOrder = dto.DisplayOrder;
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cập nhật thư mục Master Data: {Id} - {Name}", folder.Id, folder.Name);

        return await GetByIdAsync(id);
    }

    public async Task<bool> MoveFolderAsync(int id, MoveFolderDto dto)
    {
        var folder = await _context.MasterDataFolders.FindAsync(id);
        if (folder == null) return false;

        if (dto.TargetParentId.HasValue && dto.TargetParentId.Value == id)
        {
            throw new InvalidOperationException("Không thể chuyển thư mục vào chính nó.");
        }

        // Kiểm tra xem targetParent có phải là con cháu của folder hiện tại không
        if (dto.TargetParentId.HasValue)
        {
            var curParent = await _context.MasterDataFolders.FindAsync(dto.TargetParentId.Value);
            while (curParent != null)
            {
                if (curParent.ParentId == id)
                {
                    throw new InvalidOperationException("Không thể chuyển thư mục vào thư mục con của nó.");
                }
                curParent = curParent.ParentId.HasValue ? await _context.MasterDataFolders.FindAsync(curParent.ParentId.Value) : null;
            }
        }

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
}
