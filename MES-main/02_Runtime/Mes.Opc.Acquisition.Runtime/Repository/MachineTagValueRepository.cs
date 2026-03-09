// -------------------------------------------------------------------------------------------------
// MachineTagValueRepository.cs
//
// Provides an abstraction over the database operations required to persist
// batches of MachineTagValue entities.  This repository encapsulates the
// usage of SqlClient and Dapper to execute a stored procedure with a
// table‑valued parameter.  Each section of the code is documented to
// illustrate how the batching works.
// -------------------------------------------------------------------------------------------------

using System;                                // Provides basic types like InvalidOperationException
using System.Collections.Generic;            // Provides IEnumerable<T> for collections of values
using System.Data;                           // Provides IDbConnection and DataTable
using System.Threading.Tasks;                // Provides Task for asynchronous methods
using Mes.Opc.Acquisition.Runtime.Persistence; // Imports the MachineTagValue model
using Microsoft.Data.SqlClient;             // Provides SqlConnection and SqlCommand
using Microsoft.Extensions.Configuration;    // Provides IConfiguration for reading connection strings

namespace Mes.Opc.Acquisition.Runtime.Repository
{
    /// <summary>
    /// Handles persistence of MachineTagValue records by executing a stored procedure on
    /// a SQL Server database.  The stored procedure accepts a table‑valued parameter
    /// of type dbo.MachineTagValue_TVP.  The details of the TVP and stored
    /// procedure should be created ahead of time in the database.
    /// </summary>
    public class MachineTagValueRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the repository.  Reads the connection
        /// string from application configuration.
        /// </summary>
        /// <param name="configuration">
        /// The configuration object used to retrieve the connection string named
        /// "DefaultConnection".
        /// </param>
        public MachineTagValueRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing");
        }

        /// <summary>
        /// Inserts a batch of MachineTagValue entities into the database by invoking
        /// the dbo.InsertMachineTagValues_Batch stored procedure with a
        /// table‑valued parameter.
        /// </summary>
        /// <param name="values">The collection of values to insert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InsertBatchAsync(IEnumerable<MachineTagValue> values)
        {
            // Backward compatible insert:
            // 1) Try V2 (includes ProductionRunId) if the DB has been upgraded.
            // 2) Fallback to legacy V1 if the V2 proc/TVP isn't available.
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                var v2Table = ToDataTableV2(values);
                using var v2Command = new SqlCommand("dbo.InsertMachineTagValues_Batch_V2", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                var p2 = v2Command.Parameters.AddWithValue("@Values", v2Table);
                p2.SqlDbType = SqlDbType.Structured;
                p2.TypeName = "dbo.MachineTagValue_TVP_V2";
                await v2Command.ExecuteNonQueryAsync();
                return;
            }
            catch (SqlException ex) when (
                ex.Number == 2812 /* could not find stored procedure */ ||
                ex.Number == 2715 /* could not find type */ ||
                ex.Number == 208  /* invalid object */)
            {
                // DB not yet upgraded; fallback to V1.
            }

            var table = ToDataTable(values);
            using var command = new SqlCommand("dbo.InsertMachineTagValues_Batch", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            var param = command.Parameters.AddWithValue("@Values", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "dbo.MachineTagValue_TVP";
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Converts a collection of MachineTagValue entities into a DataTable with the
        /// appropriate schema for the table‑valued parameter.  Each column
        /// corresponds to a property on MachineTagValue.
        /// </summary>
        /// <param name="values">The collection of values to convert.</param>
        /// <returns>A populated DataTable.</returns>
        private static DataTable ToDataTable(IEnumerable<MachineTagValue> values)
        {
            var table = new DataTable();
            table.Columns.Add("MachineCode", typeof(string));
            table.Columns.Add("OpcNodeId", typeof(string));
            table.Columns.Add("Value", typeof(string));
            table.Columns.Add("StatusCode", typeof(string));
            table.Columns.Add("SourceTimestamp", typeof(DateTime));
            foreach (var v in values)
            {
                table.Rows.Add(
                    v.MachineCode,
                    v.OpcNodeId,
                    v.Value,
                    v.StatusCode,
                    v.SourceTimestamp);
            }
            return table;
        }

        private static DataTable ToDataTableV2(IEnumerable<MachineTagValue> values)
        {
            var table = new DataTable();
            table.Columns.Add("ProductionRunId", typeof(Guid));
            table.Columns.Add("MachineCode", typeof(string));
            table.Columns.Add("OpcNodeId", typeof(string));
            table.Columns.Add("Value", typeof(string));
            table.Columns.Add("StatusCode", typeof(string));
            table.Columns.Add("SourceTimestamp", typeof(DateTime));

            foreach (var v in values)
            {
                table.Rows.Add(
                    (object?)v.ProductionRunId ?? DBNull.Value,
                    v.MachineCode,
                    v.OpcNodeId,
                    v.Value,
                    v.StatusCode,
                    v.SourceTimestamp);
            }
            return table;
        }
    }
}