using Hamco.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Hamco.Core.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure authentication services.
/// Provides a clean, reusable way to set up JWT authentication.
/// </summary>
/// <remarks>
/// Extension methods in C#:
///   - Add functionality to existing types without modifying them
///   - Defined in static classes
///   - First parameter uses 'this' keyword
///   - Called as if they're instance methods
/// 
/// Example usage:
///   services.AddAuthServices(jwtKey, jwtIssuer, jwtAudience);
///   
/// This looks like an instance method on IServiceCollection, but it's actually:
///   ServiceCollectionExtensions.AddAuthServices(services, jwtKey, ...);
/// 
/// Why use extension methods?
///   ✅ Cleaner code (fluent API style)
///   ✅ Reusable configuration (DRY principle)
///   ✅ Discoverability (IntelliSense shows them)
///   ✅ Separation of concerns (auth config separate from Program.cs)
/// 
/// 'static class' in C#:
///   - Cannot be instantiated (no 'new')
///   - Can only contain static members
///   - Often used for extension methods and utility functions
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures JWT authentication and authorization services.
    /// Registers password hasher, JWT service, and JWT Bearer authentication.
    /// </summary>
    /// <param name="services">
    /// The IServiceCollection to add services to (Dependency Injection container).
    /// </param>
    /// <param name="jwtSecret">
    /// Secret key for signing JWT tokens. Must be at least 32 characters.
    /// </param>
    /// <param name="jwtIssuer">
    /// Token issuer identifier (e.g., "hamco-api").
    /// </param>
    /// <param name="jwtAudience">
    /// Token audience identifier (e.g., "hamco-client").
    /// </param>
    /// <param name="expirationMinutes">
    /// Token lifetime in minutes. Default is 60 (1 hour).
    /// </param>
    /// <returns>
    /// The same IServiceCollection for method chaining.
    /// </returns>
    /// <remarks>
    /// This method is an extension method:
    ///   - 'this' keyword on first parameter makes it an extension
    ///   - Can be called as: services.AddAuthServices(...)
    ///   - Instead of: ServiceCollectionExtensions.AddAuthServices(services, ...)
    /// 
    /// Dependency Injection (DI) in ASP.NET Core:
    ///   IServiceCollection is the DI container configuration
    ///   Services registered here can be injected into controllers, etc.
    /// 
    /// Service lifetimes:
    ///   - Singleton: One instance for entire application (shared)
    ///   - Scoped: One instance per HTTP request (common for DbContext)
    ///   - Transient: New instance every time (rare, expensive)
    /// 
    /// We use Singleton for services because:
    ///   - Thread-safe (no mutable state)
    ///   - Lightweight (no resources to dispose)
    ///   - Better performance (avoid creating instances)
    /// 
    /// Return value 'IServiceCollection':
    ///   Allows method chaining (fluent API):
    ///   services.AddAuthServices(...)
    ///           .AddDbContext(...)
    ///           .AddControllers();
    /// </remarks>
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        string jwtSecret,
        string jwtIssuer,
        string jwtAudience,
        int expirationMinutes = 60)
    {
        // Step 1: Register password hashing service
        // AddSingleton: One instance shared across entire application
        // Generic syntax: AddSingleton<Interface, Implementation>
        //   - IPasswordHasher: What consumers ask for (interface)
        //   - PasswordHasher: What they get (concrete class)
        // 
        // When a controller requests IPasswordHasher:
        //   public AuthController(IPasswordHasher passwordHasher) { ... }
        // DI container provides the PasswordHasher instance
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        
        // Step 2: Register JWT service with factory method
        // AddSingleton with factory: AddSingleton<Interface>(sp => new Implementation(...))
        //   - 'sp' is IServiceProvider (access to other registered services)
        //   - Lambda expression: sp => ... (function that creates instance)
        //   - Can use 'sp' to inject dependencies if needed
        // 
        // Why factory method instead of AddSingleton<IJwtService, JwtService>()?
        //   JwtService constructor needs parameters (jwtSecret, jwtIssuer, etc.)
        //   Factory method lets us pass those parameters
        // 
        // The 'sp =>' parameter is unused here (we don't need other services)
        // But it's required for the factory signature
        services.AddSingleton<IJwtService>(sp => 
            new JwtService(jwtSecret, jwtIssuer, jwtAudience, expirationMinutes));

        // Step 3: Configure JWT authentication middleware
        // Encoding.UTF8.GetBytes() converts secret string to byte array
        // Stored in 'key' variable for reuse (used in token validation)
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        // AddAuthentication() sets up authentication middleware
        // 'options =>' is a lambda that configures default schemes
        services.AddAuthentication(options =>
        {
            // Default scheme: Used when no scheme specified in [Authorize]
            // JwtBearerDefaults.AuthenticationScheme = "Bearer"
            // 
            // When controller has [Authorize] (no specific scheme):
            //   ASP.NET Core uses this default scheme (JWT Bearer)
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            
            // Challenge scheme: Used when authentication fails (401 Unauthorized)
            // Same as authenticate scheme (use JWT Bearer)
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        // AddJwtBearer() configures JWT-specific authentication
        // Chained after AddAuthentication() (fluent API)
        .AddJwtBearer(options =>
        {
            // RequireHttpsMetadata: Require HTTPS for metadata endpoint
            // Set to false for development (allows HTTP)
            // ⚠️ MUST be true in production (HTTPS only)
            options.RequireHttpsMetadata = false;
            
            // SaveToken: Store token in AuthenticationProperties
            // Allows accessing token later: await HttpContext.GetTokenAsync("access_token")
            // Useful for token refresh, logging, etc.
            options.SaveToken = true;
            
            // TokenValidationParameters: How to validate incoming tokens
            // Same parameters used in JwtService.ValidateToken()
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,  // Verify signature
                IssuerSigningKey = new SymmetricSecurityKey(key),  // Key for verification
                ValidateIssuer = true,            // Check issuer claim
                ValidIssuer = jwtIssuer,          // Expected issuer value
                ValidateAudience = true,          // Check audience claim
                ValidAudience = jwtAudience,      // Expected audience value
                ValidateLifetime = true,          // Check expiration
                ClockSkew = TimeSpan.Zero         // No grace period for expiration
            };

            // Events: Customize authentication behavior
            // JwtBearerEvents allows hooking into authentication pipeline
            options.Events = new JwtBearerEvents
            {
                // OnMessageReceived: Customize how token is extracted from request
                // Default: Reads "Authorization: Bearer {token}" header
                // We add: Also read from "AuthToken" cookie
                // 
                // 'context =>' is a lambda (async function)
                // MessageReceivedContext contains request information
                OnMessageReceived = context =>
                {
                    // Check if Authorization header provided token
                    // context.Token is populated by default handler (from header)
                    if (string.IsNullOrEmpty(context.Token))
                    {
                        // No header token, try reading from cookie
                        // context.Request.Cookies["AuthToken"] reads cookie
                        // If cookie exists, use its value as token
                        // 
                        // This enables browser-based authentication:
                        //   1. Login sets HttpOnly cookie: AuthToken={jwt}
                        //   2. Browser sends cookie with every request
                        //   3. This code extracts token from cookie
                        //   4. Authentication proceeds normally
                        // 
                        // Benefits of cookie-based tokens:
                        //   ✅ Automatic (browser sends cookies)
                        //   ✅ HttpOnly flag prevents JavaScript access (XSS protection)
                        //   ✅ Works with traditional web apps (not just SPAs)
                        // 
                        // Drawbacks:
                        //   ⚠️ CSRF attacks possible (need CSRF tokens)
                        //   ⚠️ SameSite cookie restrictions
                        context.Token = context.Request.Cookies["AuthToken"];
                    }
                    
                    // Return completed task (async method requirement)
                    // Task.CompletedTask = already-finished async operation
                    return Task.CompletedTask;
                }
            };
        });

        // Step 4: Add authorization services
        // Required for [Authorize] attribute to work
        // Sets up authorization policies, role validation, etc.
        // 
        // Authorization vs Authentication:
        //   - Authentication: WHO are you? (Login, JWT, etc.)
        //   - Authorization: WHAT can you do? (Roles, policies, etc.)
        // 
        // Example usage:
        //   [Authorize]                     // Must be authenticated
        //   [Authorize(Roles = "Admin")]    // Must be Admin role
        //   [Authorize(Policy = "MinAge18")] // Custom policy
        services.AddAuthorization();

        // Return the services collection for method chaining
        // Allows: services.AddAuthServices(...).AddDbContext(...).AddControllers()
        return services;
    }
}
