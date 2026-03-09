// -------------------------------------------------------------------------------------------------
// OpcNodeIdHelper.cs
//
// Resolves OPC UA NodeIds from either legacy "ns=<index>;..." strings or
// stable "nsu=<namespaceUri>;..." strings.
//
// Why this exists:
// - Softing dataFEED can reorder namespace indexes across restarts.
// - Storing NodeIds as "ns=<index>" in DB becomes fragile.
// - Storing NodeIds as "nsu=<uri>" is stable, but must be resolved against the
//   Session.NamespaceUris table at runtime.
//
// This helper keeps the rest of the project unchanged: callers still pass a
// string NodeId, and we do the right thing depending on the format.
// -------------------------------------------------------------------------------------------------

using System;
using Opc.Ua;
using Opc.Ua.Client;

namespace Mes.Opc.Acquisition.Runtime.Opc
{
    public static class OpcNodeIdHelper
    {
        /// <summary>
        /// Converts an OPC UA NodeId string into a concrete <see cref="NodeId"/> usable
        /// by the client stack.
        /// - If the string uses the stable "nsu=" form, we parse it as an ExpandedNodeId
        ///   and resolve to the current namespace index using <paramref name="session"/>.
        /// - Otherwise we parse it as a legacy NodeId ("ns=" form).
        /// </summary>
        public static NodeId ToNodeId(Session session, string nodeIdOrNsu)
        {
            if (session is null) throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(nodeIdOrNsu)) throw new ArgumentException("NodeId is null/empty.", nameof(nodeIdOrNsu));

            // Fast path: legacy "ns=" string
            if (!nodeIdOrNsu.Contains("nsu=", StringComparison.OrdinalIgnoreCase))
                return NodeId.Parse(nodeIdOrNsu);

            // Stable path: "nsu=" string => parse as ExpandedNodeId then map URI -> index.
            var expanded = ExpandedNodeId.Parse(nodeIdOrNsu);

            // ExpandedNodeId may contain a NamespaceUri that must be mapped against the session table.
            // This is the key that makes NSU stable across server restarts.
            var resolved = ExpandedNodeId.ToNodeId(expanded, session.NamespaceUris);

            // If mapping fails, ToNodeId can return null; keep a clear exception.
            return resolved ?? throw new ServiceResultException(StatusCodes.BadNodeIdUnknown, $"Unable to resolve NodeId from '{nodeIdOrNsu}'.");
        }

        /// <summary>
        /// Normalizes a NodeId string by stripping the namespace prefix ("ns=" or "nsu=") and keeping
        /// the identifier part. This is used to compare configured node IDs vs incoming node IDs even
        /// if one side is legacy "ns=" and the other is stable "nsu=".
        /// </summary>
        public static string NormalizeForCompare(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return string.Empty;

            var s = nodeId.Trim();
            var semi = s.IndexOf(';');
            if (semi >= 0 && semi < s.Length - 1)
                return s[(semi + 1)..].Trim();

            return s;
        }
    }
}
