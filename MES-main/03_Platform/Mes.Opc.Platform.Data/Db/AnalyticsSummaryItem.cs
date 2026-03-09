using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mes.Opc.Platform.Data.Db;

/// <summary>
/// EF Core entity mapping to dbo.AnalyticsSummaryItem.
/// Each item defines one metric field computed per production run.
/// </summary>
[Table("AnalyticsSummaryItem")]
public sealed class AnalyticsSummaryItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ItemId { get; set; }

    public int SummaryId { get; set; }

    [Required, MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>0 = Tag, 1 = Constant (stored as byte in DB).</summary>
    public byte SourceType { get; set; }

    [MaxLength(256)]
    public string? TagNodeId { get; set; }

    /// <summary>Nullable byte: 0=Run, 1=Last, 2=LookbackWindow.</summary>
    public byte? Scope { get; set; }

    /// <summary>Nullable byte: 0=Sum, 1=Average, 2=Min, 3=Max, 4=Last.</summary>
    public byte? Aggregation { get; set; }

    public bool? IsCumulative { get; set; }

    public double? ConstantValue { get; set; }

    public int? LookbackMinutes { get; set; }

    public int? MaxGapSeconds { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    // Navigation property
    [ForeignKey(nameof(SummaryId))]
    public AnalyticsSummaryDefinition? Summary { get; set; }
}
