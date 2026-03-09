using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
[Route("api/v1/ui/dashboards")]
public class UiDashboardsController(OpcDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await db.UiDashboards.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await db.UiDashboards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UiDashboardCreateDto dto, CancellationToken ct)
    {
        var entity = new UiDashboard
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            IsDefault = dto.IsDefault,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null
        };

        if (dto.IsDefault)
        {
            var defaults = await db.UiDashboards.Where(x => x.IsDefault).ToListAsync(ct);
            foreach (var d in defaults) d.IsDefault = false;
        }

        db.UiDashboards.Add(entity);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UiDashboardUpdateDto dto, CancellationToken ct)
    {
        var entity = await db.UiDashboards.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.IsDefault && !entity.IsDefault)
        {
            var defaults = await db.UiDashboards.Where(x => x.IsDefault).ToListAsync(ct);
            foreach (var d in defaults) d.IsDefault = false;
            entity.IsDefault = true;
        }
        else if (!dto.IsDefault)
        {
            entity.IsDefault = false;
        }

        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        var entity = await db.UiDashboards.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        var defaults = await db.UiDashboards.Where(x => x.IsDefault).ToListAsync(ct);
        foreach (var d in defaults) d.IsDefault = false;

        entity.IsDefault = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Delete en cascade (zones/widgets/bindings) n'est pas garanti par FK SQL,
        // donc on supprime explicitement.
        var zones = await db.UiZones.Where(z => z.DashboardId == id).Select(z => z.Id).ToListAsync(ct);
        var widgets = await db.UiWidgets.Where(w => zones.Contains(w.ZoneId)).Select(w => w.Id).ToListAsync(ct);

        db.UiWidgetBindings.RemoveRange(db.UiWidgetBindings.Where(b => widgets.Contains(b.WidgetId)));
        db.UiWidgets.RemoveRange(db.UiWidgets.Where(w => zones.Contains(w.ZoneId)));
        db.UiZones.RemoveRange(db.UiZones.Where(z => z.DashboardId == id));

        var dash = await db.UiDashboards.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dash is null) return NotFound();

        db.UiDashboards.Remove(dash);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
