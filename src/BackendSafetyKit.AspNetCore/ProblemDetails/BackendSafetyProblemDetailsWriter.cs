using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BackendSafetyKit.AspNetCore.ProblemDetails;

internal sealed class BackendSafetyProblemDetailsWriter : IProblemDetailsWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public bool CanWrite(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var accept = context.HttpContext.Request.GetTypedHeaders().Accept;

        if (accept is null || accept.Count == 0)
        {
            return true;
        }

        foreach (var mediaType in accept)
        {
            if (mediaType.Quality == 0)
            {
                continue;
            }

            var type = mediaType.MediaType.Value;

            if (string.Equals(type, "application/json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "application/problem+json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "*/*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "application/*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.HttpContext.Response.ContentType =
            "application/problem+json; charset=utf-8";

        await JsonSerializer.SerializeAsync(
            context.HttpContext.Response.Body,
            context.ProblemDetails,
            JsonOptions,
            context.HttpContext.RequestAborted);
    }
}
