using Microsoft.EntityFrameworkCore;
using Hamco.Data;
using Hamco.Core.Extensions;
using Hamco.Core.Services;
using Hamco.Services;
using Hamco.Api.Middleware;
using Microsoft.Data.Sqlite;
using dotenv.net;

// ============================================================================
// PROGRAM.CS - APPLICATION ENTRY POINT
// ============================================================================
// This file configures and runs the ASP.NET Core web application.
// Execution flow:
//   1. Load .env file for local development secrets
//   2. Create WebApplicationBuilder (configure services)
//   3. Build WebApplication (create app instance)
//   4. Configure HTTP request pipeline (middleware)
//   5. Run application (start web server)
//
// Think of this as the "main()" method for web applications.
// ============================================================================

// Load .env file ONLY for local development (when DATABASE_URL is not set)
// Railway, Heroku, etc. provide env vars directly, so we skip .env loading
// to avoid local dev settings overriding production configuration
//
// Priority: Environment variables (Railway) > .env file (local dev) > defaults
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")))
{
    DotEnv.Load();
}

// Step 1: Create application builder
// WebApplication.CreateBuilder() initializes the application:
//   - Loads configuration (appsettings.json, environment variables, .env, etc.)
//   - Sets up dependency injection container
//   - Configures logging
//   - Sets up default services
//
// 'var' keyword: Type inference (compiler determines type)
//   var builder = ... is equivalent to:
//   WebApplicationBuilder builder = ...
//
// C# 9+ top-level statements:
//   This code runs directly without wrapping in Main() method
//   Compiler generates: static void Main(string[] args) { ... } automatically
var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// SERVICE CONFIGURATION (Dependency Injection Container)
// ============================================================================
// Services registered here can be injected into controllers, middleware, etc.
// Service lifetime options:
//   - Singleton: One instance for entire application
//   - Scoped: One instance per HTTP request (most common for DbContext)
//   - Transient: New instance every time it's requested

// Add MVC controllers with views
// AddControllersWithViews() registers MVC services including:
//   - Controller activation (create controller instances)
//   - Model binding (map HTTP request data to method parameters)
//   - Model validation (check Data Annotations like [Required])
//   - JSON serialization (System.Text.Json by default)
//   - Action result execution (return Ok(), NotFound(), etc.)
//   - View rendering engine (Razor)
builder.Services.AddControllersWithViews();

// Add API Explorer for OpenAPI/Swagger
// AddEndpointsApiExplorer() enables automatic API documentation:
//   - Discovers API endpoints (controllers, actions, parameters)
//   - Generates metadata for OpenAPI tools
//   - Required for Swagger/OpenAPI document generation
builder.Services.AddEndpointsApiExplorer();

// Add OpenAPI document generation
// AddOpenApi() generates OpenAPI 3.0 specification:
//   - API schema (endpoints, parameters, responses)
//   - Accessible at /openapi/v1.json
//   - Used by Swagger UI, API clients, code generators
//
// OpenAPI (formerly Swagger) is an industry standard for describing REST APIs
builder.Services.AddOpenApi();

// ============================================================================
// DATABASE CONFIGURATION (Environment-based provider selection)
// ============================================================================

// TESTING vs PRODUCTION DATABASE PROVIDERS
//
// Problem: Integration tests need isolated in-memory databases,
// but EF Core providers (Npgsql, SQLite) register deep internal services
// that conflict when trying to swap providers at test runtime.
//
// Solution: Register the RIGHT provider from the start based on environment.
//   - Testing environment → SQLite in-memory (fast, isolated, no external deps)
//   - Production/Development → PostgreSQL (real database with full features)
//
// Environment detection:
//   builder.Environment.IsEnvironment("Testing") checks ASPNETCORE_ENVIRONMENT
//   Test factory sets this to "Testing" in ConfigureWebHost()
//
// Benefits:
//   ✓ No provider conflicts (single provider per app instance)
//   ✓ Tests get truly isolated databases
//   ✓ No complex service descriptor removal gymnastics
//   ✓ Clean separation of concerns

