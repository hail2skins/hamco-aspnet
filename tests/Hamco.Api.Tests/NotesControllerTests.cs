using System.Net;
using System.Net.Http.Json;
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
        // Arrange
        var createNoteRequest = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "This is test content"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var noteResponse = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(noteResponse);
        Assert.True(noteResponse.Id > 0);
        Assert.Equal("Test Note", noteResponse.Title);
        Assert.Equal("test-note", noteResponse.Slug); // Slug auto-generated from title
        Assert.Equal("This is test content", noteResponse.Content);
        Assert.True(noteResponse.CreatedAt > DateTime.MinValue);
        Assert.True(noteResponse.UpdatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task CreateNote_InvalidData_Returns400()
    {
        // Arrange - empty title (invalid)
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
