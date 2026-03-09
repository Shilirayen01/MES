using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
public class UiWidgetsController(OpcDbContext db) : ControllerBase
{
    [HttpGet("api/v1/ui/zones/{zoneId:guid}/widgets")]
    public async Task<IActionResult> GetByZone(Guid zoneId, CancellationToken ct)
    {
        var items = await db.UiWidgets.AsNoTracking()
            .Where(w => w.ZoneId == zoneId)
            .OrderBy(w => w.OrderIndex)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("api/v1/ui/zones/{zoneId:guid}/widgets")]
    public async Task<IActionResult> Create(Guid zoneId, [FromBody] UiWidgetCreateDto dto, CancellationToken ct)
    {
        var zoneExists = await db.UiZones.AsNoTracking().AnyAsync(z => z.Id == zoneId, ct);
        if (!zoneExists) return BadRequest(new { error = "ZoneId introuvable." });

        var entity = new UiWidget
        {
            Id = Guid.NewGuid(),
            ZoneId = zoneId,
            Title = dto.Title.Trim(),
            WidgetType = dto.WidgetType.Trim(),
            PropsJson = dto.PropsJson,
            OrderIndex = dto.OrderIndex,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.UiWidgets.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("api/v1/ui/widgets/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UiWidgetUpdateDto dto, CancellationToken ct)
    {
        var entity = await db.UiWidgets.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Title = dto.Title.Trim();
        entity.WidgetType = dto.WidgetType.Trim();
        entity.PropsJson = dto.PropsJson;
        entity.OrderIndex = dto.OrderIndex;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("api/v1/ui/widgets/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        db.UiWidgetBindings.RemoveRange(db.UiWidgetBindings.Where(b => b.WidgetId == id));

        var w = await db.UiWidgets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return NotFound();

        db.UiWidgets.Remove(w);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
