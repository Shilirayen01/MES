namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public sealed class CycleTrackingOptions
    {
        public bool Enabled { get; set; } = false;
        public int RuleReloadSeconds { get; set; } = 60;
        public string ScopeResolver { get; set; } = "Default"; // 'Db311' or 'Default'

        /// <summary>
        /// Enables "recovery" mode: if the runtime starts while production is already in
        /// progress, the tracker can create a Run without seeing the Start edge.
        /// </summary>
        public bool RecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Maximum time (in seconds) after the first observation of a scope during which
        /// recovery is allowed. This prevents creating late "recovered" runs long after startup.
        /// </summary>
        public int RecoveryMaxAgeSeconds { get; set; } = 60;
    }
}
