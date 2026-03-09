using System;
using System.Collections.Generic;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public enum EdgeType { Rising, Falling }

    public enum RunEventType { Started, Ended, Aborted, Timeout }

    public enum StartStrategy { None, EdgeOnBit, ValueEquals, CounterReset, LastChanged, CounterIncrease }

    public enum EndStrategy { None, EdgeOnBit, LastChanged, CounterReachedTarget }

    /// <summary>
    /// Optional "recovery" mode used to create a Run when the runtime starts
    /// while production is already in progress (missed Start edge).
    /// </summary>
    public enum RecoveryStrategy
    {
        None,
        /// <summary>Recovery confirmation succeeds if a numeric counter is non-zero.</summary>
        CounterNonZero,
        /// <summary>Recovery confirmation succeeds if a numeric counter increases within a window.</summary>
        CounterIncrease
    }

    public sealed record CycleRule(
        string MachineCode,
        string ScopeKey,
        bool IsActive,
        StartStrategy StartStrategy,
        string? StartNodeId,
        EdgeType? StartEdgeType,
        string? StartValue,
        EndStrategy EndPrimaryStrategy,
        string? EndPrimaryNodeId,
        EdgeType? EndPrimaryEdgeType,
        EndStrategy? EndFallbackStrategy,
        string? EndFallbackNodeId,
        IReadOnlyList<string> AbortNodeIds,
        int DebounceMs,
        int MinCycleSeconds,
        int TimeoutSeconds,
        decimal? Epsilon,
        decimal? TargetTolerance,
        string? ValidationSpeedNodeId,
        decimal? ValidationSpeedMin,
        string? ValidationStateNodeId,
        string? ValidationStateValue,
        // Recovery (optional)
        RecoveryStrategy RecoveryStrategy,
        string? RecoveryConfirmNodeId,
        decimal? RecoveryConfirmDelta,
        int? RecoveryConfirmWindowSeconds
    );

    public sealed record TagSample(
        string MachineCode,
        string NodeId,
        string? Value,
        DateTime TimestampUtc
    );

    public sealed record RunEvent(
        Guid RunId,
        string MachineCode,
        string ScopeKey,
        RunEventType Type,
        DateTime TimestampUtc,
        string Reason
    );

    public sealed record TrackResult(
        Guid? RunId,
        string ScopeKey,
        RunEvent? Event
    );
}
