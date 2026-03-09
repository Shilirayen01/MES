using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
[Route("api/v1/machines")]
public class MachinesController : ControllerBase
{
    private readonly OpcDbContext _db;

    public MachinesController(OpcDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, [FromQuery] string? lineCode, [FromQuery] int? opcEndpointId, CancellationToken ct)
    {
        var q = _db.Machines.AsNoTracking();

        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(lineCode)) q = q.Where(x => x.LineCode == lineCode);
        if (opcEndpointId.HasValue) q = q.Where(x => x.OpcEndpointId == opcEndpointId.Value);

        var items = await q.OrderBy(x => x.MachineCode).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{machineCode}")]
    public async Task<IActionResult> GetByCode(string machineCode, CancellationToken ct)
    {
        var item = await _db.Machines.AsNoTracking().FirstOrDefaultAsync(x => x.MachineCode == machineCode, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MachineCreateDto dto, CancellationToken ct)
    {
        var code = dto.MachineCode.Trim();

        // Validation FK : OpcEndpoint doit exister
        var endpointExists = await _db.OpcEndpoints.AsNoTracking().AnyAsync(x => x.Id == dto.OpcEndpointId, ct);
        if (!endpointExists)
            return BadRequest(new { error = $"OpcEndpointId={dto.OpcEndpointId} n'existe pas." });

        // Unicité PK (MachineCode)
        var exists = await _db.Machines.AnyAsync(x => x.MachineCode == code, ct);
        if (exists)
            return Conflict(new { error = $"MachineCode '{code}' existe déjà." });

        var entity = new Machine
        {
            MachineCode = code,
            Name = dto.Name.Trim(),
            LineCode = dto.LineCode,
            OpcEndpointId = dto.OpcEndpointId,
            IsActive = dto.IsActive
        };

        _db.Machines.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByCode), new { machineCode = entity.MachineCode }, entity);
    }

    [HttpPut("{machineCode}")]
    public async Task<IActionResult> Update(string machineCode, [FromBody] MachineUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.Machines.FirstOrDefaultAsync(x => x.MachineCode == machineCode, ct);
        if (entity is null) return NotFound();

        var endpointExists = await _db.OpcEndpoints.AsNoTracking().AnyAsync(x => x.Id == dto.OpcEndpointId, ct);
        if (!endpointExists)
            return BadRequest(new { error = $"OpcEndpointId={dto.OpcEndpointId} n'existe pas." });

        entity.Name = dto.Name.Trim();
        entity.LineCode = dto.LineCode;
        entity.OpcEndpointId = dto.OpcEndpointId;
        entity.IsActive = dto.IsActive;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    // Soft delete recommandé
    [HttpDelete("{machineCode}")]
    public async Task<IActionResult> Delete(string machineCode, CancellationToken ct)
    {
        var entity = await _db.Machines.FirstOrDefaultAsync(x => x.MachineCode == machineCode, ct);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
