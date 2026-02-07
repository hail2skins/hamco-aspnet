using System.Net;
using System.Net.Http.Json;
using Hamco.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Collection definition to run notes tests sequentially (not in parallel).
/// Prevents race conditions with shared admin user creation.
/// </summary>
[CollectionDefinition("NotesTests", DisableParallelization = true)]
public class NotesTestsCollection
{
}

/// <summary>
/// Integration tests for NotesController with proper authorization.
/// Tests the correct behavior: Admin-only write, public read.
/// </summary>
/// <remarks>
/// [Collection("NotesTests")] ensures tests run sequentially, not in parallel.
/// This prevents race conditions when creating the shared admin user.
/// 
/// VALID TEST COVERAGE (Art's specification):
/// 
/// ADMIN-ONLY ENDPOINTS (require auth + admin role):
///   - POST /api/notes - Create note
///   - PUT /api/notes/{id} - Update note
///   - DELETE /api/notes/{id} - Delete note
///   Expected: 401 if no auth, 403 if not admin, 2xx if admin
/// 
/// PUBLIC ENDPOINTS (no auth required):
///   - GET /api/notes - List all notes
///   - GET /api/notes/{id} - Get single note
///   Expected: Anyone can read the blog
/// 
/// Old CRUD tests that expected unauthenticated write access were DELETED.
/// Those tests tested invalid behavior (anonymous posting).
/// </remarks>
[Collection("NotesTests")]
public class NotesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NotesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper: Gets authenticated admin client.
    /// </summary>
    /// <remarks>
    /// Since tests run sequentially (Collection attribute), we can safely
    /// create a new admin user for each test without worrying about race conditions.
    /// The first user registered will be admin; subsequent tests will register new users.
    /// </remarks>
    private async Task<HttpClient> GetAuthenticatedAdminClientAsync()
    {
        // For simplicity in sequential tests, just use admin@test.com
        // If it already exists, we'll get an error, but that's fine
        // Try to register, but if email exists, just login instead
        var registerRequest = new RegisterRequest
        {
            Username = "AdminUser",
            Email = "admin@test.com",
            Password = "AdminPass123"
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        
        AuthResponse? authResponse;
        if (registerResponse.StatusCode == HttpStatusCode.Created)
        {
            // Successfully registered
            authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        }
        else
        {
            // User already exists, login instead
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "AdminPass123"
            };
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        }
        
        // Create authenticated client with Bearer token
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);
        
        return authenticatedClient;
    }

    // ============================================================================
    // CREATE (POST) - ADMIN ONLY
    // ============================================================================

    [Fact]
    public async Task CreateNote_AdminUser_Returns201()
    {
        // ARRANGE
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var request = new CreateNoteRequest
        {
            Title = "Admin Post",
            Content = "Only admin can create"
        };

        // ACT
        var response = await adminClient.PostAsJsonAsync("/api/notes", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal("Admin Post", note.Title);
        Assert.NotNull(note.UserId); // Should have user ID
    }

    [Fact]
    public async Task CreateNote_Unauthenticated_Returns401()
    {
        // ARRANGE
        var request = new CreateNoteRequest
        {
            Title = "Anonymous Post",
            Content = "Should fail"
        };

        // ACT - No Bearer token
        var response = await _client.PostAsJsonAsync("/api/notes", request);

        // ASSERT - Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_NonAdmin_Returns403()
    {
        // ARRANGE - Register second user (not admin)
        var registerRequest = new RegisterRequest
        {
            Username = "RegularUser",
            Email = $"regular_{Guid.NewGuid()}@test.com",
            Password = "Password123"
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        var regularClient = _factory.CreateClient();
        regularClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var request = new CreateNoteRequest
        {
            Title = "Regular User Post",
            Content = "Should fail"
        };

        // ACT
        var response = await regularClient.PostAsJsonAsync("/api/notes", request);

        // ASSERT - Forbidden (authenticated but not admin)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ============================================================================
    // UPDATE (PUT) - ADMIN ONLY
    // ============================================================================

    [Fact]
    public async Task UpdateNote_AdminUser_Returns200()
    {
        // ARRANGE - Create note first
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest
        {
            Title = "Original Title",
            Content = "Original content"
        };
        
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated Title",
            Content = "Updated content"
        };

        // ACT
        var response = await adminClient.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedNote = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(updatedNote);
        Assert.Equal("Updated Title", updatedNote.Title);
        Assert.Equal("updated-title", updatedNote.Slug); // Slug regenerated
    }

    [Fact]
    public async Task UpdateNote_Unauthenticated_Returns401()
    {
        // ARRANGE - Create note as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "Test", Content = "Test" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        var updateRequest = new UpdateNoteRequest { Title = "Hacked", Content = "Hacked" };

        // ACT - Try to update without auth
        var response = await _client.PutAsJsonAsync($"/api/notes/{note.Id}", updateRequest);

        // ASSERT
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_NonAdmin_Returns403()
    {
        // ARRANGE - Create note as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "Admin Note", Content = "Content" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        // Register non-admin user
        var registerRequest = new RegisterRequest
        {
            Username = "NonAdmin",
            Email = $"nonadmin_{Guid.NewGuid()}@test.com",
            Password = "Pass123"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        
        var nonAdminClient = _factory.CreateClient();
        nonAdminClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

        var updateRequest = new UpdateNoteRequest { Title = "Hacked", Content = "Hacked" };

        // ACT - Try to update as non-admin
        var response = await nonAdminClient.PutAsJsonAsync($"/api/notes/{note.Id}", updateRequest);

        // ASSERT
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ============================================================================
    // DELETE - ADMIN ONLY
    // ============================================================================

    [Fact]
    public async Task DeleteNote_AdminUser_Returns204()
    {
        // ARRANGE - Create note
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "To Delete", Content = "Will be deleted" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        // ACT
        var response = await adminClient.DeleteAsync($"/api/notes/{note.Id}");

        // ASSERT
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify actually deleted (public GET should return 404)
        var getResponse = await _client.GetAsync($"/api/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_Unauthenticated_Returns401()
    {
        // ARRANGE - Create note as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "Protected", Content = "Can't delete" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        // ACT - Try to delete without auth
        var response = await _client.DeleteAsync($"/api/notes/{note.Id}");

        // ASSERT
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_NonAdmin_Returns403()
    {
        // ARRANGE - Create note as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "Protected", Content = "Can't delete" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        // Register non-admin
        var registerRequest = new RegisterRequest
        {
            Username = "NonAdmin2",
            Email = $"nonadmin2_{Guid.NewGuid()}@test.com",
            Password = "Pass123"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        
        var nonAdminClient = _factory.CreateClient();
        nonAdminClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

        // ACT - Try to delete as non-admin
        var response = await nonAdminClient.DeleteAsync($"/api/notes/{note.Id}");

        // ASSERT
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ============================================================================
    // READ (GET) - PUBLIC (NO AUTH REQUIRED)
    // ============================================================================

    [Fact]
    public async Task GetAllNotes_Public_Returns200()
    {
        // ARRANGE - Create some notes as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        await adminClient.PostAsJsonAsync("/api/notes", 
            new CreateNoteRequest { Title = "Public Note 1", Content = "Content 1" });
        await adminClient.PostAsJsonAsync("/api/notes", 
            new CreateNoteRequest { Title = "Public Note 2", Content = "Content 2" });

        // ACT - Public access (no auth)
        var response = await _client.GetAsync("/api/notes");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);
        Assert.True(notes.Count >= 2); // At least our 2 notes
    }

    [Fact]
    public async Task GetNoteById_Public_Returns200()
    {
        // ARRANGE - Create note as admin
        var adminClient = await GetAuthenticatedAdminClientAsync();
        var createRequest = new CreateNoteRequest { Title = "Public Note", Content = "Anyone can read" };
        var createResponse = await adminClient.PostAsJsonAsync("/api/notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);

        // ACT - Public access (no auth)
        var response = await _client.GetAsync($"/api/notes/{note.Id}");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var retrievedNote = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(retrievedNote);
        Assert.Equal("Public Note", retrievedNote.Title);
        Assert.Equal("public-note", retrievedNote.Slug);
    }

    [Fact]
    public async Task GetNoteById_NotFound_Returns404()
    {
        // ACT - Request non-existent note (public access)
        var response = await _client.GetAsync("/api/notes/999999");

        // ASSERT
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
