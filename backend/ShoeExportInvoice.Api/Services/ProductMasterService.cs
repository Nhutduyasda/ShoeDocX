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

    public async Task<PagedResultDto<ProductMasterDto>> GetPagedAsync(string? search, int page = 1, int pageSize = 10, int? folderId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var query = _context.ProductMasters
            .AsNoTracking()
            .Include(p => p.Folder)
            .AsQueryable();

        if (folderId.HasValue)
        {
            var folderIds = await GetAllDescendantFolderIdsAsync(folderId.Value);
            folderIds.Add(folderId.Value);
            query = query.Where(p => p.FolderId.HasValue && folderIds.Contains(p.FolderId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim().ToLower();
            query = query.Where(p => p.StyleCode.ToLower().Contains(trimmed) ||
                                     p.Description.ToLower().Contains(trimmed) ||
                                     p.HsCode.ToLower().Contains(trimmed));
        }

        var totalCount = await query.CountAsync();

        decimal avgCmt = 0m;
        decimal avgDap = 0m;
        if (totalCount > 0)
        {
            avgCmt = (decimal)(await query.Where(x => x.UnitPriceCMT > 0).Select(x => (double?)x.UnitPriceCMT).AverageAsync() ?? 0d);
            avgDap = (decimal)(await query.Where(x => x.UnitPriceDAP > 0).Select(x => (double?)x.UnitPriceDAP).AverageAsync() ?? 0d);
        }

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
            PageSize = pageSize,
            AvgUnitPriceCMT = avgCmt,
            AvgUnitPriceDAP = avgDap
        };
    }

    public async Task<IEnumerable<ProductMasterDto>> GetAllAsync()
    {
        return await _context.ProductMasters
            .AsNoTracking()
            .Include(p => p.Folder)
            .OrderBy(p => p.StyleCode)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductMasterDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ProductMasters
            .AsNoTracking()
            .Include(p => p.Folder)
            .FirstOrDefaultAsync(p => p.Id == id);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<ProductMasterDto> CreateAsync(CreateProductMasterDto dto)
    {
        var cleanStyleCode = dto.StyleCode.Trim().ToUpper();

        if (await _context.ProductMasters.AnyAsync(p => p.StyleCode == cleanStyleCode && p.FolderId == dto.FolderId))
        {
            throw new InvalidOperationException($"Mã hình thể '{cleanStyleCode}' đã tồn tại trong hệ thống.");
        }

        int pairPerCarton = dto.PairPerCarton;
        if (pairPerCarton <= 0)
        {
            if (dto.FolderId.HasValue)
            {
                var folder = await _context.MasterDataFolders.FindAsync(dto.FolderId.Value);
                pairPerCarton = folder?.DefaultPairsPerCarton ?? 12;
            }
            else
            {
                pairPerCarton = 12;
            }
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
            PairPerCarton = pairPerCarton,
            FolderId = dto.FolderId,
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

        if (await _context.ProductMasters.AnyAsync(p => p.Id != id && p.StyleCode == cleanStyleCode && p.FolderId == (dto.FolderId ?? entity.FolderId)))
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
        if (dto.FolderId.HasValue)
        {
            entity.FolderId = dto.FolderId;
        }
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

    public async Task<int> DeleteAllAsync(int? folderId = null)
    {
        if (folderId.HasValue)
        {
            var folderIds = await GetAllDescendantFolderIdsAsync(folderId.Value);
            folderIds.Add(folderId.Value);

            var count = await _context.ProductMasters
                .Where(p => p.FolderId.HasValue && folderIds.Contains(p.FolderId.Value))
                .ExecuteDeleteAsync();

            return count;
        }

        var totalCount = await _context.ProductMasters.ExecuteDeleteAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'ProductMasters';");
        }
        catch
        {
            // Bỏ qua nếu SQLite không có bảng sqlite_sequence
        }
        return totalCount;
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

    public async Task<int> BulkMoveProductsAsync(List<int> productIds, int? targetFolderId)
    {
        if (productIds == null || productIds.Count == 0) return 0;

        var products = await _context.ProductMasters
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var p in products)
        {
            p.FolderId = targetFolderId;
            p.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return products.Count;
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

    private async Task<List<int>> GetAllDescendantFolderIdsAsync(int folderId)
    {
        var result = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(folderId);

        var allChildPairs = await _context.MasterDataFolders
            .AsNoTracking()
            .Where(f => f.ParentId != null)
            .Select(f => new { f.Id, ParentId = f.ParentId!.Value })
            .ToListAsync();

        var lookup = allChildPairs.ToLookup(x => x.ParentId, x => x.Id);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var childId in lookup[current])
            {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }

    public async Task<ValidateItemsResult> ValidateItemsAsync(ValidateItemsRequest request)
    {
        var result = new ValidateItemsResult();
        if (request?.StyleCodes == null || request.StyleCodes.Count == 0)
        {
            return result;
        }

        var nonNullCodes = request.StyleCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToList();

        if (nonNullCodes.Count == 0)
        {
            return result;
        }

        result.TotalCodes = nonNullCodes.Count;

        // 1. Lấy toàn bộ cây thư mục để tra cứu đối tác gốc (Root Partner Folder)
        var allFolders = await _context.MasterDataFolders.AsNoTracking().ToListAsync();
        var folderMap = allFolders.ToDictionary(f => f.Id);

        MasterDataFolder? GetRootPartnerFolder(int? folderId)
        {
            if (!folderId.HasValue || !folderMap.TryGetValue(folderId.Value, out var current))
                return null;

            return current;
        }

        // 2. Xác định danh sách folderId của đối tác hiện tại (bao gồm các thư mục con)
        var currentFolderIds = new HashSet<int>();
        if (request.CurrentPartnerFolderId > 0)
        {
            currentFolderIds.Add(request.CurrentPartnerFolderId);

        }

        // 3. Lấy toàn bộ sản phẩm Master Data để kiểm tra chéo
        var allProducts = await _context.ProductMasters
            .AsNoTracking()
            .Include(p => p.Folder)
            .ToListAsync();

        // 4. Kiểm tra từng mã trong danh sách
        var details = new List<ItemValidationDetail>();
        foreach (var raw in nonNullCodes)
        {
            var cleaned = CleanStyleCode(raw);
            var baseCode = cleaned.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? cleaned[..^2].Trim() : cleaned;

            var matching = allProducts.Where(p =>
                p.StyleCode.Equals(cleaned, StringComparison.OrdinalIgnoreCase) ||
                p.StyleCode.Equals(baseCode, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var currentMatch = matching.FirstOrDefault(p => p.FolderId.HasValue && currentFolderIds.Contains(p.FolderId.Value));

            if (currentMatch != null)
            {
                details.Add(new ItemValidationDetail
                {
                    RawCode = raw,
                    NormalizedCode = cleaned,
                    IsMatchedInCurrent = true,
                    MatchedFolderId = currentMatch.FolderId,
                    MatchedFolderName = currentMatch.Folder?.Name,
                    MatchedProduct = MapToDto(currentMatch)
                });
            }
            else
            {
                var otherMatches = matching.Where(p => p.FolderId.HasValue && !currentFolderIds.Contains(p.FolderId.Value)).ToList();
                var otherMatch = otherMatches.Count == 1 ? otherMatches[0] : null;
                var otherRoot = otherMatch != null ? GetRootPartnerFolder(otherMatch.FolderId) : null;

                details.Add(new ItemValidationDetail
                {
                    RawCode = raw,
                    NormalizedCode = cleaned,
                    IsMatchedInCurrent = false,
                    MatchedFolderId = otherMatch?.FolderId,
                    MatchedFolderName = otherRoot?.Name ?? otherMatch?.Folder?.Name,
                    MatchedProduct = otherMatch != null ? MapToDto(otherMatch) : null
                });
            }
        }

        result.Details = details;

        // 5. Thuật toán gợi ý Heuristic: Nếu có mã không khớp đối tác hiện tại,
        // kiểm tra xem có đối tác nào khác chiếm ưu thế (>= 50% số mã không khớp)
        var unmatchedCodes = details.Where(d => !d.IsMatchedInCurrent).ToList();
        if (unmatchedCodes.Count > 0)
        {
            var otherPartnerCounts = new Dictionary<int, (MasterDataFolder Partner, int Count)>();

            foreach (var detail in unmatchedCodes)
            {
                var baseCode = detail.NormalizedCode.EndsWith(".G", StringComparison.OrdinalIgnoreCase)
                    ? detail.NormalizedCode[..^2].Trim()
                    : detail.NormalizedCode;

                var matchingForCode = allProducts.Where(p =>
                    p.StyleCode.Equals(detail.NormalizedCode, StringComparison.OrdinalIgnoreCase) ||
                    p.StyleCode.Equals(baseCode, StringComparison.OrdinalIgnoreCase)
                ).Where(p => p.FolderId.HasValue && !currentFolderIds.Contains(p.FolderId.Value)).ToList();

                var candidatePartnerRoots = matchingForCode
                    .Select(p => GetRootPartnerFolder(p.FolderId))
                    .Where(r => r != null)
                    .Select(r => r!)
                    .GroupBy(r => r.Id)
                    .Select(g => g.First())
                    .ToList();

                foreach (var partnerRoot in candidatePartnerRoots)
                {
                    if (!otherPartnerCounts.ContainsKey(partnerRoot.Id))
                    {
                        otherPartnerCounts[partnerRoot.Id] = (partnerRoot, 1);
                    }
                    else
                    {
                        var cur = otherPartnerCounts[partnerRoot.Id];
                        otherPartnerCounts[partnerRoot.Id] = (cur.Partner, cur.Count + 1);
                    }
                }
            }

            if (otherPartnerCounts.Count > 0)
            {
                var best = otherPartnerCounts.Values.OrderByDescending(x => x.Count).First();
                if (best.Count > 0 && otherPartnerCounts.Values.Count(x => x.Count == best.Count) == 1 && ((double)best.Count / unmatchedCodes.Count) >= 0.5)
                {
                    result.HasMismatch = true;
                    result.SuggestedPartnerFolderId = best.Partner.Id;
                    result.SuggestedPartnerName = best.Partner.Name;
                    result.MatchedCountInSuggested = best.Count;

                    // Cập nhật MatchedProduct cho các dòng chưa khớp bằng sản phẩm từ đối tác gợi ý
                    var suggestedFolderIds = new List<int> { best.Partner.Id };

                    foreach (var detail in details.Where(d => !d.IsMatchedInCurrent))
                    {
                        var baseCode = detail.NormalizedCode.EndsWith(".G", StringComparison.OrdinalIgnoreCase)
                            ? detail.NormalizedCode[..^2].Trim()
                            : detail.NormalizedCode;

                        var prodInSuggested = allProducts.FirstOrDefault(p =>
                            p.FolderId.HasValue && suggestedFolderIds.Contains(p.FolderId.Value) &&
                            (p.StyleCode.Equals(detail.NormalizedCode, StringComparison.OrdinalIgnoreCase) ||
                             p.StyleCode.Equals(baseCode, StringComparison.OrdinalIgnoreCase)));

                        if (prodInSuggested != null)
                        {
                            detail.MatchedFolderId = prodInSuggested.FolderId;
                            detail.MatchedFolderName = best.Partner.Name;
                            detail.MatchedProduct = MapToDto(prodInSuggested);
                        }
                    }
                }
            }
        }

        return result;
    }

    public static string CleanStyleCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return string.Empty;
        var trimmed = rawCode.Trim();
        bool hasGo = false;
        if (trimmed.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
        {
            hasGo = true;
            trimmed = trimmed[..^2].Trim();
        }
        var cleaned = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s*\([^\)]*\)", "").Trim();
        if (cleaned.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
        {
            hasGo = true;
            cleaned = cleaned[..^2].Trim();
        }
        if (hasGo)
        {
            cleaned += ".G";
        }
        return cleaned.ToUpperInvariant();
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
        FolderId = p.FolderId,
        FolderName = p.Folder != null ? p.Folder.Name : null,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
