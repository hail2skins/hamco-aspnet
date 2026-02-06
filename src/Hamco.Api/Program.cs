using Microsoft.EntityFrameworkCore;
using Hamco.Data;
using Hamco.Core.Extensions;

// ============================================================================
// PROGRAM.CS - APPLICATION ENTRY POINT
// ============================================================================
// This file configures and runs the ASP.NET Core web application.
// Execution flow:
//   1. Create WebApplicationBuilder (configure services)
//   2. Build WebApplication (create app instance)
//   3. Configure HTTP request pipeline (middleware)
//   4. Run application (start web server)
//
// Think of this as the "main()" method for web applications.
// ============================================================================

// Step 1: Create application builder
// WebApplication.CreateBuilder() initializes the application:
//   - Loads configuration (appsettings.json, environment variables, etc.)
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

// Add MVC controllers
// AddControllers() registers all controller-related services:
//   - Controller activation (create controller instances)
//   - Model binding (map HTTP request data to method parameters)
//   - Model validation (check Data Annotations like [Required])
//   - JSON serialization (System.Text.Json by default)
//   - Action result execution (return Ok(), NotFound(), etc.)
builder.Services.AddControllers();

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
// DATABASE CONFIGURATION (PostgreSQL via Entity Framework Core)
// ============================================================================

// Get connection string from configuration
// builder.Configuration: Loads from appsettings.json, environment variables, etc.
// GetConnectionString("DefaultConnection"): Reads ConnectionStrings:DefaultConnection
// ?? "...": Null-coalescing operator (use default if not found)
// 
// Configuration priority (highest to lowest):
//   1. Command-line arguments
//   2. Environment variables
//   3. appsettings.{Environment}.json (Development, Production, etc.)
//   4. appsettings.json
//   5. Default value in code (after ??)
// 
// Connection string format (PostgreSQL):
//   Host=localhost;Database=hamco_dev;Username=art;Password=
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=hamco_dev;Username=art;Password=";

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

// ============================================================================
// AUTHENTICATION CONFIGURATION (JWT)
// ============================================================================

// Get JWT configuration from appsettings.json or use defaults
// Null-coalescing operator (??) provides fallback values
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-very-secret-key-that-is-at-least-32-characters-long";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "hamco-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "hamco-client";

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

// Force HTTPS redirection
// app.UseHttpsRedirection() middleware:
//   - Checks if request uses HTTP
//   - If HTTP, returns 307 Temporary Redirect to HTTPS URL
//   - If HTTPS, passes through to next middleware
// 
// Example:
//   Request: http://localhost:5000/api/notes
//   Response: 307 Redirect to https://localhost:5001/api/notes
// 
// Important for security:
//   - Protects authentication tokens from interception
//   - Prevents man-in-the-middle attacks
//   - Required for JWT token security
// 
// Note: Development certificates are self-signed (browser warnings expected)
app.UseHttpsRedirection();

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

// Enable authorization middleware
// app.UseAuthorization() middleware:
//   - Checks if user has permission for requested endpoint
//   - Evaluates [Authorize] attributes on controllers/actions
//   - Checks roles, policies, claims
//   - Returns 401/403 if unauthorized
//   - Passes to next middleware if authorized
// 
// Must come AFTER UseAuthentication()!
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
