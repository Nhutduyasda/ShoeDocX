using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class ProductMasterService : IProductMasterService
{
    private readonly AppDbContext _context;

    public ProductMasterService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<ProductMasterDto>> GetPagedAsync(string? search, int page = 1, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var query = _context.ProductMasters.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim().ToLower();
            query = query.Where(p => p.StyleCode.ToLower().Contains(trimmed) ||
                                     p.Description.ToLower().Contains(trimmed) ||
                                     p.HsCode.ToLower().Contains(trimmed));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return new PagedResultDto<ProductMasterDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ProductMasterDto>> GetAllAsync()
    {
        return await _context.ProductMasters
            .AsNoTracking()
            .OrderBy(p => p.StyleCode)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductMasterDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ProductMasters.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<ProductMasterDto> CreateAsync(CreateProductMasterDto dto)
    {
        var cleanStyleCode = dto.StyleCode.Trim().ToUpper();

        if (await ExistsStyleCodeAsync(cleanStyleCode))
        {
            throw new InvalidOperationException($"Mã hình thể '{cleanStyleCode}' đã tồn tại trong hệ thống.");
        }

        var entity = new ProductMaster
        {
            StyleCode = cleanStyleCode,
            Description = dto.Description.Trim(),
            UnitPriceCMT = dto.UnitPriceCMT,
            UnitPriceDAP = dto.UnitPriceDAP,
            UnitPriceCMT_Go = (dto.UnitPriceCMT_Go.HasValue && dto.UnitPriceCMT_Go.Value > 0) ? dto.UnitPriceCMT_Go : null,
            UnitPriceDAP_Go = (dto.UnitPriceDAP_Go.HasValue && dto.UnitPriceDAP_Go.Value > 0) ? dto.UnitPriceDAP_Go : null,
            HsCode = string.IsNullOrWhiteSpace(dto.HsCode) ? "64041990" : dto.HsCode.Trim(),
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "đôi" : dto.Unit.Trim(),
            PairPerCarton = dto.PairPerCarton <= 0 ? 12 : dto.PairPerCarton,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductMasters.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<ProductMasterDto?> UpdateAsync(int id, UpdateProductMasterDto dto)
    {
        var entity = await _context.ProductMasters.FindAsync(id);
        if (entity == null) return null;

        var cleanStyleCode = dto.StyleCode.Trim().ToUpper();

        if (await ExistsStyleCodeAsync(cleanStyleCode, id))
        {
            throw new InvalidOperationException($"Mã hình thể '{cleanStyleCode}' đã tồn tại ở một sản phẩm khác.");
        }

        entity.StyleCode = cleanStyleCode;
        entity.Description = dto.Description.Trim();
        entity.UnitPriceCMT = dto.UnitPriceCMT;
        entity.UnitPriceDAP = dto.UnitPriceDAP;
        entity.UnitPriceCMT_Go = (dto.UnitPriceCMT_Go.HasValue && dto.UnitPriceCMT_Go.Value > 0) ? dto.UnitPriceCMT_Go : null;
        entity.UnitPriceDAP_Go = (dto.UnitPriceDAP_Go.HasValue && dto.UnitPriceDAP_Go.Value > 0) ? dto.UnitPriceDAP_Go : null;
        entity.HsCode = string.IsNullOrWhiteSpace(dto.HsCode) ? "64041990" : dto.HsCode.Trim();
        entity.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "đôi" : dto.Unit.Trim();
        entity.PairPerCarton = dto.PairPerCarton <= 0 ? 12 : dto.PairPerCarton;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ProductMasters.FindAsync(id);
        if (entity == null) return false;

        _context.ProductMasters.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteAllAsync()
    {
        var count = await _context.ProductMasters.ExecuteDeleteAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'ProductMasters';");
        }
        catch
        {
            // Bỏ qua nếu SQLite không có bảng sqlite_sequence
        }
        return count;
    }

    public async Task<int> BulkUpdateUnitAsync(string newUnit)
    {
        if (string.IsNullOrWhiteSpace(newUnit))
            throw new ArgumentException("Đơn vị tính không được để trống.", nameof(newUnit));

        var trimmed = newUnit.Trim();
        var count = await _context.ProductMasters
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Unit, trimmed)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

        return count;
    }

    public async Task<bool> ExistsStyleCodeAsync(string styleCode, int? excludeId = null)
    {
        var query = _context.ProductMasters.AsNoTracking().Where(p => p.StyleCode.ToLower() == styleCode.Trim().ToLower());
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }

    private static ProductMasterDto MapToDto(ProductMaster p) => new()
    {
        Id = p.Id,
        StyleCode = p.StyleCode,
        Description = p.Description,
        UnitPriceCMT = p.UnitPriceCMT,
        UnitPriceDAP = p.UnitPriceDAP,
        UnitPriceCMT_Go = p.UnitPriceCMT_Go,
        UnitPriceDAP_Go = p.UnitPriceDAP_Go,
        HsCode = p.HsCode,
        Unit = p.Unit,
        PairPerCarton = p.PairPerCarton,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
