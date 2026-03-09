using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mes.Opc.Platform.Data.Db;

/// <summary>
/// EF Core entity mapping to dbo.MachineCycleRule.
/// Defines how cycles (start/end) are detected for a given machine.
/// </summary>
[Table("MachineCycleRule")]
public sealed class MachineCycleRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineCode { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ScopeKey { get; set; } = "default";

    public bool IsActive { get; set; } = true;

    // ── Start detection ─────────────────────────────────────────────────────
    [Required, MaxLength(30)]
    public string StartStrategy { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? StartNodeId { get; set; }

    [MaxLength(10)]
    public string? StartEdgeType { get; set; }

    [MaxLength(100)]
    public string? StartValue { get; set; }

    // ── End detection (primary) ─────────────────────────────────────────────
    [Required, MaxLength(30)]
    public string EndPrimaryStrategy { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? EndPrimaryNodeId { get; set; }

    [MaxLength(10)]
    public string? EndPrimaryEdgeType { get; set; }

    // ── End detection (fallback) ────────────────────────────────────────────
    [MaxLength(30)]
    public string? EndFallbackStrategy { get; set; }

    [MaxLength(256)]
    public string? EndFallbackNodeId { get; set; }

    public string? AbortNodeIds { get; set; }

    // ── Timing ──────────────────────────────────────────────────────────────
    public int DebounceMs { get; set; } = 500;
    public int MinCycleSeconds { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 3600;

    // ── Validation ──────────────────────────────────────────────────────────
    [Column(TypeName = "decimal(18,3)")]
    public decimal? Epsilon { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TargetTolerance { get; set; }

    [MaxLength(256)]
    public string? ValidationNodeId_Speed { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ValidationSpeedMin { get; set; }

    [MaxLength(256)]
    public string? ValidationNodeId_State { get; set; }

    [MaxLength(100)]
    public string? ValidationStateValue { get; set; }

    // ── Recovery ────────────────────────────────────────────────────────────
    [MaxLength(30)]
    public string? RecoveryStrategy { get; set; }

    [MaxLength(256)]
    public string? RecoveryConfirmNodeId { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? RecoveryConfirmDelta { get; set; }

    public int? RecoveryConfirmWindowSeconds { get; set; }
}
