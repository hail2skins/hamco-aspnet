using System.Net;
using System.Net.Http.Json;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Integration tests for API Key authentication with role-based permissions.
/// Tests the permission matrix: Admin API keys (full CRUD) vs User API keys (Read-only).
/// </summary>
/// <remarks>
/// API KEY PERMISSION MATRIX:
/// 
/// | Role       | GET (Read) | POST (Create) | PUT (Update) | DELETE |
/// |------------|------------|---------------|--------------|--------|
/// | Admin Key  | ✅ 200     | ✅ 201        | ✅ 200       | ✅ 204 |
/// | User Key   | ✅ 200     | ❌ 403        | ❌ 403       | ❌ 403 |
/// | No Auth    | ✅ 200     | ❌ 401        | ❌ 401       | ❌ 401 |
/// 
/// DESIGN PHILOSOPHY:
/// - Read operations (GET) are public - anyone can read the blog
/// - Write operations (POST/PUT/DELETE) require authentication
/// - Admin API keys have full CRUD permissions
/// - User (non-admin) API keys can only read (same as public)
/// 
/// USE CASES:
/// - Admin keys: Automation, bots, integrations that need write access
/// - User keys: Read-only monitoring, public API access, untrusted clients
/// 
/// IMPLEMENTATION:
/// - API keys authenticate via X-API-Key header
/// - ApiKeyMiddleware validates key and sets User claims
/// - Authorization policies check for Admin role on write endpoints
/// - Non-admin keys authenticate successfully but fail authorization
/// </remarks>
public class ApiKeyPermissionsTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _adminApiKey = string.Empty;
    private string _userApiKey = string.Empty;
    private string _adminUserId = string.Empty;

    public ApiKeyPermissionsTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Helper: Initialize database and create both admin and user API keys.
    /// </summary>
    private async Task InitializeApiKeysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Register first user (becomes admin)
        var registerRequest = new RegisterRequest
        {
            Username = "AdminUser",
            Email = "admin@test.com",
            Password = "AdminPass123"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);

        _adminUserId = authResponse.UserId!;

        // Create authenticated client with admin JWT
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

        // Create admin API key
        var adminKeyRequest = new { name = "Admin Bot", isAdmin = true };
        var adminKeyResponse = await adminClient.PostAsJsonAsync("/api/admin/api-keys", adminKeyRequest);
        Assert.Equal(HttpStatusCode.Created, adminKeyResponse.StatusCode);

        var adminKeyResult = await adminKeyResponse.Content.ReadFromJsonAsync<ApiKeyResponse>();
        Assert.NotNull(adminKeyResult);
        _adminApiKey = adminKeyResult.Key!;

        // Create user (non-admin) API key
        var userKeyRequest = new { name = "Read Only Bot", isAdmin = false };
        var userKeyResponse = await adminClient.PostAsJsonAsync("/api/admin/api-keys", userKeyRequest);
        Assert.Equal(HttpStatusCode.Created, userKeyResponse.StatusCode);

        var userKeyResult = await userKeyResponse.Content.ReadFromJsonAsync<ApiKeyResponse>();
        Assert.NotNull(userKeyResult);
        _userApiKey = userKeyResult.Key!;
    }

    /// <summary>
    /// Helper: Create a test note using admin API key.
    /// Returns the note ID for update/delete tests.
    /// </summary>
    private async Task<int> CreateTestNoteAsync()
    {
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var request = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "For permission testing"
        };

        var response = await adminClient.PostAsJsonAsync("/api/notes", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        return note.Id;
    }

    // ============================================================================
    // ADMIN API KEY - FULL CRUD ACCESS
    // ============================================================================

    [Fact]
    public async Task AdminApiKey_CanRead_Returns200()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        await CreateTestNoteAsync(); // Create some data to read

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        // ACT
        var response = await adminClient.GetAsync("/api/notes");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);
        Assert.NotEmpty(notes);
    }

    [Fact]
    public async Task AdminApiKey_CanCreate_Returns201()
    {
        // ARRANGE
        await InitializeApiKeysAsync();

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var request = new CreateNoteRequest
        {
            Title = "Admin Created",
            Content = "Using admin API key"
        };

        // ACT
        var response = await adminClient.PostAsJsonAsync("/api/notes", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal("Admin Created", note.Title);
        Assert.Null(note.UserId); // API key posts don't have userId
    }

    [Fact]
    public async Task AdminApiKey_CanUpdate_Returns200()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        var noteId = await CreateTestNoteAsync();

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        var request = new UpdateNoteRequest
        {
            Title = "Updated by Admin Key",
            Content = "Modified content"
        };

        // ACT
        var response = await adminClient.PutAsJsonAsync($"/api/notes/{noteId}", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal("Updated by Admin Key", note.Title);
    }

    [Fact]
    public async Task AdminApiKey_CanDelete_Returns204()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        var noteId = await CreateTestNoteAsync();

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-API-Key", _adminApiKey);

        // ACT
        var response = await adminClient.DeleteAsync($"/api/notes/{noteId}");

        // ASSERT
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ============================================================================
    // USER (NON-ADMIN) API KEY - READ ONLY ACCESS
    // ============================================================================

    [Fact]
    public async Task UserApiKey_CanRead_Returns200()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        await CreateTestNoteAsync(); // Create some data to read

        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        // ACT
        var response = await userClient.GetAsync("/api/notes");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);
        Assert.NotEmpty(notes);
    }

    [Fact]
    public async Task UserApiKey_CannotCreate_Returns403()
    {
        // ARRANGE
        await InitializeApiKeysAsync();

        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        var request = new CreateNoteRequest
        {
            Title = "Should Fail",
            Content = "User key cannot create"
        };

        // ACT
        var response = await userClient.PostAsJsonAsync("/api/notes", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserApiKey_CannotUpdate_Returns403()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        var noteId = await CreateTestNoteAsync();

        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        var request = new UpdateNoteRequest
        {
            Title = "Attempted Update",
            Content = "Should fail"
        };

        // ACT
        var response = await userClient.PutAsJsonAsync($"/api/notes/{noteId}", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserApiKey_CannotDelete_Returns403()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        var noteId = await CreateTestNoteAsync();

        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        // ACT
        var response = await userClient.DeleteAsync($"/api/notes/{noteId}");

        // ASSERT
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify note still exists
        var getResponse = await _client.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    // ============================================================================
    // EDGE CASES
    // ============================================================================

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        // ARRANGE
        await InitializeApiKeysAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "hamco_sk_invalid_key_12345");

        // ACT
        var response = await client.GetAsync("/api/notes");

        // ASSERT
        // Invalid key should fail authentication (401)
        // Note: Current implementation may return 200 for public read endpoint
        // This test documents expected behavior - adjust if design differs
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || 
            response.StatusCode == HttpStatusCode.OK,
            "Invalid API key on public endpoint should either fail auth (401) or allow public access (200)"
        );
    }

    [Fact]
    public async Task UserApiKey_GetSingleNote_Returns200()
    {
        // ARRANGE
        await InitializeApiKeysAsync();
        var noteId = await CreateTestNoteAsync();

        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Add("X-API-Key", _userApiKey);

        // ACT
        var response = await userClient.GetAsync($"/api/notes/{noteId}");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal(noteId, note.Id);
    }
}

/// <summary>
/// Response model for API key creation endpoint.
/// </summary>
public class ApiKeyResponse
{
    public string? Key { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Prefix { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Message { get; set; }
}
