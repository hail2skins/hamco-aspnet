using Hamco.Api;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hamco.Api.Tests.Controllers.Admin;

/// <summary>
/// Integration tests for ApiKeysController.
/// Tests API key generation, listing, and revocation endpoints.
/// </summary>
/// <remarks>
/// Integration Testing Strategy:
///   These are NOT unit tests - they test the full HTTP request/response cycle.
///   Uses WebApplicationFactory to spin up a real (but in-memory) API server.
///   Benefits:
///     ✅ Tests actual HTTP behavior (routing, serialization, auth)
///     ✅ Catches issues unit tests miss (middleware order, DI config)
///     ✅ Tests integration between controller, service, database
///     ✅ More realistic than mocking everything
/// 
/// WebApplicationFactory:
///   Creates a TestServer that hosts your ASP.NET Core app.
///   In-memory server (no network, just function calls).
///   Fast enough for automated testing (~100ms per test).
/// 
/// Test Database:
///   SQLite in-memory (isolated, fast, disposable).
///   Each test gets a fresh database.
///   Real database queries (not mocked repositories).
/// 
/// Authentication in Tests:
///   Create test user → generate JWT → include in Authorization header.
///   Tests both authenticated and unauthenticated scenarios.
/// 
/// TDD Process:
///   1. Write these tests FIRST (before controller exists)
///   2. Run tests → watch them fail (red)
///   3. Implement minimal controller code to pass (green)
///   4. Refactor while keeping tests green
/// </remarks>
public class ApiKeysControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private HamcoDbContext _context = null!;
    private string _adminToken = string.Empty;
    private string _adminUserId = string.Empty;

    /// <summary>
    /// Test setup - runs once per test class (IClassFixture).
    /// Creates web application factory and HTTP client.
    /// </summary>
    public ApiKeysControllerTests(WebApplicationFactory<Program> factory)
    {
        // Create factory with SQLite in-memory database
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<HamcoDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add SQLite in-memory database
                services.AddDbContext<HamcoDbContext>(options =>
                {
                    options.UseSqlite("DataSource=:memory:");
                });

                // Register ApiKeyService (if not already registered)
                if (!services.Any(s => s.ServiceType == typeof(IApiKeyService)))
                {
                    services.AddScoped<IApiKeyService, Hamco.Services.ApiKeyService>();
                }
            });
        });

        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Helper method to initialize database and create admin user.
    /// Runs at the start of each test.
    /// </summary>
    private async Task InitializeAsync()
    {
        // Get service scope
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        // Open connection and create schema
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        // Create admin user for testing
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var adminUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testadmin",
            Email = "admin@test.com",
            PasswordHash = passwordHasher.HashPassword("TestPassword123!"),
            Roles = new List<string> { "Admin" },
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        _adminUserId = adminUser.Id;
        _adminToken = jwtService.GenerateToken(adminUser);
    }

    /// <summary>
    /// Cleanup after each test.
    /// </summary>
    private void Cleanup()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region POST /api/admin/api-keys (Generate Key)

    /// <summary>
    /// Test: POST /api/admin/api-keys with valid admin token generates key.
    /// </summary>
    [Fact]
    public async Task PostApiKey_WithAdminToken_ReturnsCreatedWithKey()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new { name = "Test Bot", isAdmin = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/api-keys", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(result.TryGetProperty("key", out var keyProp));
        var key = keyProp.GetString();
        Assert.NotNull(key);
        Assert.StartsWith("hamco_sk_", key);

        Assert.True(result.TryGetProperty("id", out var idProp));
        Assert.NotNull(idProp.GetString());

        Assert.True(result.TryGetProperty("name", out var nameProp));
        Assert.Equal("Test Bot", nameProp.GetString());

        Cleanup();
    }

    /// <summary>
    /// Test: POST /api/admin/api-keys without auth token returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task PostApiKey_WithoutToken_Returns401()
    {
        // Arrange
        await InitializeAsync();
        var request = new { name = "Test Bot", isAdmin = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/api-keys", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Cleanup();
    }

    /// <summary>
    /// Test: POST /api/admin/api-keys with invalid request body returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task PostApiKey_WithEmptyName_Returns400()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new { name = "", isAdmin = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/api-keys", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Cleanup();
    }

    #endregion

    #region GET /api/admin/api-keys (List Keys)

    /// <summary>
    /// Test: GET /api/admin/api-keys with admin token returns list of keys.
    /// </summary>
    [Fact]
    public async Task GetApiKeys_WithAdminToken_ReturnsKeys()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create some API keys first
        var apiKeyService = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IApiKeyService>();
        await apiKeyService.GenerateKeyAsync("Key 1", true, _adminUserId);
        await apiKeyService.GenerateKeyAsync("Key 2", false, _adminUserId);

        // Act
        var response = await _client.GetAsync("/api/admin/api-keys");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(result.ValueKind == JsonValueKind.Array);
        var keys = result.EnumerateArray().ToList();
        Assert.Equal(2, keys.Count);

        // Verify keys contain expected properties (but NOT full key!)
        var firstKey = keys[0];
        Assert.True(firstKey.TryGetProperty("id", out _));
        Assert.True(firstKey.TryGetProperty("name", out _));
        Assert.True(firstKey.TryGetProperty("keyPrefix", out _));
        Assert.True(firstKey.TryGetProperty("isActive", out _));
        Assert.False(firstKey.TryGetProperty("keyHash", out _)); // Should NOT expose hash!
        Assert.False(firstKey.TryGetProperty("key", out _)); // Should NOT expose full key!

        Cleanup();
    }

    /// <summary>
    /// Test: GET /api/admin/api-keys without auth token returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetApiKeys_WithoutToken_Returns401()
    {
        // Arrange
        await InitializeAsync();

        // Act
        var response = await _client.GetAsync("/api/admin/api-keys");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Cleanup();
    }

    #endregion

    #region DELETE /api/admin/api-keys/{id} (Revoke Key)

    /// <summary>
    /// Test: DELETE /api/admin/api-keys/{id} with admin token revokes key.
    /// </summary>
    [Fact]
    public async Task DeleteApiKey_WithAdminToken_Returns204()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var apiKeyService = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IApiKeyService>();
        var (key, entity) = await apiKeyService.GenerateKeyAsync("To Delete", true, _adminUserId);

        // Act
        var response = await _client.DeleteAsync($"/api/admin/api-keys/{entity.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify key was revoked
        var revokedKey = await _context.ApiKeys.FindAsync(entity.Id);
        Assert.NotNull(revokedKey);
        Assert.False(revokedKey.IsActive);

        Cleanup();
    }

    /// <summary>
    /// Test: DELETE /api/admin/api-keys/{id} without auth token returns 401.
    /// </summary>
    [Fact]
    public async Task DeleteApiKey_WithoutToken_Returns401()
    {
        // Arrange
        await InitializeAsync();

        // Act
        var response = await _client.DeleteAsync("/api/admin/api-keys/some-id");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Cleanup();
    }

    /// <summary>
    /// Test: DELETE /api/admin/api-keys/{id} with non-existent ID returns 404.
    /// </summary>
    [Fact]
    public async Task DeleteApiKey_WithNonExistentId_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.DeleteAsync("/api/admin/api-keys/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Cleanup();
    }

    #endregion
}
