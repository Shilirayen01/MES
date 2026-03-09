using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mes.Opc.Acquisition.Runtime.Opc;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    /// <summary>
    /// In-memory state machine that detects product cycles (Start/End/Abort/Timeout)
    /// and emits a stable RunId for each active cycle.
    ///
    /// Key design constraints:
    /// - O(1) work per sample
    /// - no DB/IO inside Process()
    /// - thread-safe per (Machine, Scope)
    ///
    /// Recovery mode:
    /// If the runtime starts while production is already in progress, the tracker can
    /// create a Run without observing the Start edge ("Recovered" run).
    /// Recovery is configurable per rule (RecoveryStrategy + RecoveryConfirm* columns)
    /// and can be globally disabled via CycleTrackingOptions.RecoveryEnabled.
    /// </summary>
    public sealed class ProductRunTracker : IProductRunTracker
    {
        private readonly ICycleRuleProvider _rules;
        private readonly IScopeResolver _scopeResolver;
        private readonly CycleTrackingOptions _options;
        private readonly ILogger<ProductRunTracker> _logger;

        private readonly ConcurrentDictionary<(string MachineCode, string ScopeKey), ScopeState> _state = new();

        public ProductRunTracker(
            ICycleRuleProvider rules,
            IScopeResolver scopeResolver,
            IOptions<CycleTrackingOptions> options,
            ILogger<ProductRunTracker> logger)
        {
            _rules = rules;
            _scopeResolver = scopeResolver;
            _options = options.Value;
            _logger = logger;
        }

        public TrackResult Process(TagSample sample)
        {


            var scopeKey = _scopeResolver.ResolveScope(sample);
            // ✅ NEW: on cherche la règle AVANT de traiter le cas "Line"
            var rule = _rules.GetRule(sample.MachineCode, scopeKey);

            if(sample.MachineCode== "MC_BU26")
            {
                Console.WriteLine($"Process sample for MC_BU26 | NodeId='{sample.NodeId}' | Value='{sample.Value}' | Scope='{scopeKey}' | RuleActive='{rule?.IsActive}'");
            }
            // ✅ Cas spécial : tags "Line" globaux(LineSpeed, état ligne, alarmes globales, etc.)
            // On n’ignore "Line" QUE s’il n’existe PAS de règle active pour (Machine, Line)
            if (string.Equals(scopeKey, "Line", StringComparison.OrdinalIgnoreCase) && (rule is null || !rule.IsActive))
            {
                // On "injecte" le dernier LineSpeed dans chaque SPx pour que ValidationSpeedNodeId fonctionne
                foreach (var (_, r, stLine) in EnumerateActiveScopesForMachine(sample.MachineCode))
                {
                    lock (stLine.Lock)
                    {
                        stLine.Observe(sample, r);
                    }
                }

                // Pas d'évènement produit par les tags Line eux-mêmes
                return new TrackResult(null, scopeKey, null);
            }


            if (rule is null || !rule.IsActive)
            {
                return new TrackResult(null, scopeKey, null);
            }

            var key = (sample.MachineCode.Trim(), scopeKey.Trim());
            var st = _state.GetOrAdd(key, _ => new ScopeState(sample.TimestampUtc));

            lock (st.Lock)
            {
                // Always keep a snapshot of relevant values for recovery/validation.
                st.Observe(sample, rule);

                // Timeout check (only when running)
                if (st.IsRunning)
                {
                    var elapsed = sample.TimestampUtc - st.StartTsUtc;
                    if (elapsed.TotalSeconds >= rule.TimeoutSeconds)
                    {
                        return EndRun(st, sample, scopeKey, RunEventType.Timeout, "Timeout");
                    }
                }

                // Abort check (only when running). We treat AbortNodeIds as boolean alarms.
                if (st.IsRunning && IsAbortTriggered(rule, sample, st))
                {
                    return EndRun(st, sample, scopeKey, RunEventType.Aborted, "Abort");
                }

                // Start detection (edge/value)
                if (!st.IsRunning && IsStart(rule, sample, st))
                {
                    return StartRun(st, sample, scopeKey, reason: "Start");
                }

                // Recovery start (missed start edge at runtime boot). This is evaluated
                // after normal start detection so a real edge always wins.
                if (!st.IsRunning && TryRecoveryStart(rule, st, sample.TimestampUtc, out var recovered))
                {
                    _logger.LogInformation(
                        "CycleTracking | Recovered run started | Machine={machine} | Scope={scope} | Reason={reason}",
                        sample.MachineCode, scopeKey, recovered.Reason);

                    return StartRun(st, sample with { TimestampUtc = recovered.TimestampUtc }, scopeKey, reason: recovered.Reason);
                }

                // End detection (primary then fallback)
                if (st.IsRunning && IsEndPrimary(rule, sample, st))
                {
                    if (!PassMinCycle(rule, sample, st))
                    {
                        return new TrackResult(st.RunId, scopeKey, null);
                    }
                    return EndRun(st, sample, scopeKey, RunEventType.Ended, "EndPrimary");
                }

                if (st.IsRunning && IsEndFallback(rule, sample, st))
                {
                    if (!PassMinCycle(rule, sample, st))
                    {
                        return new TrackResult(st.RunId, scopeKey, null);
                    }
                    return EndRun(st, sample, scopeKey, RunEventType.Ended, "EndFallback");
                }

                // No event: return current RunId (if any)
                return new TrackResult(st.IsRunning ? st.RunId : null, scopeKey, null);
            }
        }

        private static bool PassMinCycle(CycleRule rule, TagSample sample, ScopeState st)
            => (sample.TimestampUtc - st.StartTsUtc).TotalSeconds >= rule.MinCycleSeconds;

        private static bool NodeMatch(string? configuredNodeId, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(configuredNodeId)) return false;

            // Exact match first (fast path)
            if (string.Equals(configuredNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                return true;

            // Tolerant match: treat legacy "ns=" and stable "nsu=" as equivalent by comparing
            // the identifier part after the first ';'.
            var a = OpcNodeIdHelper.NormalizeForCompare(configuredNodeId);
            var b = OpcNodeIdHelper.NormalizeForCompare(nodeId);

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

private static bool TryParseBool(string? v, out bool b)
        {
            if (v is null) { b = false; return false; }
            if (bool.TryParse(v, out b)) return true;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                b = i != 0;
                return true;
            }
            b = false;
            return false;
        }

        private static bool TryParseDecimal(string? v, out decimal d)
        {
            if (v is null) { d = 0m; return false; }
            return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d);
        }

        private static bool IsAbortTriggered(CycleRule rule, TagSample sample, ScopeState st)
        {
            if (rule.AbortNodeIds.Count == 0) return false;

            // Abort is only evaluated when the incoming sample is one of the Abort nodes.
            // Trigger condition: Rising edge to TRUE (debounced).
            if (!rule.AbortNodeIds.Any(a => NodeMatch(a, sample.NodeId)))
                return false;

            if (!TryParseBool(sample.Value, out var current))
                return false;

            return st.EdgeOnNode(sample.NodeId, current, EdgeType.Rising, sample.TimestampUtc, debounceMs: rule.DebounceMs);
        }

        private bool IsStart(CycleRule rule, TagSample sample, ScopeState st)
        {
            if (rule.StartStrategy == StartStrategy.None) return false;

            if (rule.StartStrategy == StartStrategy.EdgeOnBit)
            {
                if (!NodeMatch(rule.StartNodeId, sample.NodeId)) return false;
                if (!TryParseBool(sample.Value, out var current)) return false;
                var edge = rule.StartEdgeType ?? EdgeType.Rising;
                return st.EdgeOnNode(sample.NodeId, current, edge, sample.TimestampUtc, debounceMs: rule.DebounceMs);
            }

            if (rule.StartStrategy == StartStrategy.ValueEquals)
            {
                if (!NodeMatch(rule.StartNodeId, sample.NodeId)) return false;
                return string.Equals(sample.Value, rule.StartValue, StringComparison.OrdinalIgnoreCase);
            }

            // ✅ NEW: Start on counter increase / numeric change
            if (rule.StartStrategy == StartStrategy.LastChanged ) 
            {
                if (!NodeMatch(rule.StartNodeId, sample.NodeId)) return false;

                // Utilise Epsilon pour filtrer le bruit (ex: 1.000)
                return st.LastChangedOnNode(sample.NodeId, sample.Value, rule.Epsilon);
            }
            if (rule.StartStrategy == StartStrategy.CounterIncrease)
            {
                if (!NodeMatch(rule.StartNodeId, sample.NodeId)) return false;
                return st.CounterIncreasedOnNode(sample.NodeId, sample.Value, rule.Epsilon);
            }
            return false;
        }
        
        private bool IsEndPrimary(CycleRule rule, TagSample sample, ScopeState st)
        {
            if (rule.EndPrimaryStrategy == EndStrategy.None) return false;

            if (rule.EndPrimaryStrategy == EndStrategy.EdgeOnBit)
            {
                if (!NodeMatch(rule.EndPrimaryNodeId, sample.NodeId)) return false;
                if (!TryParseBool(sample.Value, out var current)) return false;
                var edge = rule.EndPrimaryEdgeType ?? EdgeType.Rising;
                return st.EdgeOnNode(sample.NodeId, current, edge, sample.TimestampUtc, debounceMs: rule.DebounceMs);
            }

            if (rule.EndPrimaryStrategy == EndStrategy.LastChanged)
            {
                if (!NodeMatch(rule.EndPrimaryNodeId, sample.NodeId)) return false;
                return st.LastChangedOnNode(sample.NodeId, sample.Value, rule.Epsilon);
            }



            return false;
        }

        private bool IsEndFallback(CycleRule rule, TagSample sample, ScopeState st)
        {
            if (rule.EndFallbackStrategy is null || rule.EndFallbackStrategy == EndStrategy.None) return false;

            if (rule.EndFallbackStrategy == EndStrategy.LastChanged)
            {
                if (!NodeMatch(rule.EndFallbackNodeId, sample.NodeId)) return false;
                return st.LastChangedOnNode(sample.NodeId, sample.Value, rule.Epsilon);
            }

            if (rule.EndFallbackStrategy == EndStrategy.EdgeOnBit)
            {
                if (!NodeMatch(rule.EndFallbackNodeId, sample.NodeId)) return false;
                if (!TryParseBool(sample.Value, out var current)) return false;
                return st.EdgeOnNode(sample.NodeId, current, EdgeType.Rising, sample.TimestampUtc, debounceMs: rule.DebounceMs);
            }

            return false;
        }

        private bool TryRecoveryStart(CycleRule rule, ScopeState st, DateTime nowUtc, out RecoveryDecision decision)
        {
            decision = default;

            if (!_options.RecoveryEnabled) return false;
            if (rule.RecoveryStrategy == RecoveryStrategy.None) return false;
            if (st.RecoveryCompleted) return false;

            // Optional: try to read start signal, but do NOT block recovery if it's false.
            // We rely on validation + confirmation (speed + counter increase) to prove production is running.
            _ = IsStartConditionActive(rule, st, out _);

            // Start recovery window when we are actually in a "running-like" situation (validation gates pass).
            if (!PassValidationGates(rule, st))
                return false;

            st.RecoveryWindowStartUtc ??= nowUtc;

            if ((nowUtc - st.RecoveryWindowStartUtc.Value).TotalSeconds > Math.Max(5, _options.RecoveryMaxAgeSeconds))
            {
                st.RecoveryCompleted = true;
                return false;
            }

            // Not already finished
            if (IsEndFinishedActive(rule, st, out var finishedActive) && finishedActive)
                return false;

            // Abort guard
            if (IsAnyAbortActive(rule, st))
                return false;

            // Confirmation step (CounterIncrease recommended)
            if (!PassRecoveryConfirmation(rule, st, nowUtc))
                return false;

            st.RecoveryCompleted = true;
            decision = new RecoveryDecision(nowUtc, "Recovered");
            return true;
        }




        private static bool IsStartConditionActive(CycleRule rule, ScopeState st, out bool active)
        {
            active = false;

            if (rule.StartStrategy == StartStrategy.EdgeOnBit)
            {
                if (string.IsNullOrWhiteSpace(rule.StartNodeId)) return false;
                if (!st.TryGetLatest(rule.StartNodeId, out var sv)) return false;
                if (!TryParseBool(sv.Value, out var b)) return false;

                // For a rising-edge start, "active" means the bit is true.
                // For a falling-edge start, "active" means the bit is false.
                var edge = rule.StartEdgeType ?? EdgeType.Rising;
                active = edge == EdgeType.Rising ? b : !b;
                return true;
            }

            if (rule.StartStrategy == StartStrategy.ValueEquals)
            {
                if (string.IsNullOrWhiteSpace(rule.StartNodeId)) return false;
                if (!st.TryGetLatest(rule.StartNodeId, out var sv)) return false;
                active = string.Equals(sv.Value, rule.StartValue, StringComparison.OrdinalIgnoreCase);
                return true;
            }

            // Other strategies not supported for recovery in this version.
            return false;
        }

        private static bool IsEndFinishedActive(CycleRule rule, ScopeState st, out bool finishedActive)
        {
            finishedActive = false;

            if (rule.EndPrimaryStrategy != EndStrategy.EdgeOnBit) return false;
            if (string.IsNullOrWhiteSpace(rule.EndPrimaryNodeId)) return false;

            if (!st.TryGetLatest(rule.EndPrimaryNodeId, out var ev)) return false;
            if (!TryParseBool(ev.Value, out var b)) return false;

            var edge = rule.EndPrimaryEdgeType ?? EdgeType.Rising;
            finishedActive = edge == EdgeType.Rising ? b : !b;
            return true;
        }

        private static bool PassValidationGates(CycleRule rule, ScopeState st)
        {
            // Speed gate
            if (!string.IsNullOrWhiteSpace(rule.ValidationSpeedNodeId) && rule.ValidationSpeedMin.HasValue)
            {
                if (!st.TryGetLatest(rule.ValidationSpeedNodeId, out var sv)) return false;
                if (!TryParseDecimal(sv.Value, out var speed)) return false;
                if (speed < rule.ValidationSpeedMin.Value) return false;
            }

            // State gate
            if (!string.IsNullOrWhiteSpace(rule.ValidationStateNodeId) && !string.IsNullOrWhiteSpace(rule.ValidationStateValue))
            {
                if (!st.TryGetLatest(rule.ValidationStateNodeId, out var sv)) return false;
                if (!string.Equals(sv.Value, rule.ValidationStateValue, StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }

        private static bool IsAnyAbortActive(CycleRule rule, ScopeState st)
        {
            foreach (var node in rule.AbortNodeIds)
            {
                if (!st.TryGetLatest(node, out var sv)) continue;
                if (!TryParseBool(sv.Value, out var b)) continue;
                if (b) return true;
            }
            return false;
        }

        private static bool PassRecoveryConfirmation(CycleRule rule, ScopeState st, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(rule.RecoveryConfirmNodeId))
            {
                // If confirm node isn't configured, we cannot safely recover.
                return false;
            }

            if (!st.TryGetLatest(rule.RecoveryConfirmNodeId, out var cv))
                return false;

            if (!TryParseDecimal(cv.Value, out var current))
                return false;

            if (rule.RecoveryStrategy == RecoveryStrategy.CounterNonZero)
            {
                return current > 0m;
            }

            if (rule.RecoveryStrategy == RecoveryStrategy.CounterIncrease)
            {
                var windowSeconds = Math.Max(1, rule.RecoveryConfirmWindowSeconds ?? 5);
                var minDelta = Math.Max(0.000m, rule.RecoveryConfirmDelta ?? 1m);

                // Arm baseline on first observation of the confirm counter.
                if (!st.RecoveryCounterBaseline.HasValue)
                {
                    st.RecoveryCounterBaseline = current;
                    st.RecoveryCounterBaselineTsUtc = nowUtc;
                    return false;
                }

                // Baseline too old => re-arm.
                if (st.RecoveryCounterBaselineTsUtc.HasValue && (nowUtc - st.RecoveryCounterBaselineTsUtc.Value).TotalSeconds > windowSeconds)
                {
                    st.RecoveryCounterBaseline = current;
                    st.RecoveryCounterBaselineTsUtc = nowUtc;
                    return false;
                }

                return current >= st.RecoveryCounterBaseline.Value + minDelta;
            }

            return false;
        }

        private static TrackResult StartRun(ScopeState st, TagSample sample, string scopeKey, string reason)
        {
            var id = Guid.NewGuid();

            st.IsRunning = true;
            st.RunId = id;
            st.StartTsUtc = sample.TimestampUtc;

            // Once a run exists, recovery is no longer applicable for this scope.
            st.RecoveryCompleted = true;

            var ev = new RunEvent(id, sample.MachineCode, scopeKey, RunEventType.Started, sample.TimestampUtc, reason);
            return new TrackResult(id, scopeKey, ev);
        }
        private IEnumerable<(string scopeKey, CycleRule rule, ScopeState st)> EnumerateActiveScopesForMachine(string machineCode)
        {
            // On supporte SP1..SP5 de DB311 (générique : tu peux rendre ça configurable plus tard)
            var scopes = new[] { "SP1", "SP2", "SP3", "SP4", "SP5" };

            foreach (var sk in scopes)
            {
                var r = _rules.GetRule(machineCode, sk);
                if (r is null || !r.IsActive) continue;

                var key = (machineCode.Trim(), sk.Trim());
                var st = _state.GetOrAdd(key, _ => new ScopeState(DateTime.UtcNow));
                yield return (sk, r, st);
            }
        }
        private static TrackResult EndRun(ScopeState st, TagSample sample, string scopeKey, RunEventType type, string reason)
        {
            var id = st.RunId;

            st.IsRunning = false;
            st.RunId = null;
            st.StartTsUtc = default;

            // Reset numeric baselines used by end strategies and recovery.
            st.RecoveryCounterBaseline = null;
            st.RecoveryCounterBaselineTsUtc = null;

            var ev = id.HasValue
                ? new RunEvent(id.Value, sample.MachineCode, scopeKey, type, sample.TimestampUtc, reason)
                : null;

            return new TrackResult(null, scopeKey, ev);
        }

        private readonly record struct RecoveryDecision(DateTime TimestampUtc, string Reason);

        private sealed class ScopeState
        {
            public readonly object Lock = new();

            public bool IsRunning;
            public Guid? RunId;
            public DateTime StartTsUtc;

            // Recovery control
            public DateTime FirstSeenUtc { get; }

            /// <summary>
            /// If true, recovery will no longer be attempted for this scope (either succeeded, or expired/disabled).
            /// </summary>
            public bool RecoveryCompleted = false;

            /// <summary>
            /// When start condition becomes active for the first time, we start the recovery window from here.
            /// This prevents expiring recovery too early before the necessary tags arrive.
            /// </summary>
            public DateTime? RecoveryWindowStartUtc;

            /// <summary>
            /// Baseline used by recovery confirmation strategies that rely on a numeric counter (e.g. CounterIncrease).
            /// </summary>
            public decimal? RecoveryCounterBaseline;

            /// <summary>
            /// Timestamp of the baseline counter sample.
            /// </summary>
            public DateTime? RecoveryCounterBaselineTsUtc;

            // Latest values for relevant node ids
            private readonly Dictionary<string, ObservedValue> _latest = new(StringComparer.OrdinalIgnoreCase);

            // Per-node last bool for edge detection
            private readonly Dictionary<string, bool?> _lastBool = new(StringComparer.OrdinalIgnoreCase);
            private DateTime? _lastEdgeTsUtc;

            // Per-node last numeric for change detection
            private readonly Dictionary<string, decimal?> _lastNumeric = new(StringComparer.OrdinalIgnoreCase);

            public ScopeState(DateTime firstSeenUtc)
            {
                FirstSeenUtc = firstSeenUtc;
            }

            public void Observe(TagSample sample, CycleRule rule)
            {
                var wanted = rule.ValidationSpeedNodeId;

                if (!string.IsNullOrWhiteSpace(wanted) &&
                    sample.NodeId.Contains("LineSpeed", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"OBSERVE LineSpeed sample.NodeId='{sample.NodeId}' | rule.ValidationSpeedNodeId='{wanted}'");
                }

                if (!IsRelevantNode(rule, sample.NodeId))
                    return;

                _latest[sample.NodeId] = new ObservedValue(sample.Value, sample.TimestampUtc);
            }




            public bool TryGetLatest(string nodeId, out ObservedValue value)
                => _latest.TryGetValue(nodeId, out value);

            public bool EdgeOnNode(string nodeId, bool current, EdgeType edge, DateTime tsUtc, int debounceMs)
            {
                _lastBool.TryGetValue(nodeId, out var previous);

                // Update last-known value
                _lastBool[nodeId] = current;

                // We need a previous value to detect an edge.
                if (previous is null) return false;

                var rise = !previous.Value && current;
                var fall = previous.Value && !current;
                var hit = edge == EdgeType.Rising ? rise : fall;
                if (!hit) return false;

                // Debounce (global per scope to avoid spikes across multiple nodes)
                if (_lastEdgeTsUtc.HasValue && (tsUtc - _lastEdgeTsUtc.Value).TotalMilliseconds < debounceMs)
                    return false;

                _lastEdgeTsUtc = tsUtc;
                return true;
            }

            public bool LastChangedOnNode(string nodeId, string? value, decimal? epsilon)
            {
                if (value is null) return false;
                if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var current)) return false;

                _lastNumeric.TryGetValue(nodeId, out var last);

                if (last is null)
                {
                    _lastNumeric[nodeId] = current;
                    return false;
                }

                var eps = epsilon ?? 0m;
                var changed = current > last.Value + eps;
                _lastNumeric[nodeId] = current;
                return changed;
            }
            public bool CounterIncreasedOnNode(string nodeId, string? value, decimal? epsilon)
            {
                if (value is null) return false;
                if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var current)) return false;

                _lastNumeric.TryGetValue(nodeId, out var last);

                if (last is null)
                {
                    _lastNumeric[nodeId] = current;

                    // ✅ pour le cas "Line" (service démarre en plein run)
                    // si tu veux être permissif : return current > 0m;
                    return false;
                }

                var eps = epsilon ?? 0m;
                var increased = current > last.Value + eps;

                _lastNumeric[nodeId] = current;
                return increased;
            }

            private static bool IsRelevantNode(CycleRule rule, string nodeId)
            {

                if (NodeEquals(rule.StartNodeId, nodeId)) return true;
                if (NodeEquals(rule.EndPrimaryNodeId, nodeId)) return true;
                if (NodeEquals(rule.EndFallbackNodeId, nodeId)) return true;
                if (NodeEquals(rule.ValidationSpeedNodeId, nodeId)) return true;
                if (NodeEquals(rule.ValidationStateNodeId, nodeId)) return true;
                if (NodeEquals(rule.RecoveryConfirmNodeId, nodeId)) return true;

                foreach (var a in rule.AbortNodeIds)
                {
                    if (NodeEquals(a, nodeId)) return true;
                }

                return false;
            }

            private static bool NodeEquals(string? configured, string actual)
                => !string.IsNullOrWhiteSpace(configured)
                   && string.Equals(configured, actual, StringComparison.OrdinalIgnoreCase);
        }

        private readonly record struct ObservedValue(string? Value, DateTime TimestampUtc);

    }
}