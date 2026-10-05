namespace BackendSafetyKit;

/// <summary>
/// Configures Backend Safety Kit distributed tracing behavior.
/// </summary>
public sealed class DistributedTracingOptions
{
    /// <summary>
    /// Gets or sets the name used for activities created by Backend Safety Kit.
    /// </summary>
    public string ActivityName { get; set; } = "BackendSafetyKit.HttpRequest";

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActivityName) || ActivityName.Length > 256)
        {
            throw new ArgumentException(
                "The activity name must be non-empty and at most 256 characters.",
                nameof(ActivityName));
        }
    }
}
