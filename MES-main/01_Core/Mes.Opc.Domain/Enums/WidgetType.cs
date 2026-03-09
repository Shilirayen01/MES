namespace Mes.Opc.Domain.Enums
{
    /// <summary>
    /// Enumerates the different types of widgets that can be displayed on a MES
    /// dashboard.  Each widget type corresponds to a specific UI component in
    /// the front‑end and influences how the tag data is rendered.
    /// </summary>
    public enum WidgetType
    {
        /// <summary>
        /// A simple KPI card displaying a single value with a label and optional unit.
        /// </summary>
        KpiCard,

        /// <summary>
        /// A radial gauge for visualising analog measurements within a defined range.
        /// </summary>
        Gauge,

        /// <summary>
        /// A trend chart plotting the value of a tag over time.
        /// </summary>
        Trend,

        /// <summary>
        /// A tabular view listing multiple tag values or states.
        /// </summary>
        Table,

        /// <summary>
        /// A free‑form text display used for messages, instructions or multi‑line data.
        /// </summary>
        Text,

        /// <summary>
        /// A specialised list view for displaying active alarms or events.
        /// </summary>
        AlarmList
    }
}