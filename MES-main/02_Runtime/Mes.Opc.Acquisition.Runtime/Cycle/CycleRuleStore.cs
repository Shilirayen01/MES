using System;
using System.Collections.Generic;
using System.Threading;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    /// <summary>
    /// Immutable snapshot store for cycle rules. Reload swaps the whole dictionary
    /// atomically to keep Process() lock-free.
    /// </summary>
    public sealed class CycleRuleStore : ICycleRuleProvider
    {
        private Dictionary<(string MachineCode, string ScopeKey), CycleRule> _rules
            = new(StringTupleComparer.Instance);

        public CycleRule? GetRule(string machineCode, string scopeKey)
        {
            var key = (machineCode.Trim(), scopeKey.Trim());
            return _rules.TryGetValue(key, out var r) ? r : null;
        }

        public void Swap(IEnumerable<CycleRule> rules)
        {
            var next = new Dictionary<(string MachineCode, string ScopeKey), CycleRule>(StringTupleComparer.Instance);
            foreach (var r in rules)
            {
                if (!r.IsActive) continue;
                next[(r.MachineCode.Trim(), r.ScopeKey.Trim())] = r;
            }
            Volatile.Write(ref _rules, next);
        }

        private sealed class StringTupleComparer : IEqualityComparer<(string MachineCode, string ScopeKey)>
        {
            public static readonly StringTupleComparer Instance = new();

            public bool Equals((string MachineCode, string ScopeKey) x, (string MachineCode, string ScopeKey) y)
                => StringComparer.OrdinalIgnoreCase.Equals(x.MachineCode, y.MachineCode)
                && StringComparer.OrdinalIgnoreCase.Equals(x.ScopeKey, y.ScopeKey);

            public int GetHashCode((string MachineCode, string ScopeKey) obj)
                => HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MachineCode),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ScopeKey));
        }
    }
}
