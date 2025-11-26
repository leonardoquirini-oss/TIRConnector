using Microsoft.Extensions.Options;
using TIRConnector.API.Configuration;

namespace TIRConnector.API.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeySettings _apiKeySettings;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<ApiKeySettings> apiKeySettings,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _apiKeySettings = apiKeySettings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check, Swagger, and admin static files
        if (context.Request.Path.StartsWithSegments("/api/health") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/admin"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("API Key missing from request: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "API Key is missing"
            });
            return;
        }

        var validKeys = _apiKeySettings.GetKeys().ToList();
        if (!validKeys.Contains(extractedApiKey.ToString()))
        {
            _logger.LogWarning("Invalid API Key attempted: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Invalid API Key"
            });
            return;
        }

        await _next(context);
    }
}
