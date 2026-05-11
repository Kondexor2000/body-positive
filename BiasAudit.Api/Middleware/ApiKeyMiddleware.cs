using BiasAudit.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BiasAudit.Api.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
{
    private readonly ApiKeyOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPublicEndpoint(context))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_options.HeaderName, out var provided) ||
            !string.Equals(provided.ToString(), _options.Value, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers[HeaderNames.WWWAuthenticate] = "ApiKey";
            await context.Response.WriteAsJsonAsync(new { message = "Brak poprawnego klucza API." });
            return;
        }

        await next(context);
    }

    private static bool IsPublicEndpoint(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/api-test.html", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }
}
