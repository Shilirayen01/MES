// -------------------------------------------------------------------------------------------------
// OpcTagHub.cs
//
// This SignalR hub manages client subscriptions for OPC tag updates.  Clients
// connect to this hub and call JoinMachine to subscribe to updates for a
// specific machine.  When the realtime publisher receives a new tag value
// it broadcasts to the group named after the machine code.  Clients can
// also leave a group by calling LeaveMachine.
// -------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace Mes.Opc.Platform.Realtime
{
    /// <summary>
    /// SignalR hub that clients connect to for receiving real‑time OPC tag
    /// updates.  Clients should join groups corresponding to machine codes
    /// to receive only the updates relevant to those machines.  Group names
    /// are normalised (trimmed and uppercased) to ensure consistent
    /// subscription semantics.
    /// </summary>
    // Enforce that only authenticated users can connect; adjust as needed
    public class OpcTagHub : Hub
    {
        /// <summary>
        /// Adds the caller's connection to the group for the specified machine.
        /// </summary>
        /// <param name="machineCode">The code of the machine to subscribe to.</param>
        /// <returns>A task that completes when the caller has been added to the group.</returns>
        public async Task JoinMachine(string machineCode)
        {
            if (string.IsNullOrWhiteSpace(machineCode)) return;
            var group = machineCode.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        /// <summary>
        /// Removes the caller's connection from the group for the specified machine.
        /// </summary>
        /// <param name="machineCode">The code of the machine to unsubscribe from.</param>
        /// <returns>A task that completes when the caller has been removed from the group.</returns>
        public async Task LeaveMachine(string machineCode)
        {
            if (string.IsNullOrWhiteSpace(machineCode)) return;
            var group = machineCode.Trim().ToUpperInvariant();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        /// <summary>
        /// Adds the caller's connection to the group for the specified widget.
        /// Group name format: "widget-{WidgetId}".
        /// </summary>
        public Task JoinWidget(Guid widgetId)
        {
            if (widgetId == Guid.Empty)
                return Task.CompletedTask;

            return Groups.AddToGroupAsync(Context.ConnectionId, $"widget-{widgetId}");
        }

        /// <summary>
        /// Removes the caller's connection from the group for the specified widget.
        /// </summary>
        public Task LeaveWidget(Guid widgetId)
        {
            if (widgetId == Guid.Empty)
                return Task.CompletedTask;

            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"widget-{widgetId}");
        }

        /// <summary>
        /// Called when a connection is disconnected.  Any cleanup or logging
        /// can be performed here.  Groups are automatically cleaned up by
        /// SignalR when connections are removed.
        /// </summary>
        /// <param name="exception">The exception that triggered the disconnect, if any.</param>
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Base implementation removes the connection from all groups.
            return base.OnDisconnectedAsync(exception);
        }
    }
}