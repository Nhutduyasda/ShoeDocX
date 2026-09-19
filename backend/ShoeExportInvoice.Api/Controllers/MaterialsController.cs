using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize(Roles = "Admin,Kho")]
public class MaterialsController : ControllerBase
{
    private readonly AppDbContext _context;
    public MaterialsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaterialDto>>> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _context.Materials.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(m => m.MaterialCode.ToLower().Contains(term) || m.MaterialName.ToLower().Contains(term));
        }
        var materials = await query.OrderBy(m => m.MaterialCode).ToListAsync(cancellationToken);
        return Ok(materials.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> Create(SaveMaterialDto request, CancellationToken cancellationToken)
    {
        var entity = new Material();
        await ApplyAsync(entity, request, null, cancellationToken);
        _context.Materials.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MaterialDto>> Update(int id, SaveMaterialDto request, CancellationToken cancellationToken)
    {
        var entity = await _context.Materials.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy vật tư #{id}.");
        await ApplyAsync(entity, request, id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _context.Materials.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity == null) return NotFound(new { message = $"Không tìm thấy vật tư #{id}." });
        if (await _context.BomItems.AnyAsync(i => i.MaterialId == id, cancellationToken) ||
            await _context.MaterialRequirementPlanItems.AnyAsync(i => i.MaterialId == id, cancellationToken))
            return Conflict(new { message = "Vật tư đang được sử dụng trong BOM hoặc chứng từ kế hoạch và không thể xóa." });
        _context.Materials.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task ApplyAsync(Material entity, SaveMaterialDto request, int? currentId, CancellationToken cancellationToken)
    {
        var code = request.MaterialCode.Trim();
        var duplicateQuery = _context.Materials.Where(m => m.MaterialCode == code);
        if (currentId.HasValue) duplicateQuery = duplicateQuery.Where(m => m.Id != currentId.Value);
        if (await duplicateQuery.AnyAsync(cancellationToken))
            throw new InvalidOperationException($"Mã vật tư '{code}' đã tồn tại.");
        entity.MaterialCode = code;
        entity.MaterialName = request.MaterialName.Trim();
        entity.Unit = request.Unit.Trim();
        entity.MaterialType = request.MaterialType;
        entity.CurrentStock = request.CurrentStock;
    }

    private static MaterialDto Map(Material m) => new()
    {
        Id = m.Id, MaterialCode = m.MaterialCode, MaterialName = m.MaterialName, Unit = m.Unit,
        MaterialType = m.MaterialType, CurrentStock = m.CurrentStock
    };
}
