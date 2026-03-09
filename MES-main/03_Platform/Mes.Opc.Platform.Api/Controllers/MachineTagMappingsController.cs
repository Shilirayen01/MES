using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
public class MachineTagMappingsController : ControllerBase
{
    private readonly OpcDbContext _db;

    public MachineTagMappingsController(OpcDbContext db) => _db = db;

    // LIST mappings par machine
    [HttpGet("api/v1/machines/{machineCode}/tag-mappings")]
    public async Task<IActionResult> GetByMachine(string machineCode, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var machineExists = await _db.Machines.AsNoTracking().AnyAsync(x => x.MachineCode == machineCode, ct);
        if (!machineExists) return NotFound(new { error = $"Machine '{machineCode}' introuvable." });

        var q = _db.MachineTagMappings.AsNoTracking().Where(x => x.MachineCode == machineCode);
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);

        var items = await q.OrderBy(x => x.Id).ToListAsync(ct);
        return Ok(items);
    }

    // CREATE mapping pour machine
    [HttpPost("api/v1/machines/{machineCode}/tag-mappings")]
    public async Task<IActionResult> CreateForMachine(string machineCode, [FromBody] MachineTagMappingCreateDto dto, CancellationToken ct)
    {
        var machineExists = await _db.Machines.AsNoTracking().AnyAsync(x => x.MachineCode == machineCode, ct);
        if (!machineExists)
            return BadRequest(new { error = $"Machine '{machineCode}' n'existe pas." });

        var nodeId = dto.OpcNodeId?.Trim();
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(new { error = "OpcNodeId ne doit pas être vide." });

        if (nodeId.Length > 200)
            return BadRequest(new { error = "OpcNodeId dépasse 200 caractères." });

        var entity = new MachineTagMapping
        {
            MachineCode = machineCode,
            OpcNodeId = nodeId,
            CharacteristicCode = dto.CharacteristicCode.Trim(),
            DataTypeExpected = dto.DataTypeExpected.Trim(),
            Unit = dto.Unit,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            SamplingMs = dto.SamplingMs,
            PublishingMs = dto.PublishingMs,
            IsActive = dto.IsActive
        };

        _db.MachineTagMappings.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    // GET by id
    [HttpGet("api/v1/tag-mappings/{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _db.MachineTagMappings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    // UPDATE by id
    [HttpPut("api/v1/tag-mappings/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MachineTagMappingUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.MachineTagMappings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var nodeId = dto.OpcNodeId?.Trim();
        if (string.IsNullOrWhiteSpace(nodeId))
            return BadRequest(new { error = "OpcNodeId ne doit pas être vide." });
        if (nodeId.Length > 200)
            return BadRequest(new { error = "OpcNodeId dépasse 200 caractères." });

        // Validation FK : machine doit exister
        var machineExists = await _db.Machines.AsNoTracking().AnyAsync(x => x.MachineCode == entity.MachineCode, ct);
        if (!machineExists)
            return BadRequest(new { error = $"Machine '{entity.MachineCode}' n'existe pas." });

        entity.OpcNodeId = nodeId;
        entity.CharacteristicCode = dto.CharacteristicCode.Trim();
        entity.DataTypeExpected = dto.DataTypeExpected.Trim();
        entity.Unit = dto.Unit;
        entity.MinValue = dto.MinValue;
        entity.MaxValue = dto.MaxValue;
        entity.SamplingMs = dto.SamplingMs;
        entity.PublishingMs = dto.PublishingMs;
        entity.IsActive = dto.IsActive;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    // Soft delete
    [HttpDelete("api/v1/tag-mappings/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.MachineTagMappings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