if (builder.Environment.IsEnvironment("Testing"))
{
    // TESTING ENVIRONMENT: Use SQLite in-memory database
    //
    // SQLite in-memory databases:
    //   - Created fresh for each test instance
    //   - No persistence between runs (perfect for tests)
    //   - No external dependencies (no PostgreSQL server needed)
    //   - Fast (runs entirely in memory)
    //
    // Important: Connection must stay open for schema to persist!
    // Test factory manages connection lifetime (see TestWebApplicationFactory)
    //
    // Connection string "DataSource=:memory:" creates in-memory database
    // Each WebApplicationFactory gets its own isolated instance
    builder.Services.AddDbContext<HamcoDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    // PRODUCTION/DEVELOPMENT: Use PostgreSQL via Npgsql
    // 
    // Connection string sources (highest priority first):
    //   1. DATABASE_URL environment variable (Railway, Heroku, etc. provide this)
    //   2. Individual DB_* environment variables (see .env.example)
    //   3. ConnectionStrings:DefaultConnection from appsettings.json
    //   4. Default localhost fallback for development
    //
    // Railway provides DATABASE_URL like: postgres://user:password@host:port/database
    // We parse this and also support individual DB_HOST, DB_PORT, etc.

    string connectionString;

    // Check for Railway-style DATABASE_URL
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Parse postgres://user:password@host:port/database format
        try
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }
        catch
        {
            // If parsing fails, use as-is
            connectionString = databaseUrl;
        }
    }
    else
    {
        // Build connection string from individual env vars or appsettings
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "hamco_dev";
        var username = Environment.GetEnvironmentVariable("DB_USER") ?? "art";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

        connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

        // Fallback to appsettings if no env vars set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_HOST")))
        {
            connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? connectionString;
        }
    }

    // Register database context with dependency injection
    // AddDbContext<T>(): Registers DbContext as scoped service
    // Scoped lifetime: New instance per HTTP request
    //   - Request starts → Create DbContext
    //   - Request ends → Dispose DbContext (close connection, discard changes)
    //
    // Lambda: options => ... configures DbContext options
    // UseNpgsql(): Use PostgreSQL provider (Npgsql library)
    //   - Translates EF Core queries to PostgreSQL SQL
    //   - Handles PostgreSQL-specific features
    //   - Connection pooling, transaction management, etc.
    builder.Services.AddDbContext<HamcoDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// ============================================================================
// AUTHENTICATION CONFIGURATION (JWT)
// ============================================================================

// Get JWT configuration from environment variables or appsettings.json
// Priority: Environment Variables > appsettings.json > Default fallback
//
// For Railway/production: Set JWT_KEY, JWT_ISSUER, JWT_AUDIENCE env vars
// For local development: Use .env file (see .env.example)
//
// Generate a secure JWT key for production:
//   openssl rand -base64 32
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? "your-very-secret-key-change-in-production-minimum-32-chars";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "hamco-api";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "hamco-client";

// Register authentication services using extension method
// AddAuthServices() is defined in ServiceCollectionExtensions
// Registers:
//   - IPasswordHasher / PasswordHasher (BCrypt password hashing)
//   - IJwtService / JwtService (JWT token generation/validation)
//   - JWT Bearer authentication middleware
//   - Authorization services
//
// This is a custom extension method that encapsulates all auth configuration.
// Benefits:
//   - Keeps Program.cs clean and readable
//   - Reusable across projects
//   - Single responsibility (auth setup in one place)
builder.Services.AddAuthServices(jwtKey, jwtIssuer, jwtAudience);

// Register API Key service
// IApiKeyService: Manages API key generation, validation, and revocation
// ApiKeyService: Implementation using BCrypt hashing
// Scoped lifetime: One instance per HTTP request (uses DbContext)
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Register Markdown rendering service
// IMarkdownService: Renders Markdown to HTML with syntax highlighting and XSS protection
// MarkdownService: Implementation using Markdig + Markdown.ColorCode + HtmlSanitizer
// Scoped lifetime: One instance per HTTP request
builder.Services.AddScoped<IMarkdownService, MarkdownService>();

