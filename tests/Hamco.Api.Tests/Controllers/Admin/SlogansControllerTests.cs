using Hamco.Api;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hamco.Api.Tests.Controllers.Admin;

/// <summary>
/// Integration tests for SlogansController.
/// Tests all CRUD operations (NO random endpoint) with 5 authentication scenarios:
/// 1. Admin JWT - all operations succeed
/// 2. Admin API Key - all operations succeed
/// 3. Non-admin user JWT - all operations fail (403)
/// 4. Non-admin API Key - all operations fail (403)
/// 5. Anonymous - all operations fail (401)
/// </summary>
/// <remarks>
/// Why these test scenarios?
///   Slogans are admin-only CRUD - NO public access at all.
///   We need to verify authorization works correctly.
///   Both JWT and API Key authentication should work for admins.
///   Non-admins and anonymous users should be blocked completely.
/// 
/// Test Strategy:
///   - Integration tests (full HTTP stack, not unit tests)
///   - SQLite in-memory database (isolated, fast)
///   - WebApplicationFactory (real server, real auth)
///   - Test each endpoint with all 5 auth scenarios
///   
/// NO /api/slogans/random endpoint - public users see slogans via server-side rendering only.
/// </remarks>
public class SlogansControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private HamcoDbContext _context = null!;
    
    // Admin user credentials
    private string _adminToken = string.Empty;
    private string _adminUserId = string.Empty;
    private string _adminApiKey = string.Empty;
    
    // Non-admin user credentials
    private string _userToken = string.Empty;
    private string _userApiKey = string.Empty;

    public SlogansControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Set environment to "Testing" so Program.cs skips migration and uses test config
            builder.UseEnvironment("Testing");
            
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

                // Register services if not already registered
                if (!services.Any(s => s.ServiceType == typeof(IApiKeyService)))
                {
                    services.AddScoped<IApiKeyService, Hamco.Services.ApiKeyService>();
                }
            });
        });

        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Initialize database and create test users/API keys.
    /// Creates both admin and non-admin accounts with JWT and API keys.
    /// </summary>
    private async Task InitializeAsync()
    {
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        // Create admin user
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

        // Create non-admin user
        var regularUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            Email = "user@test.com",
            PasswordHash = passwordHasher.HashPassword("TestPassword123!"),
            Roles = new List<string> { "User" },
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(regularUser);

        await _context.SaveChangesAsync();

        // Generate JWTs
        _adminUserId = adminUser.Id;
        _adminToken = jwtService.GenerateToken(adminUser);
        _userToken = jwtService.GenerateToken(regularUser);

        // Generate API keys
        var adminKeyResult = await apiKeyService.GenerateKeyAsync("Admin Test Key", true, adminUser.Id);
        _adminApiKey = adminKeyResult.key;

        var userKeyResult = await apiKeyService.GenerateKeyAsync("User Test Key", false, regularUser.Id);
        _userApiKey = userKeyResult.key;

        // Create some test slogans
        _context.Slogans.Add(new Slogan
        {
            Text = "Your AI workspace, everywhere",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id
        });
        _context.Slogans.Add(new Slogan
        {
            Text = "Code. Deploy. Manage.",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id
        });
        _context.Slogans.Add(new Slogan
        {
            Text = "Inactive slogan",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id
        });
        await _context.SaveChangesAsync();
    }

    private void Cleanup()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region GET /api/slogans (List All)

    [Fact]
    public async Task GetSlogans_WithAdminJwt_Returns200WithSlogans()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var slogans = await response.Content.ReadFromJsonAsync<List<Slogan>>();
        Assert.NotNull(slogans);
        Assert.Equal(3, slogans.Count); // All 3 slogans (active and inactive)
        
        Cleanup();
    }

    [Fact]
    public async Task GetSlogans_WithAdminApiKey_Returns200WithSlogans()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        // Act
        var response = await _client.GetAsync("/api/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var slogans = await response.Content.ReadFromJsonAsync<List<Slogan>>();
        Assert.NotNull(slogans);
        Assert.Equal(3, slogans.Count);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSlogans_WithNonAdminJwt_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        // Act
        var response = await _client.GetAsync("/api/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSlogans_WithNonAdminApiKey_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        // Act
        var response = await _client.GetAsync("/api/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task GetSlogans_Anonymous_Returns401()
    {
        // Arrange
        await InitializeAsync();
        // No auth headers

        // Act
        var response = await _client.GetAsync("/api/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        Cleanup();
    }

    #endregion

    #region POST /api/slogans (Create Slogan)

    [Fact]
    public async Task PostSlogan_WithAdminJwt_Returns201WithCreatedSlogan()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new { text = "New test slogan", isActive = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var slogan = await response.Content.ReadFromJsonAsync<Slogan>();
        Assert.NotNull(slogan);
        Assert.Equal("New test slogan", slogan.Text);
        Assert.True(slogan.IsActive);
        Assert.Equal(_adminUserId, slogan.CreatedByUserId);
        
        Cleanup();
    }

    [Fact]
    public async Task PostSlogan_WithAdminApiKey_Returns201WithCreatedSlogan()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var request = new { text = "API key created slogan" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var slogan = await response.Content.ReadFromJsonAsync<Slogan>();
        Assert.NotNull(slogan);
        Assert.Equal("API key created slogan", slogan.Text);
        Assert.True(slogan.IsActive); // Default to true
        
        Cleanup();
    }

    [Fact]
    public async Task PostSlogan_WithNonAdminJwt_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var request = new { text = "Unauthorized slogan" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PostSlogan_WithNonAdminApiKey_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        var request = new { text = "Unauthorized slogan" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PostSlogan_Anonymous_Returns401()
    {
        // Arrange
        await InitializeAsync();
        // No auth headers

        var request = new { text = "Anonymous slogan" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PostSlogan_WithEmptyText_Returns400()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new { text = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slogans", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        Cleanup();
    }

    #endregion

    #region PUT /api/slogans/{id} (Update Slogan)

    [Fact]
    public async Task PutSlogan_WithAdminJwt_Returns200WithUpdatedSlogan()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var sloganId = _context.Slogans.First().Id;
        var request = new { text = "Updated slogan text", isActive = false };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/slogans/{sloganId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var slogan = await response.Content.ReadFromJsonAsync<Slogan>();
        Assert.NotNull(slogan);
        Assert.Equal("Updated slogan text", slogan.Text);
        Assert.False(slogan.IsActive);
        Assert.NotNull(slogan.UpdatedAt);
        
        Cleanup();
    }

    [Fact]
    public async Task PutSlogan_WithAdminApiKey_Returns200WithUpdatedSlogan()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var sloganId = _context.Slogans.First().Id;
        var request = new { text = "API key updated" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/slogans/{sloganId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var slogan = await response.Content.ReadFromJsonAsync<Slogan>();
        Assert.NotNull(slogan);
        Assert.Equal("API key updated", slogan.Text);
        
        Cleanup();
    }

    [Fact]
    public async Task PutSlogan_WithNonAdminJwt_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var sloganId = _context.Slogans.First().Id;
        var request = new { text = "Unauthorized update" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/slogans/{sloganId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PutSlogan_WithNonAdminApiKey_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        var sloganId = _context.Slogans.First().Id;
        var request = new { text = "Unauthorized update" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/slogans/{sloganId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PutSlogan_Anonymous_Returns401()
    {
        // Arrange
        await InitializeAsync();
        // No auth headers

        var sloganId = _context.Slogans.First().Id;
        var request = new { text = "Anonymous update" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/slogans/{sloganId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task PutSlogan_WithInvalidId_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new { text = "Update nonexistent" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/slogans/999999", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        Cleanup();
    }

    #endregion

    #region DELETE /api/slogans/{id} (Delete Slogan)

    [Fact]
    public async Task DeleteSlogan_WithAdminJwt_Returns204()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var sloganId = _context.Slogans.First().Id;

        // Act
        var response = await _client.DeleteAsync($"/api/slogans/{sloganId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify deletion
        var deletedSlogan = await _context.Slogans.FindAsync(sloganId);
        Assert.Null(deletedSlogan);
        
        Cleanup();
    }

    [Fact]
    public async Task DeleteSlogan_WithAdminApiKey_Returns204()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var sloganId = _context.Slogans.First().Id;

        // Act
        var response = await _client.DeleteAsync($"/api/slogans/{sloganId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task DeleteSlogan_WithNonAdminJwt_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var sloganId = _context.Slogans.First().Id;

        // Act
        var response = await _client.DeleteAsync($"/api/slogans/{sloganId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task DeleteSlogan_WithNonAdminApiKey_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        var sloganId = _context.Slogans.First().Id;

        // Act
        var response = await _client.DeleteAsync($"/api/slogans/{sloganId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task DeleteSlogan_Anonymous_Returns401()
    {
        // Arrange
        await InitializeAsync();
        // No auth headers

        var sloganId = _context.Slogans.First().Id;

        // Act
        var response = await _client.DeleteAsync($"/api/slogans/{sloganId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        Cleanup();
    }

    [Fact]
    public async Task DeleteSlogan_WithInvalidId_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.DeleteAsync("/api/slogans/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        Cleanup();
    }

    #endregion
}
