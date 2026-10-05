namespace BackendSafetyKit;

/// <summary>
/// Specifies the log level used for structured HTTP request completion events.
/// </summary>
public enum RequestLoggingLogLevel
{
    /// <summary>
    /// Logs the event at trace level.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Logs the event at debug level.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Logs the event at information level.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Logs the event at warning level.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Logs the event at error level.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Logs the event at critical level.
    /// </summary>
    Critical = 5,

    /// <summary>
    /// Disables the event category.
    /// </summary>
    None = 6
}