// Register slogan randomizer service
// ISloganRandomizer: Gets random active slogans from database
// SloganRandomizer: Implementation with in-memory caching
// Scoped lifetime: One instance per HTTP request
builder.Services.AddScoped<ISloganRandomizer, SloganRandomizer>();

// Register image randomizer service
// IImageRandomizer: Gets random header background images
// ImageRandomizer: Implementation with static image list
// Singleton lifetime: One instance for entire application (no state, just random selection)
builder.Services.AddSingleton<IImageRandomizer, ImageRandomizer>();

// Add in-memory caching for slogan service
// IMemoryCache: Used by SloganRandomizer to cache database queries
builder.Services.AddMemoryCache();

// ============================================================================
// BUILD APPLICATION
// ============================================================================

// Build the WebApplication instance
// builder.Build() creates the app with all configured services
// After this point, can't add more services (service registration closed)
// From here on, we configure the HTTP request pipeline (middleware)
var app = builder.Build();

// ============================================================================
// HTTP REQUEST PIPELINE (Middleware Configuration)
// ============================================================================
// Middleware: Components that handle HTTP requests/responses
// Execution order matters! Middleware runs in the order added.
//
// Request flow:
//   Client → [Middleware 1] → [Middleware 2] → [Middleware N] → Endpoint
//   Client ← [Middleware 1] ← [Middleware 2] ← [Middleware N] ← Endpoint
//
// Each middleware can:
//   - Process request before next middleware
//   - Call next middleware (or short-circuit)
//   - Process response after next middleware returns
//
// Example middleware pipeline:
//   1. HTTPS Redirection (force HTTPS)
//   2. Authentication (identify user)
//   3. Authorization (check permissions)
//   4. Routing (match URL to controller action)
//   5. Controller execution (your code)

// Enable OpenAPI UI in development environment
// app.Environment.IsDevelopment() checks if:
//   - ASPNETCORE_ENVIRONMENT = "Development"
//   - Or running from Visual Studio / dotnet run
//
// Why only in development?
//   - Production: Don't expose API schema publicly (security)
//   - Development: Useful for testing (Swagger UI)
if (app.Environment.IsDevelopment())
{
    // MapOpenApi() exposes OpenAPI JSON document at /openapi/v1.json
    // Can be used with:
    //   - Swagger UI (interactive API explorer)
    //   - Postman (import OpenAPI spec)
    //   - Code generators (generate client SDKs)
    app.MapOpenApi();
}

// HTTPS Redirection - DISABLED for Railway
//
// Railway handles SSL termination at the edge (load balancer)
// The app container runs HTTP internally
// Enabling HTTPS redirection here causes issues because there's no HTTPS port
// inside the container - Railway provides HTTPS externally
//
// Local development: HTTPS redirection is handled by launchSettings.json
// Production (Railway): Railway provides HTTPS at the edge
//
// Commented out: app.UseHttpsRedirection();

// Enable static files middleware
// app.UseStaticFiles() serves static files from wwwroot folder:
//   - CSS files (wwwroot/css/*)
//   - JavaScript files (wwwroot/js/*)
//   - Images (wwwroot/img/*)
//   - Favicon (wwwroot/img/favicon.ico)
//
// Maps requests like /css/styles.css to wwwroot/css/styles.css
// Must come before routing so static files bypass controller execution
app.UseStaticFiles();

// Enable authentication middleware
// app.UseAuthentication() middleware:
//   - Reads Authorization header (or cookie)
//   - Extracts JWT token
//   - Validates token (signature, expiration, issuer, audience)
//   - Populates HttpContext.User with claims
//   - Passes to next middleware
//
// Must come BEFORE UseAuthorization()!
// Authentication = "Who are you?" (identity)
//
// JWT validation configured in ServiceCollectionExtensions.AddAuthServices()
app.UseAuthentication();

