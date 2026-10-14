namespace BackendSafetyKit;

/// <summary>
/// Stable property names for Backend Safety Kit diagnostic events.
/// </summary>
public static class BackendSafetyDiagnosticPropertyNames
{
    /// <summary>The normalized HTTP method.</summary>
    public const string HttpRequestMethod = "http.request.method";

    /// <summary>The final HTTP response status code.</summary>
    public const string HttpResponseStatusCode = "http.response.status_code";

    /// <summary>The completed request duration, in milliseconds.</summary>
    public const string HttpServerDurationMilliseconds = "http.server.duration_ms";

    /// <summary>Whether the request duration reached the configured slow-request threshold.</summary>
    public const string RequestIsSlow = "backend_safety.request.is_slow";
}
