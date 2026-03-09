using System.Collections.ObjectModel;

namespace Mes.Opc.Platform.AnalyticsWorker.Domain;

public sealed class MachineConfig
{
    public required string MachineCode { get; init; }
    public IReadOnlyList<KpiDefinition> Kpis { get; init; } = Array.Empty<KpiDefinition>();
    public SummaryDefinition? Summary { get; init; }
    public QualityRuleSet? QualityRuleSet { get; init; }
}

public sealed class KpiDefinition
{
    public required int KpiId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required string Expression { get; init; } // Example: "(RT/PlannedTime) * (AvgSpeed/NominalSpeed) * FTQ"
    public string? Unit { get; init; }
    public IReadOnlyList<KpiVariable> Variables { get; init; } = Array.Empty<KpiVariable>();
}

public sealed class KpiVariable
{
    public required int VariableId { get; init; }
    public required string VariableName { get; init; }
    public required SourceType SourceType { get; init; }

    // Tag source
    public string? TagNodeId { get; init; }
    public TagScope? Scope { get; init; }
    public AggregationType? Aggregation { get; init; }
    public bool? IsCumulative { get; init; }

    // Common
    public int? LookbackMinutes { get; init; }
    public int? MaxGapSeconds { get; init; }
    public MissingDataMode MissingDataMode { get; init; } = MissingDataMode.NullResult;

    // For PercentInRange
    public double? RangeMin { get; init; }
    public double? RangeMax { get; init; }
    public double? PercentThreshold { get; init; }

    // Constant source
    public double? ConstantValue { get; init; }
}

public sealed class SummaryDefinition
{
    public required int SummaryId { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public IReadOnlyList<SummaryItem> Items { get; init; } = Array.Empty<SummaryItem>();
}

public sealed class SummaryItem
{
    public required int ItemId { get; init; }
    public required string FieldName { get; init; }
    public required SourceType SourceType { get; init; }

    public string? TagNodeId { get; init; }
    public TagScope? Scope { get; init; }
    public AggregationType? Aggregation { get; init; }
    public bool? IsCumulative { get; init; }

    public int? LookbackMinutes { get; init; }
    public int? MaxGapSeconds { get; init; }
    public double? ConstantValue { get; init; }

    public string? Unit { get; init; }
}

public sealed class QualityRuleSet
{
    public required int RuleSetId { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required QualityLogicMode LogicMode { get; init; }
    public IReadOnlyList<QualityRule> Rules { get; init; } = Array.Empty<QualityRule>();
}

public sealed class QualityRule
{
    public required int RuleId { get; init; }
    public required string Code { get; init; }
    public required bool IsActive { get; init; }
    public required string TagNodeId { get; init; }
    public required TagScope Scope { get; init; }
    public required ConditionType Condition { get; init; }
    public required RuleEvaluationMode EvaluationMode { get; init; }

    public double? ExpectedValue { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }

    public double? PercentThreshold { get; init; }
    public int? LookbackMinutes { get; init; }
    public int? MaxGapSeconds { get; init; }
}
