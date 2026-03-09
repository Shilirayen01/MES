using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public sealed class CycleRuleRepository
    {
        private readonly string _connectionString;

        public CycleRuleRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing: DefaultConnection");
        }

        public async Task<IReadOnlyList<CycleRule>> LoadActiveRulesAsync()
        {
            // V3 = NSU-aware (preferred). Uses COALESCE so the runtime works with either
            // the new *Nsu columns or the legacy columns.
            // If the database hasn't been migrated yet (columns missing), we fall back.
            const string sqlV3 = @"
                SELECT
                  MachineCode,
                  ScopeKey,
                  IsActive,
                  StartStrategy,
                  COALESCE(StartNodeIdNsu, StartNodeId) AS StartNodeId,
                  StartEdgeType,
                  StartValue,
                  EndPrimaryStrategy,
                  COALESCE(EndPrimaryNodeIdNsu, EndPrimaryNodeId) AS EndPrimaryNodeId,
                  EndPrimaryEdgeType,
                  EndFallbackStrategy,
                  COALESCE(EndFallbackNodeIdNsu, EndFallbackNodeId) AS EndFallbackNodeId,
                  COALESCE(AbortNodeIdsNsu, AbortNodeIds) AS AbortNodeIds,
                  DebounceMs,
                  MinCycleSeconds,
                  TimeoutSeconds,
                  Epsilon,
                  TargetTolerance,
                  COALESCE(ValidationNodeId_SpeedNsu, ValidationNodeId_Speed) AS ValidationSpeedNodeId,
                  ValidationSpeedMin,
                  COALESCE(ValidationNodeId_StateNsu, ValidationNodeId_State) AS ValidationStateNodeId,
                  ValidationStateValue,
                  RecoveryStrategy,
                  COALESCE(RecoveryConfirmNodeIdNsu, RecoveryConfirmNodeId) AS RecoveryConfirmNodeId,
                  RecoveryConfirmDelta,
                  RecoveryConfirmWindowSeconds
                FROM dbo.MachineCycleRule
                WHERE IsActive = 1;";

            const string sqlV2 = @"
                SELECT
                  MachineCode,
                  ScopeKey,
                  IsActive,
                  StartStrategy,
                  StartNodeId,
                  StartEdgeType,
                  StartValue,
                  EndPrimaryStrategy,
                  EndPrimaryNodeId,
                  EndPrimaryEdgeType,
                  EndFallbackStrategy,
                  EndFallbackNodeId,
                  AbortNodeIds,
                  DebounceMs,
                  MinCycleSeconds,
                  TimeoutSeconds,
                  Epsilon,
                  TargetTolerance,
                  ValidationNodeId_Speed AS ValidationSpeedNodeId,
                  ValidationSpeedMin,
                  ValidationNodeId_State AS ValidationStateNodeId,
                  ValidationStateValue,
                  RecoveryStrategy,
                  RecoveryConfirmNodeId,
                  RecoveryConfirmDelta,
                  RecoveryConfirmWindowSeconds
                FROM dbo.MachineCycleRule
                WHERE IsActive = 1;";

            const string sqlV1 = @"
            SELECT
              MachineCode,
              ScopeKey,
              IsActive,
              StartStrategy,
              StartNodeId,
              StartEdgeType,
              StartValue,
              EndPrimaryStrategy,
              EndPrimaryNodeId,
              EndPrimaryEdgeType,
              EndFallbackStrategy,
              EndFallbackNodeId,
              AbortNodeIds,
              DebounceMs,
              MinCycleSeconds,
              TimeoutSeconds,
              Epsilon,
              TargetTolerance,
              ValidationNodeId_Speed AS ValidationSpeedNodeId,
              ValidationSpeedMin,
              ValidationNodeId_State AS ValidationStateNodeId,
              ValidationStateValue
            FROM dbo.MachineCycleRule
            WHERE IsActive = 1;";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            try
            {
                var rows = await conn.QueryAsync<dynamic>(sqlV3);
                return rows.Select(r => MapRow((object)r, hasRecovery: true)).ToList();
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                // Columns missing (or table missing). Fall back to legacy schemas.
                try
                {
                    var rows = await conn.QueryAsync<dynamic>(sqlV2);
                    return rows.Select(r => MapRow((object)r, hasRecovery: true)).ToList();
                }
                catch (SqlException ex2) when (ex2.Number == 207 || ex2.Number == 208)
                {
                    var rows = await conn.QueryAsync<dynamic>(sqlV1);
                    return rows.Select(r => MapRow((object)r, hasRecovery: false)).ToList();
                }
            }
        }

        private static CycleRule MapRow(dynamic row, bool hasRecovery)
        {
            var abortRaw = (string?)row.AbortNodeIds;
            var aborts = string.IsNullOrWhiteSpace(abortRaw)
                ? Array.Empty<string>()
                : abortRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var startStrategy = ParseEnum<StartStrategy>((string?)row.StartStrategy, StartStrategy.None);
            var endPrimaryStrategy = ParseEnum<EndStrategy>((string?)row.EndPrimaryStrategy, EndStrategy.None);

            EndStrategy? endFallbackStrategy = null;
            if ((string?)row.EndFallbackStrategy is not null)
                endFallbackStrategy = ParseEnum<EndStrategy>((string?)row.EndFallbackStrategy, EndStrategy.None);

            EdgeType? startEdge = null;
            if ((string?)row.StartEdgeType is not null)
                startEdge = ParseEnum<EdgeType>((string?)row.StartEdgeType, EdgeType.Rising);

            EdgeType? endPrimaryEdge = null;
            if ((string?)row.EndPrimaryEdgeType is not null)
                endPrimaryEdge = ParseEnum<EdgeType>((string?)row.EndPrimaryEdgeType, EdgeType.Rising);

            var recoveryStrategy = hasRecovery
                ? ParseEnum<RecoveryStrategy>((string?)row.RecoveryStrategy, RecoveryStrategy.None)
                : RecoveryStrategy.None;

            string? recoveryConfirmNodeId = hasRecovery ? (string?)row.RecoveryConfirmNodeId : null;
            decimal? recoveryConfirmDelta = hasRecovery ? row.RecoveryConfirmDelta as decimal? : null;
            int? recoveryConfirmWindowSeconds = hasRecovery ? row.RecoveryConfirmWindowSeconds as int? : null;

            return new CycleRule(
                MachineCode: (string)row.MachineCode,
                ScopeKey: (string)row.ScopeKey,
                IsActive: (bool)row.IsActive,

                StartStrategy: startStrategy,
                StartNodeId: (string?)row.StartNodeId,
                StartEdgeType: startEdge,
                StartValue: (string?)row.StartValue,

                EndPrimaryStrategy: endPrimaryStrategy,
                EndPrimaryNodeId: (string?)row.EndPrimaryNodeId,
                EndPrimaryEdgeType: endPrimaryEdge,

                EndFallbackStrategy: endFallbackStrategy,
                EndFallbackNodeId: (string?)row.EndFallbackNodeId,

                AbortNodeIds: aborts,

                DebounceMs: (int)row.DebounceMs,
                MinCycleSeconds: (int)row.MinCycleSeconds,
                TimeoutSeconds: (int)row.TimeoutSeconds,

                Epsilon: row.Epsilon as decimal?,
                TargetTolerance: row.TargetTolerance as decimal?,

                ValidationSpeedNodeId: (string?)row.ValidationSpeedNodeId,
                ValidationSpeedMin: row.ValidationSpeedMin as decimal?,

                ValidationStateNodeId: (string?)row.ValidationStateNodeId,
                ValidationStateValue: (string?)row.ValidationStateValue,

                RecoveryStrategy: recoveryStrategy,
                RecoveryConfirmNodeId: recoveryConfirmNodeId,
                RecoveryConfirmDelta: recoveryConfirmDelta,
                RecoveryConfirmWindowSeconds: recoveryConfirmWindowSeconds
            );
        }

        private static T ParseEnum<T>(string? raw, T @default) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw)) return @default;
            return Enum.TryParse<T>(raw, ignoreCase: true, out var v) ? v : @default;
        }
    }
}
