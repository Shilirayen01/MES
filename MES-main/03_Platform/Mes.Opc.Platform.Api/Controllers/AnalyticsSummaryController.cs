using Mes.Opc.Contracts.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

/// <summary>
/// Read + manage analytics summary definitions and their items.
/// Route: api/v1/analytics/summary
/// </summary>
[ApiController]
[Route("api/v1/analytics/summary")]
public sealed class AnalyticsSummaryController : ControllerBase
{
    private readonly OpcDbContext _db;

    public AnalyticsSummaryController(OpcDbContext db) => _db = db;

    // ── Definitions ──────────────────────────────────────────────────────────

    /// <summary>Lists all summary definitions (optionally filtered by machine).</summary>
    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(
        [FromQuery] string? machineCode,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var q = _db.AnalyticsSummaryDefinitions
            .Include(d => d.Items)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(machineCode))
            q = q.Where(d => d.AppliesToMachineCode == null || d.AppliesToMachineCode == machineCode);

        if (isActive.HasValue)
            q = q.Where(d => d.IsActive == isActive.Value);

        var list = await q.OrderBy(d => d.SummaryId).ToListAsync(ct);
        return Ok(list.Select(ToDto));
    }

    /// <summary>Returns one summary definition with all its items.</summary>
    [HttpGet("definitions/{id:int}")]
    public async Task<IActionResult> GetDefinition(int id, CancellationToken ct)
    {
        var def = await _db.AnalyticsSummaryDefinitions
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SummaryId == id, ct);

        return def is null ? NotFound() : Ok(ToDto(def));
    }

    /// <summary>Creates a new summary definition.</summary>
    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition(
        [FromBody] AnalyticsSummaryDefinitionCreateDto dto,
        CancellationToken ct)
    {
        var entity = new AnalyticsSummaryDefinition
        {
            Name = dto.Name.Trim(),
            IsActive = dto.IsActive,
            AppliesToMachineCode = dto.AppliesToMachineCode
        };

        _db.AnalyticsSummaryDefinitions.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetDefinition), new { id = entity.SummaryId }, ToDto(entity));
    }

    /// <summary>Updates a summary definition.</summary>
    [HttpPut("definitions/{id:int}")]
    public async Task<IActionResult> UpdateDefinition(
        int id,
        [FromBody] AnalyticsSummaryDefinitionCreateDto dto,
        CancellationToken ct)
    {
        var entity = await _db.AnalyticsSummaryDefinitions.FirstOrDefaultAsync(d => d.SummaryId == id, ct);
        if (entity is null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.IsActive = dto.IsActive;
        entity.AppliesToMachineCode = dto.AppliesToMachineCode;

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(entity));
    }

    /// <summary>Deletes a summary definition and all its items (cascade).</summary>
[HttpDelete("definitions/{id:int}")]
public async Task<IActionResult> DeleteDefinition(int id, CancellationToken ct)
{
    // 1. Chercher la définition
    var entity = await _db.AnalyticsSummaryDefinitions
        .FirstOrDefaultAsync(d => d.SummaryId == id, ct);

    if (entity == null) return NotFound();

    // 2. Supprimer TOUS les enfants liés manuellement avant la définition
    // On cible la table enfant directement via le contexte
    var itemsToDelete = await _db.AnalyticsSummaryItems
        .Where(i => i.SummaryId == id)
        .ToListAsync(ct);

    if (itemsToDelete.Any())
    {
        _db.AnalyticsSummaryItems.RemoveRange(itemsToDelete);
    }

    // 3. Supprimer la définition principale
    _db.AnalyticsSummaryDefinitions.Remove(entity);

    // 4. Sauvegarder les changements en une seule transaction
    await _db.SaveChangesAsync(ct);
    
    return NoContent();
}

    // ── Items ────────────────────────────────────────────────────────────────

    /// <summary>Adds an item to a summary definition.</summary>
    [HttpPost("definitions/{defId:int}/items")]
    public async Task<IActionResult> AddItem(int defId, [FromBody] AnalyticsSummaryItemCreateDto dto, CancellationToken ct)
    {
        var defExists = await _db.AnalyticsSummaryDefinitions.AsNoTracking().AnyAsync(d => d.SummaryId == defId, ct);
        if (!defExists) return NotFound(new { error = $"Definition {defId} not found." });

        var entity = new AnalyticsSummaryItem
        {
            SummaryId = defId,
            FieldName = dto.FieldName.Trim(),
            SourceType = dto.SourceType == "Constant" ? (byte)1 : (byte)0,
            TagNodeId = dto.TagNodeId,
            Scope = dto.Scope is null ? null : ParseScopeByte(dto.Scope),
            Aggregation = dto.Aggregation is null ? null : ParseAggByte(dto.Aggregation),
            IsCumulative = dto.IsCumulative,
            ConstantValue = dto.ConstantValue,
            LookbackMinutes = dto.LookbackMinutes,
            MaxGapSeconds = dto.MaxGapSeconds,
            Unit = dto.Unit
        };

        _db.AnalyticsSummaryItems.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Created(string.Empty, ToItemDto(entity));
    }

    /// <summary>Removes an item from a summary definition.</summary>
    [HttpDelete("definitions/{defId:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int defId, int itemId, CancellationToken ct)
    {
        var item = await _db.AnalyticsSummaryItems
            .FirstOrDefaultAsync(i => i.SummaryId == defId && i.ItemId == itemId, ct);

        if (item is null) return NotFound();

        _db.AnalyticsSummaryItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static readonly string[] _scopes = ["Run", "Last", "LookbackWindow"];
    private static readonly string[] _aggs = ["Sum", "Average", "Min", "Max", "Last"];

    private static byte? ParseScopeByte(string? s) =>
        s is null ? null : (byte?)Array.IndexOf(_scopes, s);

    private static byte? ParseAggByte(string? s) =>
        s is null ? null : (byte?)Array.IndexOf(_aggs, s);

    private static AnalyticsSummaryDefinitionDto ToDto(AnalyticsSummaryDefinition e) => new()
    {
        SummaryId = e.SummaryId,
        Name = e.Name,
        IsActive = e.IsActive,
        AppliesToMachineCode = e.AppliesToMachineCode,
        Items = e.Items?.Select(ToItemDto).ToList() ?? new()
    };

    private static AnalyticsSummaryItemDto ToItemDto(AnalyticsSummaryItem i) => new()
    {
        ItemId = i.ItemId,
        SummaryId = i.SummaryId,
        FieldName = i.FieldName,
        SourceType = i.SourceType == 0 ? "Tag" : "Constant",
        TagNodeId = i.TagNodeId,
        Scope = i.Scope.HasValue && i.Scope.Value < _scopes.Length ? _scopes[i.Scope.Value] : null,
        Aggregation = i.Aggregation.HasValue && i.Aggregation.Value < _aggs.Length ? _aggs[i.Aggregation.Value] : null,
        IsCumulative = i.IsCumulative,
        ConstantValue = i.ConstantValue,
        LookbackMinutes = i.LookbackMinutes,
        MaxGapSeconds = i.MaxGapSeconds,
        Unit = i.Unit
    };
}
