using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
[Route("api/v1/opc-endpoints")]
public class OpcEndpointsController : ControllerBase
{
    private readonly OpcDbContext _db;

    public OpcEndpointsController(OpcDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
    {
        var q = _db.OpcEndpoints.AsNoTracking();

        if (isActive.HasValue)
            q = q.Where(x => x.IsActive == isActive.Value);

        var items = await q.OrderBy(x => x.Id).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _db.OpcEndpoints.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OpcEndpointCreateDto dto, CancellationToken ct)
    {
        var entity = new OpcEndpoint
        {
            Name = dto.Name.Trim(),
            EndpointUrl = dto.EndpointUrl.Trim(),
            IsActive = dto.IsActive,
            Description = dto.Description,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now
        };

        _db.OpcEndpoints.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OpcEndpointUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.OpcEndpoints.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.EndpointUrl = dto.EndpointUrl.Trim();
        entity.IsActive = dto.IsActive;
        entity.Description = dto.Description;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    // Soft delete recommandé : IsActive=false
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.OpcEndpoints.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
