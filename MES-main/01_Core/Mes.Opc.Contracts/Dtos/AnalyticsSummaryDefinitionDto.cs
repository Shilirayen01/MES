using System.Collections.Generic;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents an analytics summary definition returned by the API.
    /// A definition groups items (fields) to be computed per production run.
    /// </summary>
    public sealed class AnalyticsSummaryDefinitionDto
    {
        public int SummaryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? AppliesToMachineCode { get; set; }
        public List<AnalyticsSummaryItemDto> Items { get; set; } = new();
    }

    /// <summary>Payload for creating a new summary definition (POST).</summary>
    public sealed class AnalyticsSummaryDefinitionCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? AppliesToMachineCode { get; set; }
    }
}
