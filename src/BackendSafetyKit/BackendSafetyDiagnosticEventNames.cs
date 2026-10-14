namespace BackendSafetyKit;

/// <summary>
/// Stable event names emitted by Backend Safety Kit.
/// </summary>
public static class BackendSafetyDiagnosticEventNames
{
    /// <summary>A completed HTTP request without a client or server error status.</summary>
    public const string HttpRequestCompleted = "BackendSafetyKit.HttpRequest.Completed";

    /// <summary>An HTTP request completed with a 4xx status code.</summary>
    public const string HttpRequestClientError = "BackendSafetyKit.HttpRequest.ClientError";

    /// <summary>An HTTP request completed with a 5xx status code.</summary>
    public const string HttpRequestServerError = "BackendSafetyKit.HttpRequest.ServerError";
}
