using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mes.Opc.Platform.Data.Db;

/// <summary>
/// EF Core entity mapping to dbo.AnalyticsSummaryDefinition.
/// A definition groups items (fields) to be computed per production run.
/// </summary>
[Table("AnalyticsSummaryDefinition")]
public sealed class AnalyticsSummaryDefinition
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SummaryId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? AppliesToMachineCode { get; set; }

    public ICollection<AnalyticsSummaryItem> Items { get; set; } = new List<AnalyticsSummaryItem>();
}
