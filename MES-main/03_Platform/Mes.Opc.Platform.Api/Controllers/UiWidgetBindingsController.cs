using Mes.Opc.Platform.Api.Dtos;
using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

[ApiController]
public class UiWidgetBindingsController(OpcDbContext db) : ControllerBase
{
    [HttpGet("api/v1/ui/widgets/{widgetId:guid}/bindings")]
    public async Task<IActionResult> GetByWidget(Guid widgetId, CancellationToken ct)
    {
        var items = await db.UiWidgetBindings.AsNoTracking()
            .Where(b => b.WidgetId == widgetId)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("api/v1/ui/widgets/{widgetId:guid}/bindings")]
    public async Task<IActionResult> Create(Guid widgetId, [FromBody] UiWidgetBindingCreateDto dto, CancellationToken ct)
    {
        var widgetExists = await db.UiWidgets.AsNoTracking().AnyAsync(w => w.Id == widgetId, ct);
        if (!widgetExists) return BadRequest(new { error = "WidgetId introuvable." });

        var entity = new UiWidgetBinding
        {
            Id = Guid.NewGuid(),
            WidgetId = widgetId,
            MachineCode = dto.MachineCode.Trim(),
            OpcNodeId = dto.OpcNodeId.Trim(),
            BindingRole = dto.BindingRole.Trim()
        };

        db.UiWidgetBindings.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("api/v1/ui/bindings/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UiWidgetBindingUpdateDto dto, CancellationToken ct)
    {
        var entity = await db.UiWidgetBindings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (entity is null) return NotFound();

        entity.MachineCode = dto.MachineCode.Trim();
        entity.OpcNodeId = dto.OpcNodeId.Trim();
        entity.BindingRole = dto.BindingRole.Trim();

        await db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("api/v1/ui/bindings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await db.UiWidgetBindings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (entity is null) return NotFound();

        db.UiWidgetBindings.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
