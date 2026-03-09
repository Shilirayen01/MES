using System;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public sealed class Db311ScopeResolver : IScopeResolver
    {
        public string ResolveScope(TagSample sample)
        {
            var node = sample.NodeId ?? string.Empty;
            // Case-insensitive check
            if (node.Contains("SP1_", StringComparison.OrdinalIgnoreCase)) return "SP1";
            if (node.Contains("SP2_", StringComparison.OrdinalIgnoreCase)) return "SP2";
            if (node.Contains("SP3_", StringComparison.OrdinalIgnoreCase)) return "SP3";
            if (node.Contains("SP4_", StringComparison.OrdinalIgnoreCase)) return "SP4";
            if (node.Contains("SP5_", StringComparison.OrdinalIgnoreCase)) return "SP5";
            return "Line";
        }
    }
}
