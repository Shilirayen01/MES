using System;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents a machine cycle detection rule returned by the API.
    /// Maps to dbo.MachineCycleRule.
    /// </summary>
    public sealed class CycleRuleDto
    {
        public int Id { get; set; }

        /// <summary>Machine this rule belongs to (e.g. "MC_01").</summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>Scope key — allows multiple rules per machine (e.g. "default").</summary>
        public string ScopeKey { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        // ── Start detection ──────────────────────────────────────────────────
        /// <summary>Strategy to detect cycle start (e.g. "EdgeOnBit", "ValueEquals").</summary>
        public string StartStrategy { get; set; } = string.Empty;
        public string? StartNodeId { get; set; }
        public string? StartEdgeType { get; set; }   // "Rising" | "Falling"
        public string? StartValue { get; set; }

        // ── End detection (primary) ──────────────────────────────────────────
        public string EndPrimaryStrategy { get; set; } = string.Empty;
        public string? EndPrimaryNodeId { get; set; }
        public string? EndPrimaryEdgeType { get; set; }

        // ── End detection (fallback) ─────────────────────────────────────────
        public string? EndFallbackStrategy { get; set; }
        public string? EndFallbackNodeId { get; set; }

        /// <summary>Semicolon-separated list of OPC node IDs that abort the cycle.</summary>
        public string? AbortNodeIds { get; set; }

        // ── Timing ───────────────────────────────────────────────────────────
        public int DebounceMs { get; set; }
        public int MinCycleSeconds { get; set; }
        public int TimeoutSeconds { get; set; }

        // ── Validation ───────────────────────────────────────────────────────
        public decimal? Epsilon { get; set; }
        public decimal? TargetTolerance { get; set; }

        public string? ValidationSpeedNodeId { get; set; }
        public decimal? ValidationSpeedMin { get; set; }

        public string? ValidationStateNodeId { get; set; }
        public string? ValidationStateValue { get; set; }

        // ── Recovery ─────────────────────────────────────────────────────────
        /// <summary>Strategy if service starts mid-production (e.g. "None", "EdgeConfirm").</summary>
        public string? RecoveryStrategy { get; set; }
        public string? RecoveryConfirmNodeId { get; set; }
        public decimal? RecoveryConfirmDelta { get; set; }
        public int? RecoveryConfirmWindowSeconds { get; set; }
    }

    /// <summary>Payload for creating a new cycle rule (POST).</summary>
    public sealed class CycleRuleCreateDto
    {
        public string MachineCode { get; set; } = string.Empty;
        public string ScopeKey { get; set; } = "default";
        public bool IsActive { get; set; } = true;

        public string StartStrategy { get; set; } = string.Empty;
        public string? StartNodeId { get; set; }
        public string? StartEdgeType { get; set; }
        public string? StartValue { get; set; }

        public string EndPrimaryStrategy { get; set; } = string.Empty;
        public string? EndPrimaryNodeId { get; set; }
        public string? EndPrimaryEdgeType { get; set; }

        public string? EndFallbackStrategy { get; set; }
        public string? EndFallbackNodeId { get; set; }
        public string? AbortNodeIds { get; set; }

        public int DebounceMs { get; set; } = 500;
        public int MinCycleSeconds { get; set; } = 1;
        public int TimeoutSeconds { get; set; } = 3600;

        public decimal? Epsilon { get; set; }
        public decimal? TargetTolerance { get; set; }
        public string? ValidationSpeedNodeId { get; set; }
        public decimal? ValidationSpeedMin { get; set; }
        public string? ValidationStateNodeId { get; set; }
        public string? ValidationStateValue { get; set; }

        public string? RecoveryStrategy { get; set; }
        public string? RecoveryConfirmNodeId { get; set; }
        public decimal? RecoveryConfirmDelta { get; set; }
        public int? RecoveryConfirmWindowSeconds { get; set; }
    }

    /// <summary>Payload for updating an existing cycle rule (PUT).</summary>
    public sealed class CycleRuleUpdateDto
    {
        public string ScopeKey { get; set; } = "default";
        public bool IsActive { get; set; }

        public string StartStrategy { get; set; } = string.Empty;
        public string? StartNodeId { get; set; }
        public string? StartEdgeType { get; set; }
        public string? StartValue { get; set; }

        public string EndPrimaryStrategy { get; set; } = string.Empty;
        public string? EndPrimaryNodeId { get; set; }
        public string? EndPrimaryEdgeType { get; set; }

        public string? EndFallbackStrategy { get; set; }
        public string? EndFallbackNodeId { get; set; }
        public string? AbortNodeIds { get; set; }

        public int DebounceMs { get; set; }
        public int MinCycleSeconds { get; set; }
        public int TimeoutSeconds { get; set; }

        public decimal? Epsilon { get; set; }
        public decimal? TargetTolerance { get; set; }
        public string? ValidationSpeedNodeId { get; set; }
        public decimal? ValidationSpeedMin { get; set; }
        public string? ValidationStateNodeId { get; set; }
        public string? ValidationStateValue { get; set; }

        public string? RecoveryStrategy { get; set; }
        public string? RecoveryConfirmNodeId { get; set; }
        public decimal? RecoveryConfirmDelta { get; set; }
        public int? RecoveryConfirmWindowSeconds { get; set; }
    }
}
