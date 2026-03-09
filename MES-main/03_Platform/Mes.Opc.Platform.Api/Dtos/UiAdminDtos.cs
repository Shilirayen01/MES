using System.ComponentModel.DataAnnotations;

namespace Mes.Opc.Platform.Api.Dtos;

// ---------------- Dashboards ----------------
public class UiDashboardCreateDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; } = false;
}

public sealed class UiDashboardUpdateDto : UiDashboardCreateDto { }

// ---------------- Zones ----------------
public class UiZoneCreateDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = null!;

    [Required, StringLength(50)]
    public string LayoutType { get; set; } = null!;

    public string? PropsJson { get; set; }

    public int OrderIndex { get; set; } = 0;
}

public sealed class UiZoneUpdateDto : UiZoneCreateDto { }

// ---------------- Widgets ----------------
public class UiWidgetCreateDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = null!;

    [Required, StringLength(50)]
    public string WidgetType { get; set; } = null!;

    public string? PropsJson { get; set; }

    public int OrderIndex { get; set; } = 0;
}

public sealed class UiWidgetUpdateDto : UiWidgetCreateDto { }

// ---------------- Bindings ----------------
public class UiWidgetBindingCreateDto
{
    [Required, StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [Required, StringLength(400)]
    public string OpcNodeId { get; set; } = null!;

    [Required, StringLength(50)]
    public string BindingRole { get; set; } = null!;
}

public sealed class UiWidgetBindingUpdateDto : UiWidgetBindingCreateDto { }
