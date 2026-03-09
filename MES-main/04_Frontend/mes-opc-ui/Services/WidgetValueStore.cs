using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mes.Opc.Contracts.Dtos;

namespace mes_opc_ui.Services
{
    /// <summary>
    /// In-memory state store used by Blazor components.
    ///
    /// - Receives realtime tag updates from SignalR
    /// - Translates them into per-widget values using WidgetBinding (MachineCode+OpcNodeId)
    /// - Notifies the UI when values change
    ///
    /// This is intentionally simple (no external state library) so the solution stays easy to understand.
    /// </summary>
    public sealed class WidgetValueStore
    {
        // WidgetId -> (Role -> Value)
        // Role dictionary is case-insensitive so SQL "value" and UI "Value" still match.
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, string?>> _widgetValues = new();

        // (MachineCode, OpcNodeId) -> list of widget targets (WidgetId, Role)
        // Keys are normalized to reduce "it receives but doesn't display" issues.
        private readonly ConcurrentDictionary<(string Machine, string NodeId), List<(Guid WidgetId, string Role)>> _route = new();

        public event Action? Changed;

        /// <summary>
        /// Builds a fast lookup map so incoming tag updates can update the correct widget(s).
        /// Call this once after loading the dashboard.
        /// </summary>
        public void Configure(DashboardDto dashboard)
        {
            _route.Clear();

            foreach (var binding in dashboard.Zones
                         .SelectMany(z => z.Widgets)
                         .SelectMany(w => w.Bindings))
            {
                var key = (NormalizeKey(binding.MachineCode), NormalizeKey(binding.OpcNodeId));
                var list = _route.GetOrAdd(key, _ => new List<(Guid, string)>());

                // Keep role as-is (trimmed). Actual lookup is case-insensitive.
                list.Add((binding.WidgetId, NormalizeRole(binding.BindingRole)));
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Updates one or more widgets based on an incoming tag value.
        /// This is used when the UI subscribes per-machine (tagUpdate).
        /// </summary>
        public void OnTagUpdate(MachineTagValueDto tag)
        {
            var key = (NormalizeKey(tag.MachineCode), NormalizeKey(tag.OpcNodeId));
            if (!_route.TryGetValue(key, out var targets) || targets.Count == 0)
                return;

            foreach (var (widgetId, role) in targets)
            {
                var valuesForWidget = _widgetValues.GetOrAdd(
                    widgetId,
                    _ => new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

                valuesForWidget[role] = tag.Value;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Updates a widget directly from a server-side widget update event.
        /// This is the preferred mode: the backend dispatches per-widget updates over SignalR.
        /// </summary>
        public void OnWidgetUpdate(WidgetUpdateDto update)
        {
            if (update.WidgetId == Guid.Empty)
                return;

            var role = NormalizeRole(update.BindingRole);
            if (string.IsNullOrWhiteSpace(role))
                role = "Value";

            var valuesForWidget = _widgetValues.GetOrAdd(
                update.WidgetId,
                _ => new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

            valuesForWidget[role] = update.Value;

            Changed?.Invoke();
        }

        /// <summary>
        /// Reads a widget value by role (e.g. role="Value", role="Status").
        /// Returns null if no value is currently known.
        /// </summary>
        public string? Get(Guid widgetId, string role)
        {
            if (widgetId == Guid.Empty)
                return null;

            if (!_widgetValues.TryGetValue(widgetId, out var roles))
                return null;

            var normalizedRole = NormalizeRole(role);
            if (string.IsNullOrWhiteSpace(normalizedRole))
                normalizedRole = "Value";

            return roles.TryGetValue(normalizedRole, out var v) ? v : null;
        }

        public IReadOnlyCollection<string> GetMachinesToSubscribe(DashboardDto dashboard)
        {
            return dashboard.Zones
                .SelectMany(z => z.Widgets)
                .SelectMany(w => w.Bindings)
                .Select(b => NormalizeKey(b.MachineCode))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyCollection<Guid> GetWidgetsToSubscribe(DashboardDto dashboard)
        {
            return dashboard.Zones
                .SelectMany(z => z.Widgets)
                .Select(w => w.Id)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();
        }

        private static string NormalizeKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            return s.Trim().ToUpperInvariant();
        }

        private static string NormalizeRole(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            return s.Trim();
        }
    }
}
