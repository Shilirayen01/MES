namespace mes_opc_ui.Services
{
    /// <summary>
    /// Centralizes client-side endpoints.
    ///
    /// - ApiBaseUrl: REST API base (Mes.Opc.Platform.Api)
    /// - RealtimeBaseUrl: SignalR base (Mes.Opc.Platform.Realtime)
    ///
    /// If RealtimeBaseUrl is not provided, it falls back to ApiBaseUrl.
    /// </summary>
    public sealed class ClientEndpoints
    {
        public string ApiBaseUrl { get; }
        public string RealtimeBaseUrl { get; }

        public ClientEndpoints(string apiBaseUrl, string? realtimeBaseUrl = null)
        {
            ApiBaseUrl = EnsureTrailingSlash(apiBaseUrl);
            RealtimeBaseUrl = EnsureTrailingSlash(string.IsNullOrWhiteSpace(realtimeBaseUrl) ? apiBaseUrl : realtimeBaseUrl);
        }

        private static string EnsureTrailingSlash(string url)
        {
            url = (url ?? string.Empty).Trim();
            if (url.Length == 0) return url;
            return url.EndsWith("/") ? url : url + "/";
        }
    }
}
