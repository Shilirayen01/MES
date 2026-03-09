namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents one metric field in an analytics summary definition.
    /// Maps to dbo.AnalyticsSummaryItem.
    /// </summary>
    public sealed class AnalyticsSummaryItemDto
    {
        public int ItemId { get; set; }
        public int SummaryId { get; set; }
        public string FieldName { get; set; } = string.Empty;

        /// <summary>"Tag" or "Constant".</summary>
        public string SourceType { get; set; } = "Tag";

        public string? TagNodeId { get; set; }

        /// <summary>"Run", "Last", or "LookbackWindow".</summary>
        public string? Scope { get; set; }

        /// <summary>"Sum", "Average", "Min", "Max", or "Last".</summary>
        public string? Aggregation { get; set; }

        public bool? IsCumulative { get; set; }
        public double? ConstantValue { get; set; }
        public int? LookbackMinutes { get; set; }
        public int? MaxGapSeconds { get; set; }
        public string? Unit { get; set; }
    }

    /// <summary>Payload for creating a new summary item (POST).</summary>
    public sealed class AnalyticsSummaryItemCreateDto
    {
        public string FieldName { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Tag";
        public string? TagNodeId { get; set; }
        public string? Scope { get; set; }
        public string? Aggregation { get; set; }
        public bool? IsCumulative { get; set; }
        public double? ConstantValue { get; set; }
        public int? LookbackMinutes { get; set; }
        public int? MaxGapSeconds { get; set; }
        public string? Unit { get; set; }
    }
}
