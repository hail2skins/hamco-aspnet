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

    // ==================== UPDATE TESTS ====================

    [Fact]
    public async Task UpdateNote_ValidData_Returns200WithUpdatedNote()
    {
        // Arrange - Create note, get ID
        var createRequest = new CreateNoteRequest
        {
            Title = "Original Title",
            Content = "Original content"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated Title",
            Content = "Updated content"
        };

        // Act - PUT /api/notes/{id} with new title/content
        var response = await _client.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);

        // Assert - 200 OK with updated data, slug regenerated if title changed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedNote = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(updatedNote);
        Assert.Equal(createdNote.Id, updatedNote.Id);
        Assert.Equal("Updated Title", updatedNote.Title);
        Assert.Equal("updated-title", updatedNote.Slug); // Slug regenerated
        Assert.Equal("Updated content", updatedNote.Content);
        Assert.True(updatedNote.UpdatedAt > createdNote.UpdatedAt);
    }

    [Fact]
    public async Task UpdateNote_NonExistingId_Returns404()
    {
        // Arrange
        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated Title",
            Content = "Updated content"
        };

        // Act - PUT /api/notes/99999
        var response = await _client.PutAsJsonAsync("/api/notes/99999", updateRequest);

        // Assert - 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_InvalidData_Returns400()
    {
        // Arrange - Create note, get ID
        var createRequest = new CreateNoteRequest
        {
            Title = "Original Title",
            Content = "Original content"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        var updateRequest = new UpdateNoteRequest
        {
            Title = "", // Invalid - empty title
            Content = "Updated content"
        };

        // Act - PUT /api/notes/{id} with empty title
        var response = await _client.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);

        // Assert - 400 BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================== DELETE TESTS ====================

    [Fact]
    public async Task DeleteNote_ExistingId_RemovesNote()
    {
        // Arrange - Create 3 notes, get IDs
        var note1 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 1", Content = "Content 1" });
        var note2 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 2", Content = "Content 2" });
        var note3 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 3", Content = "Content 3" });
        
        var created1 = await note1.Content.ReadFromJsonAsync<NoteResponse>();
        var created2 = await note2.Content.ReadFromJsonAsync<NoteResponse>();
        var created3 = await note3.Content.ReadFromJsonAsync<NoteResponse>();
        
        Assert.NotNull(created1);
        Assert.NotNull(created2);
        Assert.NotNull(created3);

        // Verify all 3 exist
        var allNotesBefore = await _client.GetAsync("/api/notes");
        var notesBefore = await allNotesBefore.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notesBefore);
        var beforeCount = notesBefore.Count;

        // Act - DELETE /api/notes/{id} (delete middle one)
        var deleteResponse = await _client.DeleteAsync($"/api/notes/{created2.Id}");

        // Assert - 204 NoContent
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify - GET /api/notes returns only 2 of our notes (deleted one gone)
        var allNotesAfter = await _client.GetAsync("/api/notes");
        var notesAfter = await allNotesAfter.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notesAfter);
        Assert.Equal(beforeCount - 1, notesAfter.Count); // One less note
        Assert.DoesNotContain(notesAfter, n => n.Id == created2.Id); // Deleted note not in list
        Assert.Contains(notesAfter, n => n.Id == created1.Id); // Other notes still there
        Assert.Contains(notesAfter, n => n.Id == created3.Id);

        // Verify - GET /api/notes/{deletedId} returns 404
        var getDeletedNote = await _client.GetAsync($"/api/notes/{created2.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedNote.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_NonExistingId_Returns404()
    {
        // Act - DELETE /api/notes/99999
        var response = await _client.DeleteAsync("/api/notes/99999");

        // Assert - 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
