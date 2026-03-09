namespace Mes.Opc.Domain.Enums
{
    /// <summary>
    /// Defines the functional role of a tag binding relative to a widget.  A
    /// widget may bind multiple tags in different roles (e.g. the main value,
    /// a status indicator or a setpoint).
    /// </summary>
    public enum BindingRole
    {
        /// <summary>
        /// The primary value displayed by a widget.
        /// </summary>
        Value,

        /// <summary>
        /// Indicates the status of a machine or process (e.g. running, stopped).
        /// </summary>
        Status,

        /// <summary>
        /// Represents a setpoint or target value associated with a process parameter.
        /// </summary>
        Setpoint,

        /// <summary>
        /// Associates a binding with an alarm or event indicator.
        /// </summary>
        Alarm
    }
}