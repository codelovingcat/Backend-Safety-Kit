namespace BackendSafetyKit;

/// <summary>
/// Configures request timing and slow-request diagnostics.
/// </summary>
public sealed class RequestTimingOptions
{
    /// <summary>
    /// Gets or sets the duration at or above which a request is considered slow.
    /// </summary>
    public TimeSpan SlowRequestThreshold { get; set; } =
        TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets a value indicating whether slow requests are logged automatically.
    /// </summary>
    public bool EnableSlowRequestLogging { get; set; }

    /// <summary>
    /// Gets or sets the log level used for automatic slow-request logging.
    /// </summary>
    public RequestTimingLogLevel SlowRequestLogLevel { get; set; } =
        RequestTimingLogLevel.Warning;

    /// <summary>
    /// Gets or sets an optional callback invoked after request timing has been calculated.
    /// </summary>
    public Action<RequestTimingContext>? OnCompleted { get; set; }

    internal void Validate()
    {
        if (SlowRequestThreshold < TimeSpan.Zero ||
            SlowRequestThreshold > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SlowRequestThreshold),
                SlowRequestThreshold,
                "The slow-request threshold must be between zero and one day.");
        }

        if (!Enum.IsDefined(SlowRequestLogLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SlowRequestLogLevel),
                SlowRequestLogLevel,
                null);
        }

        ArgumentNullException.ThrowIfNull(OnCompleted);
    }
}
