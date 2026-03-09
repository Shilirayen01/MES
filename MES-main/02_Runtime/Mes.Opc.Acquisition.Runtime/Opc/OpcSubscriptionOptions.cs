namespace Mes.Opc.Acquisition.Runtime.Opc
{
    /// <summary>
    /// Configuration options for subscription behavior.
    /// </summary>
    public sealed class OpcSubscriptionOptions
    {
        /// <summary>
        /// When true, the client performs a Read() of all subscribed nodes once right
        /// after creating the subscription and publishes those values through the same
        /// callback pipeline (onValueChanged).
        /// </summary>
        public bool InitialSnapshotEnabled { get; set; } = true;

        /// <summary>
        /// Number of nodes per Read() request (chunking avoids server request limits).
        /// </summary>
        public int InitialSnapshotChunkSize { get; set; } = 200;

        /// <summary>
        /// If false, snapshot values with Bad status codes are skipped.
        /// </summary>
        public bool InitialSnapshotIncludeBadStatus { get; set; } = true;
    }
}
