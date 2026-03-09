using System;

namespace Mes.Opc.Acquisition.Runtime.Persistence
{
    public sealed class ProductionRun
    {
        public Guid RunId { get; set; }
        public string MachineCode { get; set; } = default!;
        public string ScopeKey { get; set; } = default!;
        public DateTime StartTsUtc { get; set; }
        public DateTime? EndTsUtc { get; set; }
        public string Status { get; set; } = "Running";
        public string? EndReason { get; set; }
        public string? MetaJson { get; set; }
    }
}
