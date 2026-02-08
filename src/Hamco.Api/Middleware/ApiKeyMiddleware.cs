using Hamco.Core.Services;
using Microsoft.AspNetCore.Authentication;

namespace Hamco.Api.Middleware;

/// <summary>
/// Middleware for API key authentication.
/// Validates X-API-Key header and sets HttpContext.User for authorized requests.
/// </summary>
/// <remarks>
/// API Key Authentication Flow:
///   1. Request arrives with X-API-Key header
///   2. Middleware extracts key from header
///   3. Validates key against database (IApiKeyService)
///   4. If valid: Sets HttpContext.User with API key claims
///   5. If invalid: Returns 401 Unauthorized (short-circuit)
///   6. If no key: Passes to next middleware (JWT auth can still work)
/// 
/// Why middleware instead of authentication handler?
///   - Simpler implementation
///   - Direct integration with existing auth
///   - No need for custom authentication scheme
///   - Works alongside JWT (both can work!)
/// 
/// Security considerations:
///   - Only checks IsActive = true keys
///   - Uses BCrypt verification (slow, secure)
///   - No timing attacks (BCrypt handles constant-time comparison)
///   - Expired keys rejected immediately
/// </remarks>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes HTTP request for API key authentication.
    /// </summary>
    /// <param name="context">HTTP context for the request.</param>
    /// <param name="apiKeyService">Service for validating API keys.</param>
    /// <returns>Task representing async operation.</returns>
    /// <remarks>
    /// Authentication Order (Program.cs middleware pipeline):
    ///   1. UseAuthentication() - JWT middleware checks Authorization header
    ///   2. UseApiKeyAuthentication() - This middleware checks X-API-Key header
    ///   3. UseAuthorization() - Policy checks (e.g., [Authorize(Roles="Admin")])
    /// 
    /// Why this order?
    ///   - JWT is faster (no database lookup)
    ///   - JWT is more common (most requests use it)
    ///   - API keys are for automation (less frequent)
    ///   - If JWT valid, skip API key check (performance)
    /// 
    /// Both can work in same app:
    ///   - Web UI users: JWT tokens
    ///   - Bots/automation: API keys
    ///   - Same authorization policies work for both!
    /// </remarks>
    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeyService)
    {
        // Only process if no user is already authenticated
        // (JWT auth runs before this middleware, so if JWT valid, skip API key check)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Check for X-API-Key header
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            // No API key header, continue to next middleware
            // (JWT auth or anonymous access)
            await _next(context);
            return;
        }

        var apiKey = apiKeyHeader.ToString();
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("API key header present but empty");
            await WriteUnauthorizedResponse(context, "API key is empty.");
            return;
        }

        // Validate the API key
        var principal = await apiKeyService.ValidateKeyAsync(apiKey);

        if (principal == null)
        {
            _logger.LogWarning("Invalid or revoked API key attempted: {Prefix}", 
                apiKey.Length > 8 ? apiKey[..8] : "[short]");
            await WriteUnauthorizedResponse(context, "Invalid or revoked API key.");
            return;
        }

        // API key is valid - set the user principal
        context.User = principal;
        
        _logger.LogInformation("API key authenticated: {KeyName}", 
            principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "unknown");

        // Continue to next middleware
        await _next(context);
    }

    /// <summary>
    /// Writes a 401 Unauthorized response.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <param name="message">Error message.</param>
    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        
        var response = System.Text.Json.JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(response);
    }
}

/// <summary>
/// Extension methods for registering API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the pipeline.
    /// Must be called AFTER UseAuthentication() to work alongside JWT.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Application builder for chaining.</returns>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}
