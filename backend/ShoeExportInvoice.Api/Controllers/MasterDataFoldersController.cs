using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/master-data-folders")]
[Authorize]
public class MasterDataFoldersController : ControllerBase
{
    private readonly IMasterDataFolderService _folderService;
    private readonly ILogger<MasterDataFoldersController> _logger;

    public MasterDataFoldersController(
        IMasterDataFolderService folderService,
        ILogger<MasterDataFoldersController> logger)
    {
        _folderService = folderService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy toàn bộ cây thư mục Master Data kèm số lượng sản phẩm
    /// </summary>
    [HttpGet("tree")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<List<MasterDataFolderDto>>> GetTree()
    {
        var tree = await _folderService.GetTreeAsync();
        return Ok(tree);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một thư mục theo ID
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<MasterDataFolderDto>> GetById(int id)
    {
        var folder = await _folderService.GetByIdAsync(id);
        if (folder == null)
        {
            return NotFound(new { message = $"Không tìm thấy thư mục có ID = {id}" });
        }
        return Ok(folder);
    }

    /// <summary>
    /// Tạo mới một thư mục Master Data
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<MasterDataFolderDto>> Create([FromBody] CreateFolderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _folderService.CreateFolderAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo mới thư mục");
            return StatusCode(500, new { message = "Lỗi hệ thống khi tạo thư mục: " + ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật thông tin thư mục (Tên, đối tác, hợp đồng, quy cách đóng gói, ĐVT mặc định)
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<MasterDataFolderDto>> Update(int id, [FromBody] UpdateFolderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _folderService.UpdateFolderAsync(id, dto);
            if (updated == null)
            {
                return NotFound(new { message = $"Không tìm thấy thư mục có ID = {id}" });
            }
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật thư mục ID={Id}", id);
            return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật thư mục: " + ex.Message });
        }
    }

    /// <summary>
    /// Di chuyển thư mục sang thư mục cha mới hoặc thay đổi thứ tự
    /// </summary>
    [HttpPut("{id:int}/move")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveFolderDto dto)
    {
        try
        {
            var moved = await _folderService.MoveFolderAsync(id, dto);
            if (!moved)
            {
                return NotFound(new { message = $"Không tìm thấy thư mục có ID = {id}" });
            }
            return Ok(new { success = true, message = "Di chuyển thư mục thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi di chuyển thư mục ID={Id}", id);
            return StatusCode(500, new { message = "Lỗi hệ thống khi di chuyển thư mục: " + ex.Message });
        }
    }

    /// <summary>
    /// Xóa thư mục (tùy chọn cascade: xóa cả cây con và sản phẩm, hoặc giải phóng sản phẩm về null)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool cascade = false)
    {
        try
        {
            var success = await _folderService.DeleteFolderAsync(id, cascade);
            if (!success)
            {
                return NotFound(new { message = $"Không tìm thấy thư mục có ID = {id}" });
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xóa thư mục ID={Id}", id);
            return StatusCode(500, new { message = "Lỗi hệ thống khi xóa thư mục: " + ex.Message });
        }
    }

    /// <summary>
    /// Di chuyển danh sách sản phẩm sang thư mục đích
    /// </summary>
    [HttpPost("bulk-move-products")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> BulkMoveProducts([FromBody] BulkMoveProductsDto dto)
    {
        try
        {
            var movedCount = await _folderService.BulkMoveProductsAsync(dto);
            return Ok(new { success = true, movedCount, message = $"Đã chuyển {movedCount} sản phẩm sang thư mục mới." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi di chuyển danh sách sản phẩm sang thư mục");
            return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
        }
    }
}