// Enable API key authentication middleware
// app.UseApiKeyAuthentication() middleware:
//   - Reads X-API-Key header
//   - Validates key against database (if no JWT auth)
//   - Populates HttpContext.User with API key claims
//   - Passes to next middleware if no key or key invalid
//
// Must come AFTER UseAuthentication() (so JWT takes precedence)
// Must come BEFORE UseAuthorization() (so key auth sets user before auth checks)
//
// Authentication priority:
//   1. JWT Bearer token (if present)
//   2. API Key (if no JWT and X-API-Key header present)
//   3. Anonymous (if neither)
app.UseApiKeyAuthentication();

// Enable authorization middleware
// app.UseAuthorization() middleware:
//   - Checks if user has permission for requested endpoint
//   - Evaluates [Authorize] attributes on controllers/actions
//   - Checks roles, policies, claims
//   - Returns 401/403 if unauthorized
//   - Passes to next middleware if authorized
//
// Must come AFTER UseAuthentication() and UseApiKeyAuthentication()!
// Authorization = "What can you do?" (permissions)
//
// Example:
//   [Authorize(Roles = "Admin")] → Checks if User.IsInRole("Admin")
//   If not, returns 403 Forbidden
app.UseAuthorization();

// Map controller endpoints
// app.MapControllers() creates routes for all controllers:
//   - Scans assemblies for classes with [ApiController]
//   - Creates route table from [Route] and [Http*] attributes
//   - Maps HTTP methods to controller actions
//
// Example routes created:
//   POST   /api/notes      → NotesController.CreateNote
//   GET    /api/notes/{id} → NotesController.GetNote
//   DELETE /api/notes/{id} → NotesController.DeleteNote
//   POST   /api/auth/login → AuthController.Login
//
// Attribute routing vs Convention routing:
//   We use attribute routing ([Route], [HttpGet]) - modern, explicit
//   Alternative: Convention routing (routes defined in Program.cs) - older style
app.MapControllers();

// ============================================================================
// DATABASE MIGRATIONS (Production)
// ============================================================================
//
// Apply pending migrations on startup
// This ensures the database schema is up-to-date before accepting requests
//
// WARNING: For production with multiple instances, use proper migration strategy:
//   - Run migrations as a separate deployment step
//   - Or use database deployment tools (DbUp, FluentMigrator)
//   - Or use container init scripts
//
// For Railway/single-instance deployments, this is acceptable
// For Kubernetes/multi-instance, run migrations as a Job, not in the app

if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        try
        {
            Console.WriteLine("Applying database migrations...");
            dbContext.Database.Migrate();
            Console.WriteLine("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying migrations: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
            throw; // Re-throw so Railway shows the full error
        }
    }
}

// ============================================================================
// RUN APPLICATION
// ============================================================================

// Start the web server and listen for requests
// app.Run() blocks until application shutdown (Ctrl+C, hosting shutdown, etc.)
//
// What happens when app.Run() executes:
//   1. Kestrel web server starts
//   2. Listens on configured ports (default: 5000 HTTP, 5001 HTTPS)
//   3. Waits for HTTP requests
//   4. For each request:
//      a. Request enters middleware pipeline
//      b. Middleware processes request
//      c. Controller action executes
//      d. Response sent back through middleware
//   5. Continues until application stops
//
// Console output when running:
//   info: Microsoft.Hosting.Lifetime[14]
//         Now listening on: https://localhost:5001
//   info: Microsoft.Hosting.Lifetime[14]
//         Now listening on: http://localhost:5000
//   info: Microsoft.Hosting.Lifetime[0]
//         Application started. Press Ctrl+C to shut down.
app.Run();

// ============================================================================
// PARTIAL CLASS FOR TESTING
// ============================================================================

// Make Program class accessible to integration tests
// 'public partial class Program { }' creates a public class for Program
//
// Why?
//   Integration tests use WebApplicationFactory<Program>
//   WebApplicationFactory needs to reference Program class
//   Top-level statements create implicit Program class (internal by default)
//   This makes it public and accessible to test project
//
// Without this:
//   Test project can't reference Program → compilation error
//
// Partial class in C#:
//   - Class definition split across multiple files
//   - All parts combined at compile time
//   - One part here (empty), one part auto-generated (top-level statements)
//
// This is a common pattern for testable ASP.NET Core applications.
public partial class Program { }
