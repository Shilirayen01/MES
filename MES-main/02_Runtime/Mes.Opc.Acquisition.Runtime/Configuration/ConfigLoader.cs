// -------------------------------------------------------------------------------------------------
// ConfigLoader.cs
//
// Provides methods for reading OPC configuration data (endpoints, machines, tag
// mappings) from a relational database using Dapper.  Each method is documented
// to explain the purpose of the query and the structure of the returned data.
// The configuration information drives how the Worker creates endpoint runners
// and subscriptions.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;       // Provides IEnumerable<T> for enumerables
using System.Data;                       // Provides IDbConnection for database access
using System.Threading.Tasks;            // Provides Task for asynchronous methods
using Dapper;                            // Provides QueryAsync extensions for IDbConnection
using static Mes.Opc.Acquisition.Runtime.Configuration.OpcConfigModels; // Imports the record types representing configuration data

namespace Mes.Opc.Acquisition.Runtime.Configuration
{
    /// <summary>
    /// Reads OPC configuration from the database.  The configuration consists of
    /// OPC endpoints, machines and mappings between machines and OPC node
    /// identifiers.  Dapper is used to map rows directly to record types
    /// defined in <see cref="OpcConfigModels"/>.
    /// </summary>
    public class ConfigLoader
    {
        private readonly IDbConnection _db; // The database connection used to execute queries

        /// <summary>
        /// Constructs a new <see cref="ConfigLoader"/>.  The IDbConnection is
        /// expected to be provided by dependency injection and may be scoped
        /// to the lifetime of a request or operation.
        /// </summary>
        /// <param name="db">An open database connection.</param>
        public ConfigLoader(IDbConnection db)
        {
            _db = db;
        }

        /// <summary>
        /// Retrieves all active OPC endpoints from the database.  Endpoints
        /// define the network address and metadata for connecting to OPC UA
        /// servers.
        /// </summary>
        /// <returns>An enumerable of <see cref="OpcEndpointConfig"/> records representing active endpoints.</returns>
        public async Task<IEnumerable<OpcEndpointConfig>> LoadEndpointsAsync()
        {
            return await _db.QueryAsync<OpcEndpointConfig>(
                @"SELECT Id, Name, EndpointUrl
                  FROM OpcEndpoint
                  WHERE IsActive = 1");
        }

        /// <summary>
        /// Retrieves all active machines from the database.  Each machine
        /// record associates a machine code with the OPC endpoint to which it
        /// belongs.
        /// </summary>
        /// <returns>An enumerable of <see cref="MachineConfig"/> records representing active machines.</returns>
        public async Task<IEnumerable<MachineConfig>> LoadMachinesAsync()
        {
            return await _db.QueryAsync<MachineConfig>(
                @"SELECT MachineCode, OpcEndpointId
                  FROM Machine
                  WHERE IsActive = 1");
        }

        /// <summary>
        /// Retrieves all active tag mappings from the database.  Each mapping
        /// associates a machine code with a specific OPC UA node identifier.
        /// </summary>
        /// <returns>An enumerable of <see cref="TagMappingConfig"/> records representing active mappings.</returns>
        public async Task<IEnumerable<TagMappingConfig>> LoadTagMappingsAsync()
        {
            return await _db.QueryAsync<TagMappingConfig>(
                @"SELECT MachineCode, COALESCE(OpcNodeIdNsu, OpcNodeId) AS OpcNodeId
                  FROM MachineTagMapping
                  WHERE IsActive = 1");
        }
    }
}