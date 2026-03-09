using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
public class UiZonesController(OpcDbContext db) : ControllerBase
{
    [HttpGet("api/v1/ui/dashboards/{dashboardId:guid}/zones")]
    public async Task<IActionResult> GetByDashboard(Guid dashboardId, CancellationToken ct)
    {
        var items = await db.UiZones.AsNoTracking()
            .Where(z => z.DashboardId == dashboardId)
            .OrderBy(z => z.OrderIndex)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("api/v1/ui/dashboards/{dashboardId:guid}/zones")]
    public async Task<IActionResult> Create(Guid dashboardId, [FromBody] UiZoneCreateDto dto, CancellationToken ct)
    {
        var dashExists = await db.UiDashboards.AsNoTracking().AnyAsync(d => d.Id == dashboardId, ct);
        if (!dashExists) return BadRequest(new { error = "DashboardId introuvable." });

        var entity = new UiZone
        {
            Id = Guid.NewGuid(),
            DashboardId = dashboardId,
            Title = dto.Title.Trim(),
            LayoutType = dto.LayoutType.Trim(),
            PropsJson = dto.PropsJson,
            OrderIndex = dto.OrderIndex,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.UiZones.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("api/v1/ui/zones/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UiZoneUpdateDto dto, CancellationToken ct)
    {
        var entity = await db.UiZones.FirstOrDefaultAsync(z => z.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Title = dto.Title.Trim();
        entity.LayoutType = dto.LayoutType.Trim();
        entity.PropsJson = dto.PropsJson;
        entity.OrderIndex = dto.OrderIndex;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("api/v1/ui/zones/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var widgets = await db.UiWidgets.Where(w => w.ZoneId == id).Select(w => w.Id).ToListAsync(ct);

        db.UiWidgetBindings.RemoveRange(db.UiWidgetBindings.Where(b => widgets.Contains(b.WidgetId)));
        db.UiWidgets.RemoveRange(db.UiWidgets.Where(w => w.ZoneId == id));

        var zone = await db.UiZones.FirstOrDefaultAsync(z => z.Id == id, ct);
        if (zone is null) return NotFound();

        db.UiZones.Remove(zone);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
