namespace Mes.Opc.Platform.AnalyticsWorker.Domain;

public enum TagScope : byte
{
    Product = 0,
    Global = 1
}

public enum AggregationType : byte
{
    Min = 1,
    Max = 2,
    Avg = 3,
    LastAsOfEnd = 4,
    AsOfStart = 5,
    AsOfEnd = 6,
    Delta = 7,
    TimeWeightedAverage = 8,
    PercentInRange = 9
}

public enum SourceType : byte
{
    Tag = 0,
    Constant = 1,
    QualityIsOk = 2
}

public enum MissingDataMode : byte
{
    /// <summary>If an ingredient can't be computed, the KPI result becomes NULL.</summary>
    NullResult = 0,

    /// <summary>If missing, use 0 (can be useful for optional variables).</summary>
    UseZero = 1,

    /// <summary>If missing, fail the KPI (throws).</summary>
    Fail = 2
}

public enum QualityLogicMode : byte
{
    And = 0,
    Or = 1
}

public enum ConditionType : byte
{
    Equals = 1,
    NotEquals = 2,
    InRange = 3,
    OutOfRange = 4,
    GreaterThan = 5,
    GreaterOrEqual = 6,
    LessThan = 7,
    LessOrEqual = 8
}

public enum RuleEvaluationMode : byte
{
    AnyViolation = 1,
    AsOfEnd = 2,
    MaxValue = 3,
    MinValue = 4,
    PercentInRange = 5
}
