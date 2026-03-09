using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Mes.Opc.Acquisition.Runtime.Repository
{
    public interface IProductionRunRepository
    {
        Task TryInsertStartedAsync(Guid runId, string machineCode, string scopeKey, DateTime startTsUtc);
        Task TryUpdateEndAsync(Guid runId, DateTime endTsUtc, string status, string reason);
    }

    /// <summary>
    /// Fail-safe production run repository. If the ProductionRun table is not deployed yet,
    /// operations are ignored (no throw) to keep ingestion running.
    /// </summary>
    public sealed class ProductionRunRepository : IProductionRunRepository
    {
        private readonly string _connectionString;

        public ProductionRunRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing");
        }

        public async Task TryInsertStartedAsync(Guid runId, string machineCode, string scopeKey, DateTime startTsUtc)
        {
            const string sql = @"
                IF OBJECT_ID('ProductionRun','U') IS NULL RETURN;
                IF EXISTS(SELECT 1 FROM ProductionRun WHERE RunId=@RunId) RETURN;
                INSERT INTO ProductionRun(RunId, MachineCode, ScopeKey, StartTs, Status)
                VALUES(@RunId, @MachineCode, @ScopeKey, @StartTs, 'Running');";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.ExecuteAsync(sql, new { RunId = runId, MachineCode = machineCode, ScopeKey = scopeKey, StartTs = startTsUtc });
            }
            catch
            {
                // ignore
            }
        }

        public async Task TryUpdateEndAsync(Guid runId, DateTime endTsUtc, string status, string reason)
        {
            const string sql = @"
IF OBJECT_ID('ProductionRun','U') IS NULL RETURN;
UPDATE ProductionRun
SET EndTs=@EndTs, Status=@Status, EndReason=@Reason
WHERE RunId=@RunId;";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.ExecuteAsync(sql, new { RunId = runId, EndTs = endTsUtc, Status = status, Reason = reason });
            }
            catch
            {
                // ignore
            }
        }
    }
}
