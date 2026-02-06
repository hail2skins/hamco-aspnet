using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hamco.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hamco.Api.Tests;

public class NotesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NotesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateNote_ValidRequest_Returns201WithNote()
    {
        // Arrange - First, register and login to get auth token
        var registerRequest = new RegisterRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Test123!@#"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Test123!@#"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        Assert.NotNull(authResponse);
        Assert.NotNull(authResponse.Token);

        // Add auth token to request
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

        var createNoteRequest = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "This is test content"
        };

        // Act - Create the note
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var noteResponse = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(noteResponse);
        Assert.True(noteResponse.Id > 0);
        Assert.Equal("Test Note", noteResponse.Title);
        Assert.Equal("test-note", noteResponse.Slug); // Slug auto-generated from title
        Assert.Equal("This is test content", noteResponse.Content);
        Assert.NotNull(noteResponse.UserId);
        Assert.True(noteResponse.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task CreateNote_Unauthorized_Returns401()
    {
        // Arrange - no auth token
        var createNoteRequest = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "This is test content"
        };

        // Act - Try to create note without auth
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_InvalidData_Returns400()
    {
        // Arrange - Register and login first
        var registerRequest = new RegisterRequest
        {
            Username = "testuser2",
            Email = "test2@example.com",
            Password = "Test123!@#"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "test2@example.com",
            Password = "Test123!@#"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Create request with empty title (invalid)
        var createNoteRequest = new CreateNoteRequest
        {
            Title = "", // Invalid - empty title
            Content = "This is test content"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
