using Dapper;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Microsoft.Extensions.Logging;

namespace Mes.Opc.Platform.AnalyticsWorker.Data;

public sealed class ConfigRepository
{
    private readonly SqlConnectionFactory _factory;
    private readonly ILogger<ConfigRepository> _log;

    public ConfigRepository(SqlConnectionFactory factory, ILogger<ConfigRepository> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<MachineConfig> LoadMachineConfigAsync(string machineCode, CancellationToken ct)
    {
        using var conn = _factory.Create();

        // KPI definitions
        const string kpiSql = @"
            SELECT KpiId, Code, Name, IsActive, Expression, Unit
            FROM AnalyticsKpiDefinition
            WHERE IsActive = 1
              AND (AppliesToMachineCode IS NULL OR AppliesToMachineCode = @MachineCode)
            ORDER BY KpiId;";

        var kpis = (await conn.QueryAsync<KpiDefinitionRow>(new CommandDefinition(
            kpiSql, new { MachineCode = machineCode }, cancellationToken: ct))).AsList();

        // KPI variables
        const string varSql = @"
            SELECT VariableId, KpiId, VariableName, SourceType, TagNodeId, Scope, Aggregation, IsCumulative,
                   ConstantValue, LookbackMinutes, MaxGapSeconds, MissingDataMode,
                   RangeMin, RangeMax, PercentThreshold
            FROM AnalyticsKpiVariable
            WHERE KpiId IN @KpiIds
            ORDER BY KpiId, VariableId;";

        var kpiIds = kpis.Select(k => k.KpiId).ToArray();
        var vars = kpiIds.Length == 0
            ? new List<KpiVariableRow>()
            : (await conn.QueryAsync<KpiVariableRow>(new CommandDefinition(
                varSql, new { KpiIds = kpiIds }, cancellationToken: ct))).AsList();

        var varsByKpi = vars.GroupBy(v => v.KpiId).ToDictionary(g => g.Key, g => g.ToList());

        var kpiModels = kpis.Select(k => new KpiDefinition
        {
            KpiId = k.KpiId,
            Code = k.Code,
            Name = k.Name,
            IsActive = k.IsActive,
            Expression = k.Expression,
            Unit = k.Unit,
            Variables = varsByKpi.TryGetValue(k.KpiId, out var list)
                ? list.Select(MapKpiVar).ToList()
                : new List<KpiVariable>()
        }).ToList();

        // Summary definition (one active per machine)
        const string summaryDefSql = @"
            SELECT TOP(1) SummaryId, Name, IsActive
            FROM AnalyticsSummaryDefinition
            WHERE IsActive = 1
              AND (AppliesToMachineCode IS NULL OR AppliesToMachineCode = @MachineCode)
            ORDER BY SummaryId;";

        var summaryDef = await conn.QueryFirstOrDefaultAsync<SummaryDefRow>(new CommandDefinition(
            summaryDefSql, new { MachineCode = machineCode }, cancellationToken: ct));

        SummaryDefinition? summary = null;
        if (summaryDef is not null)
        {
                        const string itemsSql = @"
            SELECT ItemId, SummaryId, FieldName, SourceType, TagNodeId, Scope, Aggregation, IsCumulative,
                   ConstantValue, LookbackMinutes, MaxGapSeconds, Unit
            FROM AnalyticsSummaryItem
            WHERE SummaryId = @SummaryId
            ORDER BY ItemId;";

            var items = (await conn.QueryAsync<SummaryItemRow>(new CommandDefinition(
                itemsSql, new { SummaryId = summaryDef.SummaryId }, cancellationToken: ct))).AsList();

            summary = new SummaryDefinition
            {
                SummaryId = summaryDef.SummaryId,
                Name = summaryDef.Name,
                IsActive = summaryDef.IsActive,
                Items = items.Select(MapSummaryItem).ToList()
            };
        }

        // Quality rule set
        const string ruleSetSql = @"
SELECT TOP(1) RuleSetId, Name, IsActive, LogicMode
FROM AnalyticsQualityRuleSet
WHERE IsActive = 1
  AND (AppliesToMachineCode IS NULL OR AppliesToMachineCode = @MachineCode)
ORDER BY RuleSetId;";

        var ruleSet = await conn.QueryFirstOrDefaultAsync<RuleSetRow>(new CommandDefinition(
            ruleSetSql, new { MachineCode = machineCode }, cancellationToken: ct));

        QualityRuleSet? qrs = null;
        if (ruleSet is not null)
        {
            const string rulesSql = @"
SELECT RuleId, RuleSetId, Code, IsActive, TagNodeId, Scope, ConditionType, EvaluationMode,
       ExpectedValue, MinValue, MaxValue, PercentThreshold, LookbackMinutes, MaxGapSeconds
FROM AnalyticsQualityRule
WHERE RuleSetId = @RuleSetId
  AND IsActive = 1
ORDER BY RuleId;";

            var rules = (await conn.QueryAsync<RuleRow>(new CommandDefinition(
                rulesSql, new { RuleSetId = ruleSet.RuleSetId }, cancellationToken: ct))).AsList();

            qrs = new QualityRuleSet
            {
                RuleSetId = ruleSet.RuleSetId,
                Name = ruleSet.Name,
                IsActive = ruleSet.IsActive,
                LogicMode = (QualityLogicMode)ruleSet.LogicMode,
                Rules = rules.Select(MapRule).ToList()
            };
        }

        return new MachineConfig
        {
            MachineCode = machineCode,
            Kpis = kpiModels,
            Summary = summary,
            QualityRuleSet = qrs
        };
    }

    private static KpiVariable MapKpiVar(KpiVariableRow v) => new()
    {
        VariableId = v.VariableId,
        VariableName = v.VariableName,
        SourceType = (SourceType)v.SourceType,

        TagNodeId = v.TagNodeId,
        Scope = v.Scope is null ? null : (TagScope)v.Scope.Value,
        Aggregation = v.Aggregation is null ? null : (AggregationType)v.Aggregation.Value,
        IsCumulative = v.IsCumulative,

        ConstantValue = v.ConstantValue,

        LookbackMinutes = v.LookbackMinutes,
        MaxGapSeconds = v.MaxGapSeconds,
        MissingDataMode = (MissingDataMode)v.MissingDataMode,

        RangeMin = v.RangeMin,
        RangeMax = v.RangeMax,
        PercentThreshold = v.PercentThreshold
    };

    private static SummaryItem MapSummaryItem(SummaryItemRow s) => new()
    {
        ItemId = s.ItemId,
        FieldName = s.FieldName,
        SourceType = (SourceType)s.SourceType,

        TagNodeId = s.TagNodeId,
        Scope = s.Scope is null ? null : (TagScope)s.Scope.Value,
        Aggregation = s.Aggregation is null ? null : (AggregationType)s.Aggregation.Value,
        IsCumulative = s.IsCumulative,

        ConstantValue = s.ConstantValue,
        LookbackMinutes = s.LookbackMinutes,
        MaxGapSeconds = s.MaxGapSeconds,
        Unit = s.Unit
    };

    private static QualityRule MapRule(RuleRow r) => new()
    {
        RuleId = r.RuleId,
        Code = r.Code,
        IsActive = r.IsActive,
        TagNodeId = r.TagNodeId,
        Scope = (TagScope)r.Scope,
        Condition = (ConditionType)r.ConditionType,
        EvaluationMode = (RuleEvaluationMode)r.EvaluationMode,

        ExpectedValue = r.ExpectedValue,
        MinValue = r.MinValue,
        MaxValue = r.MaxValue,
        PercentThreshold = r.PercentThreshold,
        LookbackMinutes = r.LookbackMinutes,
        MaxGapSeconds = r.MaxGapSeconds
    };

    private sealed record KpiDefinitionRow(int KpiId, string Code, string Name, bool IsActive, string Expression, string? Unit);
    private sealed record KpiVariableRow(
        int VariableId,
        int KpiId,
        string VariableName,
        byte SourceType,
        string? TagNodeId,
        byte? Scope,
        byte? Aggregation,
        bool? IsCumulative,
        double? ConstantValue,
        int? LookbackMinutes,
        int? MaxGapSeconds,
        byte MissingDataMode,
        double? RangeMin,
        double? RangeMax,
        double? PercentThreshold);

    private sealed record SummaryDefRow(int SummaryId, string Name, bool IsActive);
    private sealed record SummaryItemRow(
        int ItemId,
        int SummaryId,
        string FieldName,
        byte SourceType,
        string? TagNodeId,
        byte? Scope,
        byte? Aggregation,
        bool? IsCumulative,
        double? ConstantValue,
        int? LookbackMinutes,
        int? MaxGapSeconds,
        string? Unit);

    private sealed record RuleSetRow(int RuleSetId, string Name, bool IsActive, byte LogicMode);
    private sealed record RuleRow(
        int RuleId,
        int RuleSetId,
        string Code,
        bool IsActive,
        string TagNodeId,
        byte Scope,
        byte ConditionType,
        byte EvaluationMode,
        double? ExpectedValue,
        double? MinValue,
        double? MaxValue,
        double? PercentThreshold,
        int? LookbackMinutes,
        int? MaxGapSeconds);
}
