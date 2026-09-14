using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IMasterDataFolderService
{
    Task<List<MasterDataFolderDto>> GetTreeAsync();
    Task<MasterDataFolderDto?> GetByIdAsync(int id);
    Task<MasterDataFolderDto> CreateFolderAsync(CreateFolderDto dto);
    Task<MasterDataFolderDto?> UpdateFolderAsync(int id, UpdateFolderDto dto);
    Task<bool> MoveFolderAsync(int id, MoveFolderDto dto);
    Task<bool> DeleteFolderAsync(int id, bool cascadeProducts = false);
    Task<int> BulkMoveProductsAsync(BulkMoveProductsDto dto);
    Task SetSequenceAsync(int id, int nextNumber);
}
