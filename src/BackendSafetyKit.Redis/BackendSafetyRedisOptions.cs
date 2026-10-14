namespace BackendSafetyKit.Redis;

/// <summary>
/// Configures the optional Redis Streams diagnostic sink.
/// </summary>
public sealed class BackendSafetyRedisOptions
{
    /// <summary>
    /// Gets or sets the Redis stream key used for safe diagnostic events.
    /// </summary>
    public string StreamKey { get; set; } = "backend-safety:diagnostics";

    /// <summary>
    /// Gets or sets the maximum number of pending events held in memory before the oldest event is dropped.
    /// </summary>
    public int QueueCapacity { get; set; } = 512;

    /// <summary>
    /// Gets or sets the approximate maximum number of entries retained in the Redis stream.
    /// </summary>
    public int MaxStreamLength { get; set; } = 10000;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(StreamKey) || StreamKey.Length > 256)
        {
            throw new ArgumentException(
                "The Redis stream key must be non-empty and at most 256 characters.",
                nameof(StreamKey));
        }

        if (QueueCapacity is < 1 or > 100000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueCapacity),
                QueueCapacity,
                "The Redis diagnostic queue capacity must be between 1 and 100000.");
        }

        if (MaxStreamLength is < 1 or > 10000000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxStreamLength),
                MaxStreamLength,
                "The Redis stream length must be between 1 and 10000000.");
        }
    }
}
