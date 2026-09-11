using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IProductMasterService
{
    Task<PagedResultDto<ProductMasterDto>> GetPagedAsync(string? search, int page = 1, int pageSize = 10, int? folderId = null);
    Task<IEnumerable<ProductMasterDto>> GetAllAsync();
    Task<ProductMasterDto?> GetByIdAsync(int id);
    Task<ProductMasterDto> CreateAsync(CreateProductMasterDto dto);
    Task<ProductMasterDto?> UpdateAsync(int id, UpdateProductMasterDto dto);
    Task<bool> DeleteAsync(int id);
    Task<int> DeleteAllAsync(int? folderId = null);
    Task<int> BulkUpdateUnitAsync(string newUnit);
    Task<int> BulkMoveProductsAsync(List<int> productIds, int? targetFolderId);
    Task<bool> ExistsStyleCodeAsync(string styleCode, int? excludeId = null);
    Task<ValidateItemsResult> ValidateItemsAsync(ValidateItemsRequest request);
}
