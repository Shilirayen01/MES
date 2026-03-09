// -------------------------------------------------------------------------------------------------
// OpcSessionManager.cs
//
// The OpcSessionManager is responsible for establishing secure OPC UA client
// sessions.  A session encapsulates the communication channel between a
// client and an OPC UA server.  This manager creates and configures an
// ApplicationConfiguration, selects an endpoint and returns a new Session
// object ready for use.  The method is heavily commented to explain each
// step of the session setup process.
// -------------------------------------------------------------------------------------------------

using System.Threading.Tasks;           // Provides Task for asynchronous methods
using Opc.Ua;                            // Provides core OPC UA types such as ApplicationType and NodeId
using Opc.Ua.Client;                     // Provides client types like Session

namespace Mes.Opc.Acquisition.Runtime.Opc
{
    /// <summary>
    /// Provides functionality to create OPC UA client sessions.  A session
    /// represents a logical connection between a client and a server and is
    /// required before subscriptions can be created.
    /// </summary>
    public class OpcSessionManager
    {
        /// <summary>
        /// Asynchronously creates an OPC UA session for a given endpoint URL.  The
        /// session is configured with certificate stores and security settings
        /// suitable for development.  In a production environment the
        /// certificate settings should be tightened according to corporate
        /// policies.
        /// </summary>
        /// <param name="endpointUrl">The OPC UA endpoint URL to connect to.</param>
        /// <returns>A configured and connected <see cref="Session"/> object.</returns>
        public async Task<Session> CreateSessionAsync(string endpointUrl)
        {
            // Build an application configuration.  This includes identification of the
            // application, security configuration (certificates), transport
            // configuration and client configuration.
            var config = new ApplicationConfiguration
            {
                // Friendly name for the client application.
                ApplicationName = "MES.OPC.Runtime",
                // Indicate that this application acts as a client.
                ApplicationType = ApplicationType.Client,

                // Configure certificate stores used for securing communication.  These
                // directories must exist in the application's working directory.  In
                // a production environment you should disable AutoAcceptUntrustedCertificates
                // and reject SHA‑1 certificates.
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = "OPC/CertificateStores/Application"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = "OPC/CertificateStores/TrustedPeers"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = "OPC/CertificateStores/TrustedIssuers"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = "OPC/CertificateStores/Rejected"
                    },
                    // Accept untrusted certificates automatically.  This is convenient
                    // for development but should be set to false in production.
                    AutoAcceptUntrustedCertificates = true,
                    // Allow SHA‑1 signed certificates.  Setting this to false in
                    // production increases security by rejecting deprecated signatures.
                    RejectSHA1SignedCertificates = false
                },
                // No custom transport configurations are provided here; defaults suffice.
                TransportConfigurations = new TransportConfigurationCollection(),
                // Set transport quotas such as operation timeouts.  A shorter timeout can
                // detect unresponsive servers more quickly.
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 15000 // Timeout in milliseconds for OPC UA operations
                },
                // Client‑specific configuration.  Leave default values.
                ClientConfiguration = new ClientConfiguration()
            };

            // Validate the configuration.  This checks that certificate stores and other
            // settings are correct and will throw if something is misconfigured.
            await config.Validate(ApplicationType.Client);

            // Select the best available endpoint based on the configuration and the URL
            // provided.  The second argument (endpointUrl) should point to the server's
            // discovery endpoint.  The 'false' argument indicates that a secure channel
            // (without message security) is acceptable for endpoint selection.  The last
            // argument sets the timeout for the discovery request.
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(
                config,
                endpointUrl,
                false,
                15000);

            // Create an EndpointConfiguration object using the application configuration as
            // a template.  This object allows customizing client‑specific endpoint
            // settings.
            var endpointConfiguration = EndpointConfiguration.Create(config);

            // Create a ConfiguredEndpoint using the selected endpoint and configuration.  The
            // first parameter (null) means the endpoint is not associated with any local
            // configuration file.
            var configuredEndpoint = new ConfiguredEndpoint(
                null,
                selectedEndpoint,
                endpointConfiguration);

            // Create the session.  The parameters are: application configuration,
            // configured endpoint, a flag indicating whether to use a secure channel
            // (false means secure channel is mandatory), session name, session timeout
            // and two unused arguments (user identity and password provider).  This call
            // establishes a connection and completes the OPC UA handshake.
            var session = await Session.Create(
                config,
                configuredEndpoint,
                false,
                "MES OPC Session",
                60000,
                null,
                null);

            return session;
        }
    }
}