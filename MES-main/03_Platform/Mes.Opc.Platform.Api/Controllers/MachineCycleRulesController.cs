using Mes.Opc.Contracts.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

/// <summary>
/// CRUD for machine cycle detection rules (dbo.MachineCycleRule).
/// Route: api/v1/machines/{machineCode}/cycle-rules
/// </summary>
[ApiController]
[Route("api/v1/machines/{machineCode}/cycle-rules")]
public sealed class MachineCycleRulesController : ControllerBase
{
    private readonly OpcDbContext _db;

    public MachineCycleRulesController(OpcDbContext db) => _db = db;

    /// <summary>Returns all cycle rules for a specific machine.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(string machineCode, CancellationToken ct)
    {
        var rules = await _db.MachineCycleRules
            .AsNoTracking()
            .Where(r => r.MachineCode == machineCode)
            .OrderBy(r => r.ScopeKey)
            .ToListAsync(ct);

        return Ok(rules.Select(ToDto));
    }

    /// <summary>Returns a single cycle rule by its Id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(string machineCode, int id, CancellationToken ct)
    {
        var rule = await _db.MachineCycleRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.MachineCode == machineCode, ct);

        return rule is null ? NotFound() : Ok(ToDto(rule));
    }

    /// <summary>Creates a new cycle rule for a machine.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(string machineCode, [FromBody] CycleRuleCreateDto dto, CancellationToken ct)
    {
        var machineExists = await _db.Machines.AsNoTracking().AnyAsync(m => m.MachineCode == machineCode, ct);
        if (!machineExists)
            return BadRequest(new { error = $"Machine '{machineCode}' not found." });

        var conflict = await _db.MachineCycleRules.AnyAsync(
            r => r.MachineCode == machineCode && r.ScopeKey == dto.ScopeKey, ct);
        if (conflict)
            return Conflict(new { error = $"A rule with ScopeKey='{dto.ScopeKey}' already exists for machine '{machineCode}'." });

        var entity = FromCreateDto(dto, machineCode);
        _db.MachineCycleRules.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { machineCode, id = entity.Id }, ToDto(entity));
    }

    /// <summary>Updates an existing cycle rule.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(string machineCode, int id, [FromBody] CycleRuleUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.MachineCycleRules
            .FirstOrDefaultAsync(r => r.Id == id && r.MachineCode == machineCode, ct);

        if (entity is null) return NotFound();

        ApplyUpdate(entity, dto);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(entity));
    }

    /// <summary>Deletes a cycle rule.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string machineCode, int id, CancellationToken ct)
    {
        var entity = await _db.MachineCycleRules
            .FirstOrDefaultAsync(r => r.Id == id && r.MachineCode == machineCode, ct);

        if (entity is null) return NotFound();

        _db.MachineCycleRules.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static CycleRuleDto ToDto(MachineCycleRule e) => new()
    {
        Id = e.Id,
        MachineCode = e.MachineCode,
        ScopeKey = e.ScopeKey,
        IsActive = e.IsActive,
        StartStrategy = e.StartStrategy,
        StartNodeId = e.StartNodeId,
        StartEdgeType = e.StartEdgeType,
        StartValue = e.StartValue,
        EndPrimaryStrategy = e.EndPrimaryStrategy,
        EndPrimaryNodeId = e.EndPrimaryNodeId,
        EndPrimaryEdgeType = e.EndPrimaryEdgeType,
        EndFallbackStrategy = e.EndFallbackStrategy,
        EndFallbackNodeId = e.EndFallbackNodeId,
        AbortNodeIds = e.AbortNodeIds,
        DebounceMs = e.DebounceMs,
        MinCycleSeconds = e.MinCycleSeconds,
        TimeoutSeconds = e.TimeoutSeconds,
        Epsilon = e.Epsilon,
        TargetTolerance = e.TargetTolerance,
        ValidationSpeedNodeId = e.ValidationNodeId_Speed,
        ValidationSpeedMin = e.ValidationSpeedMin,
        ValidationStateNodeId = e.ValidationNodeId_State,
        ValidationStateValue = e.ValidationStateValue,
        RecoveryStrategy = e.RecoveryStrategy,
        RecoveryConfirmNodeId = e.RecoveryConfirmNodeId,
        RecoveryConfirmDelta = e.RecoveryConfirmDelta,
        RecoveryConfirmWindowSeconds = e.RecoveryConfirmWindowSeconds
    };

    private static MachineCycleRule FromCreateDto(CycleRuleCreateDto dto, string machineCode) => new()
    {
        MachineCode = machineCode,
        ScopeKey = dto.ScopeKey,
        IsActive = dto.IsActive,
        StartStrategy = dto.StartStrategy,
        StartNodeId = dto.StartNodeId,
        StartEdgeType = dto.StartEdgeType,
        StartValue = dto.StartValue,
        EndPrimaryStrategy = dto.EndPrimaryStrategy,
        EndPrimaryNodeId = dto.EndPrimaryNodeId,
        EndPrimaryEdgeType = dto.EndPrimaryEdgeType,
        EndFallbackStrategy = dto.EndFallbackStrategy,
        EndFallbackNodeId = dto.EndFallbackNodeId,
        AbortNodeIds = dto.AbortNodeIds,
        DebounceMs = dto.DebounceMs,
        MinCycleSeconds = dto.MinCycleSeconds,
        TimeoutSeconds = dto.TimeoutSeconds,
        Epsilon = dto.Epsilon,
        TargetTolerance = dto.TargetTolerance,
        ValidationNodeId_Speed = dto.ValidationSpeedNodeId,
        ValidationSpeedMin = dto.ValidationSpeedMin,
        ValidationNodeId_State = dto.ValidationStateNodeId,
        ValidationStateValue = dto.ValidationStateValue,
        RecoveryStrategy = dto.RecoveryStrategy,
        RecoveryConfirmNodeId = dto.RecoveryConfirmNodeId,
        RecoveryConfirmDelta = dto.RecoveryConfirmDelta,
        RecoveryConfirmWindowSeconds = dto.RecoveryConfirmWindowSeconds
    };

    private static void ApplyUpdate(MachineCycleRule e, CycleRuleUpdateDto dto)
    {
        e.ScopeKey = dto.ScopeKey;
        e.IsActive = dto.IsActive;
        e.StartStrategy = dto.StartStrategy;
        e.StartNodeId = dto.StartNodeId;
        e.StartEdgeType = dto.StartEdgeType;
        e.StartValue = dto.StartValue;
        e.EndPrimaryStrategy = dto.EndPrimaryStrategy;
        e.EndPrimaryNodeId = dto.EndPrimaryNodeId;
        e.EndPrimaryEdgeType = dto.EndPrimaryEdgeType;
        e.EndFallbackStrategy = dto.EndFallbackStrategy;
        e.EndFallbackNodeId = dto.EndFallbackNodeId;
        e.AbortNodeIds = dto.AbortNodeIds;
        e.DebounceMs = dto.DebounceMs;
        e.MinCycleSeconds = dto.MinCycleSeconds;
        e.TimeoutSeconds = dto.TimeoutSeconds;
        e.Epsilon = dto.Epsilon;
        e.TargetTolerance = dto.TargetTolerance;
        e.ValidationNodeId_Speed = dto.ValidationSpeedNodeId;
        e.ValidationSpeedMin = dto.ValidationSpeedMin;
        e.ValidationNodeId_State = dto.ValidationStateNodeId;
        e.ValidationStateValue = dto.ValidationStateValue;
        e.RecoveryStrategy = dto.RecoveryStrategy;
        e.RecoveryConfirmNodeId = dto.RecoveryConfirmNodeId;
        e.RecoveryConfirmDelta = dto.RecoveryConfirmDelta;
        e.RecoveryConfirmWindowSeconds = dto.RecoveryConfirmWindowSeconds;
    }
}
