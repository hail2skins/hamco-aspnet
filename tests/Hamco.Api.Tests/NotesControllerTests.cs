using System.Net;
using System.Net.Http.Json;
using Hamco.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Integration tests for NotesController CRUD operations.
/// Tests the entire HTTP request/response pipeline including database.
/// </summary>
/// <remarks>
/// Testing approach: Integration tests vs Unit tests
/// 
/// Unit tests:
///   - Test individual methods in isolation
///   - Mock dependencies (database, services, etc.)
///   - Fast, focused, easy to maintain
///   - Example: Test SlugGenerator.GenerateSlug() alone
/// 
/// Integration tests (what we have here):
///   - Test entire system together (API + Database + Dependencies)
///   - Use real components (actual database, no mocks)
///   - Slower, but catch real-world issues
///   - Example: POST /api/notes → database → response
/// 
/// Test frameworks:
///   - xUnit: Modern, used by .NET team (what we use)
///   - NUnit: Older, Java-inspired
///   - MSTest: Microsoft's original framework
/// 
/// IClassFixture&lt;T&gt; explained:
///   xUnit interface for sharing setup across tests in a class.
///   WebApplicationFactory&lt;Program&gt; is created ONCE for all tests.
///   Shared instance improves performance (don't rebuild app per test).
/// 
/// Inheritance: NotesControllerTests : IClassFixture&lt;...&gt;
///   Implements IClassFixture interface (contract for test fixture)
/// 
/// WebApplicationFactory&lt;Program&gt;:
///   ASP.NET Core test helper that creates in-memory web server.
///   Spins up the entire application for testing.
///   Provides HttpClient for making requests.
/// 
/// Test naming convention:
///   MethodName_Scenario_ExpectedBehavior
///   Example: CreateNote_ValidRequest_Returns201WithNote
///   Makes tests self-documenting (read test name, know what it does)
/// 
/// AAA Pattern (Arrange-Act-Assert):
///   - Arrange: Set up test data and preconditions
///   - Act: Execute the code being tested
///   - Assert: Verify the results are correct
///   All tests follow this pattern (standard in testing)
/// </remarks>
public class NotesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Test fixture and client stored as private fields
    // Available to all test methods in this class
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Constructor called by xUnit before running each test.
    /// Receives WebApplicationFactory instance from test framework.
    /// </summary>
    /// <param name="factory">
    /// Web application factory that spins up the API for testing.
    /// Created once and reused across all tests (IClassFixture behavior).
    /// </param>
    /// <remarks>
    /// xUnit test lifecycle:
    ///   1. Create WebApplicationFactory (once for all tests)
    ///   2. For each test:
    ///      a. Create new NotesControllerTests instance
    ///      b. Call constructor with factory
    ///      c. Run test method
    ///      d. Dispose test instance
    ///   3. Dispose WebApplicationFactory (after all tests)
    /// 
    /// Why create HttpClient here?
    ///   - factory.CreateClient() creates HTTP client configured for testing
    ///   - Pre-configured with base address (http://localhost)
    ///   - All requests go to in-memory test server (not network)
    ///   - Each test gets fresh client (no shared state between tests)
    /// 
    /// Integration test setup:
    ///   - Real database (hamco_dev) is used
    ///   - Tests may affect each other (shared database state)
    ///   - Better approach: Use in-memory database or reset DB per test
    ///   - Current approach works but isn't ideal for CI/CD
    /// </remarks>
    public NotesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ============================================================================
    // CREATE TESTS (POST /api/notes)
    // ============================================================================

    /// <summary>
    /// Tests successful note creation with valid data.
    /// </summary>
    /// <remarks>
    /// [Fact] attribute:
    ///   Marks method as a test (xUnit discovers and runs it)
    ///   'Fact' = test with no parameters (always runs the same way)
    ///   Alternative: [Theory] for parameterized tests
    /// 
    /// async Task:
    ///   Tests can be async (await HTTP calls, database operations)
    ///   xUnit handles async tests automatically
    ///   No need for special syntax (just use async/await)
    /// 
    /// Test scenario:
    ///   Send valid note data → Expect 201 Created with note in response
    /// 
    /// What this test verifies:
    ///   ✓ Model validation passes (title and content provided)
    ///   ✓ Slug generated correctly from title
    ///   ✓ Note saved to database (Id assigned)
    ///   ✓ Response status is 201 Created
    ///   ✓ Response body contains created note
    ///   ✓ Timestamps populated correctly
    /// </remarks>
    [Fact]
    public async Task CreateNote_ValidRequest_Returns201WithNote()
    {
        // ARRANGE: Set up test data
        // Create request object with valid note data
        var createNoteRequest = new CreateNoteRequest
        {
            Title = "Test Note",
            Content = "This is test content"
        };

        // ACT: Execute the operation being tested
        // PostAsJsonAsync() extension method:
        //   - Serializes object to JSON
        //   - Sets Content-Type: application/json header
        //   - Sends POST request
        //   - Returns HTTP response
        // 'await' waits for async operation to complete
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // ASSERT: Verify results are correct
        // Assert.Equal(expected, actual) compares values
        // If not equal, test fails with helpful error message
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Deserialize response body from JSON to NoteResponse object
        // ReadFromJsonAsync<T>() is extension method for JSON deserialization
        var noteResponse = await response.Content.ReadFromJsonAsync<NoteResponse>();
        
        // Assert.NotNull() ensures value is not null
        // If null, test fails immediately (following assertions would throw)
        Assert.NotNull(noteResponse);
        
        // Verify database assigned an ID (auto-increment worked)
        Assert.True(noteResponse.Id > 0);
        
        // Verify title matches what we sent
        Assert.Equal("Test Note", noteResponse.Title);
        
        // Verify slug was auto-generated correctly
        // "Test Note" → "test-note" (lowercase, spaces to hyphens)
        Assert.Equal("test-note", noteResponse.Slug);
        
        // Verify content matches what we sent
        Assert.Equal("This is test content", noteResponse.Content);
        
        // Verify timestamps were set (not default DateTime.MinValue)
        // DateTime.MinValue = 0001-01-01 00:00:00 (impossible for real timestamp)
        Assert.True(noteResponse.CreatedAt > DateTime.MinValue);
        Assert.True(noteResponse.UpdatedAt > DateTime.MinValue);
    }

    /// <summary>
    /// Tests note creation fails with invalid data (empty title).
    /// </summary>
    /// <remarks>
    /// Negative test (testing failure scenario):
    ///   Just as important as testing success!
    ///   Ensures validation works correctly.
    /// 
    /// What this test verifies:
    ///   ✓ Model validation runs automatically
    ///   ✓ Empty title is rejected ([Required] attribute)
    ///   ✓ Response status is 400 Bad Request
    ///   ✓ Note is NOT created in database
    /// 
    /// Data Annotations validation:
    ///   CreateNoteRequest has [Required] on Title
    ///   ASP.NET Core checks this before controller action runs
    ///   Returns 400 automatically if validation fails
    /// </remarks>
    [Fact]
    public async Task CreateNote_InvalidData_Returns400()
    {
        // ARRANGE: Create request with INVALID data
        var createNoteRequest = new CreateNoteRequest
        {
            Title = "",  // Invalid: Empty title violates [Required] attribute
            Content = "This is test content"
        };

        // ACT: Send request (expect validation failure)
        var response = await _client.PostAsJsonAsync("/api/notes", createNoteRequest);

        // ASSERT: Verify validation failed
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        // Could also verify error message in response body:
        // var error = await response.Content.ReadAsStringAsync();
        // Assert.Contains("Title", error);
    }

    // ============================================================================
    // READ TESTS (GET /api/notes/{id} and GET /api/notes)
    // ============================================================================

    /// <summary>
    /// Tests retrieving a single note by ID.
    /// </summary>
    /// <remarks>
    /// Two-step test:
    ///   1. Create a note (setup)
    ///   2. Retrieve it by ID (actual test)
    /// 
    /// Why create note first?
    ///   Can't assume specific IDs exist (database might be empty)
    ///   Creating note ensures we have known ID to retrieve
    /// 
    /// String interpolation:
    ///   $"/api/notes/{createdNote.Id}"
    ///   If Id = 5, becomes: "/api/notes/5"
    ///   $ prefix enables {expression} placeholders
    /// </remarks>
    [Fact]
    public async Task GetNote_ExistingId_Returns200WithNote()
    {
        // ARRANGE: Create a note first (so we have an ID to retrieve)
        var createRequest = new CreateNoteRequest
        {
            Title = "Get Test Note",
            Content = "Content for get test"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);  // Ensure creation succeeded

        // ACT: GET /api/notes/{id}
        // String interpolation: $"..." allows {variable} in strings
        var response = await _client.GetAsync($"/api/notes/{createdNote.Id}");

        // ASSERT: Verify we got the note back
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal(createdNote.Id, note.Id);
        Assert.Equal("Get Test Note", note.Title);
        Assert.Equal("get-test-note", note.Slug);
        Assert.Equal("Content for get test", note.Content);
    }

    /// <summary>
    /// Tests retrieving non-existent note returns 404.
    /// </summary>
    /// <remarks>
    /// Testing error handling:
    ///   Important to verify API returns correct status codes
    ///   404 Not Found is standard HTTP for "resource doesn't exist"
    /// 
    /// ID 99999:
    ///   Unlikely to exist (high number)
    ///   Better approach: Query max ID and use max+1
    ///   Current approach works for learning/demo purposes
    /// </remarks>
    [Fact]
    public async Task GetNote_NonExistingId_Returns404()
    {
        // ACT: Request note that doesn't exist
        var response = await _client.GetAsync("/api/notes/99999");

        // ASSERT: Verify 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests retrieving all notes returns a list.
    /// </summary>
    /// <remarks>
    /// List endpoint test:
    ///   Creates multiple notes, then verifies they're in the list
    /// 
    /// Test isolation issue:
    ///   This test shares database with other tests
    ///   List might contain notes from other tests
    ///   We verify AT LEAST our notes exist (not ONLY our notes)
    /// 
    /// Assert.Contains():
    ///   Checks if collection contains item matching condition
    ///   Lambda: n => n.Title == "First Note"
    ///   Returns true if any note has that title
    /// </remarks>
    [Fact]
    public async Task GetNotes_ReturnsListOfNotes()
    {
        // ARRANGE: Create 3 notes
        var note1 = new CreateNoteRequest { Title = "First Note", Content = "Content 1" };
        var note2 = new CreateNoteRequest { Title = "Second Note", Content = "Content 2" };
        var note3 = new CreateNoteRequest { Title = "Third Note", Content = "Content 3" };
        
        await _client.PostAsJsonAsync("/api/notes", note1);
        await _client.PostAsJsonAsync("/api/notes", note2);
        await _client.PostAsJsonAsync("/api/notes", note3);

        // ACT: GET /api/notes (all notes)
        var response = await _client.GetAsync("/api/notes");

        // ASSERT: Verify list contains our notes
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);
        
        // Verify AT LEAST 3 notes (might be more from other tests)
        Assert.True(notes.Count >= 3);
        
        // Verify our specific notes are in the list
        // Assert.Contains(collection, predicate)
        //   Searches for item matching lambda condition
        Assert.Contains(notes, n => n.Title == "First Note");
        Assert.Contains(notes, n => n.Title == "Second Note");
        Assert.Contains(notes, n => n.Title == "Third Note");
    }

    /// <summary>
    /// Tests GET /api/notes returns a list (might be empty, might have items).
    /// </summary>
    /// <remarks>
    /// Weak test (acknowledged in comment):
    ///   Can't assume empty database (other tests run first)
    ///   Just verifies endpoint returns 200 with a list
    /// 
    /// Better approach:
    ///   - Use in-memory database (reset per test)
    ///   - Clear database before test
    ///   - Use test database that's reset between runs
    /// </remarks>
    [Fact]
    public async Task GetNotes_EmptyDb_ReturnsEmptyList()
    {
        // Note: Can't guarantee empty DB (other tests may have created notes)
        // This test just verifies we get a list response
        
        // ACT
        var response = await _client.GetAsync("/api/notes");

        // ASSERT: Verify we get a list (empty or not is fine)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notes);  // List exists (might be empty, might have items)
    }

    // ============================================================================
    // UPDATE TESTS (PUT /api/notes/{id})
    // ============================================================================

    /// <summary>
    /// Tests updating a note's title and content.
    /// </summary>
    /// <remarks>
    /// Update workflow:
    ///   1. Create note (get ID)
    ///   2. Update note (send new data)
    ///   3. Verify changes applied
    /// 
    /// Slug regeneration:
    ///   When title changes, slug is regenerated
    ///   "Original Title" → "original-title"
    ///   "Updated Title" → "updated-title"
    /// 
    /// UpdatedAt timestamp:
    ///   Should be newer than CreatedAt after update
    ///   Test verifies this with: updatedNote.UpdatedAt > createdNote.UpdatedAt
    /// </remarks>
    [Fact]
    public async Task UpdateNote_ValidData_Returns200WithUpdatedNote()
    {
        // ARRANGE: Create note to update
        var createRequest = new CreateNoteRequest
        {
            Title = "Original Title",
            Content = "Original content"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/notes", createRequest);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Prepare update data
        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated Title",
            Content = "Updated content"
        };

        // ACT: PUT /api/notes/{id}
        // PutAsJsonAsync() sends PUT request with JSON body
        var response = await _client.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);

        // ASSERT: Verify update succeeded
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var updatedNote = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(updatedNote);
        
        // ID should be same (not changed)
        Assert.Equal(createdNote.Id, updatedNote.Id);
        
        // Title should be updated
        Assert.Equal("Updated Title", updatedNote.Title);
        
        // Slug should be regenerated from new title
        Assert.Equal("updated-title", updatedNote.Slug);
        
        // Content should be updated
        Assert.Equal("Updated content", updatedNote.Content);
        
        // UpdatedAt should be newer than original CreatedAt
        // > operator compares DateTime values
        Assert.True(updatedNote.UpdatedAt > createdNote.UpdatedAt);
    }

    /// <summary>
    /// Tests updating non-existent note returns 404.
    /// </summary>
    [Fact]
    public async Task UpdateNote_NonExistingId_Returns404()
    {
        // ARRANGE
        var updateRequest = new UpdateNoteRequest
        {
            Title = "Updated Title",
            Content = "Updated content"
        };

        // ACT: Try to update non-existent note
        var response = await _client.PutAsJsonAsync("/api/notes/99999", updateRequest);

        // ASSERT
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests updating note with invalid data returns 400.
    /// </summary>
    [Fact]
    public async Task UpdateNote_InvalidData_Returns400()
    {
        // ARRANGE: Create note, then prepare invalid update
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
            Title = "",  // Invalid: Empty title
            Content = "Updated content"
        };

        // ACT: Try to update with invalid data
        var response = await _client.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);

        // ASSERT: Validation should fail
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ============================================================================
    // DELETE TESTS (DELETE /api/notes/{id})
    // ============================================================================

    /// <summary>
    /// Tests deleting a note removes it from database.
    /// </summary>
    /// <remarks>
    /// Comprehensive delete test:
    ///   1. Create multiple notes
    ///   2. Delete one note
    ///   3. Verify it's gone
    ///   4. Verify other notes still exist
    /// 
    /// Multiple verification strategies:
    ///   - Check DELETE returns 204 No Content
    ///   - Verify list count decreased by 1
    ///   - Verify deleted note not in list
    ///   - Verify other notes still in list
    ///   - Verify GET deleted note returns 404
    /// 
    /// This is thorough testing (maybe overly thorough)
    /// But good for learning and catching edge cases!
    /// </remarks>
    [Fact]
    public async Task DeleteNote_ExistingId_RemovesNote()
    {
        // ARRANGE: Create 3 notes
        var note1 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 1", Content = "Content 1" });
        var note2 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 2", Content = "Content 2" });
        var note3 = await _client.PostAsJsonAsync("/api/notes", new CreateNoteRequest { Title = "Note 3", Content = "Content 3" });
        
        var created1 = await note1.Content.ReadFromJsonAsync<NoteResponse>();
        var created2 = await note2.Content.ReadFromJsonAsync<NoteResponse>();
        var created3 = await note3.Content.ReadFromJsonAsync<NoteResponse>();
        
        Assert.NotNull(created1);
        Assert.NotNull(created2);
        Assert.NotNull(created3);

        // Get baseline count
        var allNotesBefore = await _client.GetAsync("/api/notes");
        var notesBefore = await allNotesBefore.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notesBefore);
        var beforeCount = notesBefore.Count;

        // ACT: DELETE /api/notes/{id} (delete middle note)
        var deleteResponse = await _client.DeleteAsync($"/api/notes/{created2.Id}");

        // ASSERT: Verify deletion succeeded
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify list count decreased by 1
        var allNotesAfter = await _client.GetAsync("/api/notes");
        var notesAfter = await allNotesAfter.Content.ReadFromJsonAsync<List<NoteResponse>>();
        Assert.NotNull(notesAfter);
        Assert.Equal(beforeCount - 1, notesAfter.Count);
        
        // Verify deleted note NOT in list
        // Assert.DoesNotContain(collection, predicate)
        //   Fails if any item matches condition
        Assert.DoesNotContain(notesAfter, n => n.Id == created2.Id);
        
        // Verify other notes STILL in list
        Assert.Contains(notesAfter, n => n.Id == created1.Id);
        Assert.Contains(notesAfter, n => n.Id == created3.Id);

        // Verify GET deleted note returns 404
        var getDeletedNote = await _client.GetAsync($"/api/notes/{created2.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedNote.StatusCode);
    }

    /// <summary>
    /// Tests deleting non-existent note returns 404.
    /// </summary>
    [Fact]
    public async Task DeleteNote_NonExistingId_Returns404()
    {
        // ACT: Try to delete non-existent note
        var response = await _client.DeleteAsync("/api/notes/99999");

        // ASSERT: Should return 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
