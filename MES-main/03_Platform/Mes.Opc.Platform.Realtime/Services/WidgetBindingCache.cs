using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;

namespace Mes.Opc.Platform.Realtime.Services
{
    /// <summary>
    /// In-memory routing map from (MachineCode, OpcNodeId) to widget targets.
    ///
    /// This cache is populated from SQL (UiWidgetBinding). It allows the
    /// realtime pipeline to translate high-frequency OPC tag updates into
    /// widget updates without querying the database per event.
    /// </summary>
    public sealed class WidgetBindingCache
    {
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

        // key: (machine, nodeId) normalized
        private Dictionary<(string Machine, string NodeId), List<(Guid WidgetId, string Role)>> _map
            = new();

        public void Replace(Dictionary<(string Machine, string NodeId), List<(Guid WidgetId, string Role)>> newMap)
        {
            _lock.EnterWriteLock();
            try
            {
                _map = newMap;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IReadOnlyList<(Guid WidgetId, string Role)> Resolve(string machineCode, string opcNodeId)
        {
            var key = (Normalize(machineCode), Normalize(opcNodeId));

            _lock.EnterReadLock();
            try
            {
                return _map.TryGetValue(key, out var list)
                    ? list
                    : Array.Empty<(Guid, string)>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private static readonly Regex _ws = new(@"\s+", RegexOptions.Compiled);

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            // Normalize whitespace differences (e.g., "Local  Items" vs "Local Items")
            // and make matching case-insensitive.
            var normalized = _ws.Replace(s, " ").Trim();
            return normalized.ToUpperInvariant();
        }
    }
}
