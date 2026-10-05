using BackendSafetyKit;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class HttpSecurityMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var securityOptions = options.Value.HttpSecurity;
        securityOptions.Validate();

        ApplyRequestBodySizeLimit(context, securityOptions);

        context.Response.OnStarting(
            static state =>
            {
                var values = ((HttpContext Context, HttpSecurityOptions Options))state;
                ApplySecurityHeaders(values.Context, values.Options);
                return Task.CompletedTask;
            },
            (context, securityOptions));

        await next(context);

        if (!context.Response.HasStarted)
        {
            ApplySecurityHeaders(context, securityOptions);
        }
    }

    private static void ApplyRequestBodySizeLimit(
        HttpContext context,
        HttpSecurityOptions options)
    {
        if (!options.MaxRequestBodySize.HasValue)
        {
            return;
        }

        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (feature is null)
        {
            throw new InvalidOperationException(
                "The current ASP.NET Core server does not expose IHttpMaxRequestBodySizeFeature.");
        }

        if (feature.IsReadOnly)
        {
            throw new InvalidOperationException(
                "The ASP.NET Core server has already made the request body size limit read-only.");
        }

        feature.MaxRequestBodySize = options.MaxRequestBodySize.Value;
    }

    private static void ApplySecurityHeaders(
        HttpContext context,
        HttpSecurityOptions options)
    {
        var headers = context.Response.Headers;

        if (options.EnableNoSniffHeader &&
            !headers.ContainsKey("X-Content-Type-Options"))
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }

        if (options.EnableFrameOptionsHeader &&
            !headers.ContainsKey("X-Frame-Options"))
        {
            headers["X-Frame-Options"] = options.FrameOptions;
        }

        if (options.EnableReferrerPolicyHeader &&
            !headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = options.ReferrerPolicy;
        }

        if (options.EnableContentSecurityPolicyHeader &&
            !headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] = options.ContentSecurityPolicy;
        }
    }
}
