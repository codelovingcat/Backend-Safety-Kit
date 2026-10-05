namespace BackendSafetyKit;

/// <summary>
/// Specifies the log level used for slow-request diagnostics.
/// </summary>
public enum RequestTimingLogLevel
{
    /// <summary>
    /// Logs slow requests at trace level.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Logs slow requests at debug level.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Logs slow requests at information level.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Logs slow requests at warning level.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Logs slow requests at error level.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Logs slow requests at critical level.
    /// </summary>
    Critical = 5,

    /// <summary>
    /// Disables slow-request logging.
    /// </summary>
    None = 6
}
