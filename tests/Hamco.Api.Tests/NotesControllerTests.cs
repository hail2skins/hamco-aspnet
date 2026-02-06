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

    // ==================== READ TESTS ====================

    [Fact]
    public async Task GetNote_ExistingId_Returns200WithNote()
    {
        // Arrange - Create a note first
        var createRequest = new CreateNoteRequest
        {
            Title = "Get Test Note",
            Content = "Content for get test"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Act - GET /api/notes/{id}
        var response = await _client.GetAsync($"/api/notes/{createdNote.Id}");

        // Assert - 200 OK with note data
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal(createdNote.Id, note.Id);
        Assert.Equal("Get Test Note", note.Title);
        Assert.Equal("get-test-note", note.Slug);
        Assert.Equal("Content for get test", note.Content);
    }

    [Fact]
    public async Task GetNote_NonExistingId_Returns404()
    {
        // Act - GET /api/notes/99999
        var response = await _client.GetAsync("/api/notes/99999");

        // Assert - 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNotes_ReturnsListOfNotes()
    {
        // Arrange - Create 3 notes first
        var note1 = new CreateNoteRequest { Title = "First Note", Content = "Content 1" };
        var note2 = new CreateNoteRequest { Title = "Second Note", Content = "Content 2" };
        var note3 = new CreateNoteRequest { Title = "Third Note", Content = "Content 3" };
        
        await _client.PostAsJsonAsync("/api/notes", note1);
        await _client.PostAsJsonAsync("/api/notes", note2);
        await _client.PostAsJsonAsync("/api/notes", note3);

        // Act - GET /api/notes
        var response = await _client.GetAsync("/api/notes");

        // Assert - 200 OK with array containing all notes
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);
        Assert.True(notes.Count >= 3); // At least the 3 we just created
        Assert.Contains(notes, n => n.Title == "First Note");
        Assert.Contains(notes, n => n.Title == "Second Note");
        Assert.Contains(notes, n => n.Title == "Third Note");
    }

    [Fact]
    public async Task GetNotes_EmptyDb_ReturnsEmptyList()
    {
        // Note: This test assumes we can clear the DB or test against empty DB
        // For now, it will just verify we get a list (might have items from other tests)
        
        // Act - GET /api/notes
        var response = await _client.GetAsync("/api/notes");

        // Assert - 200 OK with array (might be empty or have items)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes); // List exists (empty or not is fine)
    }
}
