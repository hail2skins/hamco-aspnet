using Hamco.Api;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using HtmlAgilityPack;

namespace Hamco.Api.Tests.Controllers.Admin;

/// <summary>
/// Integration tests for AdminNotesController (MVC CRUD pages).
/// Tests all CRUD operations with 3 authentication scenarios:
/// 1. Admin user JWT - all operations succeed (200)
/// 2. Non-admin user JWT - all operations fail (403)
/// 3. Anonymous - all operations fail (302 redirect to login)
/// </summary>
/// <remarks>
/// Why MVC tests instead of API tests?
///   - This controller returns HTML views, not JSON
///   - We test status codes and basic rendering
///   - We verify forms and buttons exist
///   - We test actual create/update/delete operations
/// 
/// Test Strategy:
///   - Integration tests (full HTTP stack)
///   - SQLite in-memory database (isolated, fast)
///   - WebApplicationFactory (real server, real auth)
///   - HtmlAgilityPack to parse and verify HTML content
/// 
/// Pages under test:
///   GET  /admin/notes          - List all notes
///   GET  /admin/notes/create   - Create form
///   POST /admin/notes/create   - Create action
///   GET  /admin/notes/edit/{id}   - Edit form
///   POST /admin/notes/edit/{id}   - Edit action
///   GET  /admin/notes/delete/{id} - Delete confirmation
///   POST /admin/notes/delete/{id} - Delete action
/// </remarks>
public class AdminNotesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private HamcoDbContext _context = null!;
    
    // Admin user credentials
    private string _adminToken = string.Empty;
    private string _adminUserId = string.Empty;
    
    // Non-admin user credentials
    private string _userToken = string.Empty;

    public AdminNotesControllerTests(WebApplicationFactory<Program> factory)
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

                // Ensure services are registered
                if (!services.Any(s => s.ServiceType == typeof(IPasswordHasher)))
                {
                    services.AddScoped<IPasswordHasher, PasswordHasher>();
                }
                if (!services.Any(s => s.ServiceType == typeof(IJwtService)))
                {
                    services.AddScoped<IJwtService, JwtService>();
                }
                if (!services.Any(s => s.ServiceType == typeof(IMarkdownService)))
                {
                    services.AddScoped<IMarkdownService, MarkdownService>();
                }
                if (!services.Any(s => s.ServiceType == typeof(ISloganRandomizer)))
                {
                    services.AddScoped<ISloganRandomizer, SloganRandomizer>();
                }
                if (!services.Any(s => s.ServiceType == typeof(IImageRandomizer)))
                {
                    services.AddSingleton<IImageRandomizer, ImageRandomizer>();
                }
                
                // Add memory cache for slogan service
                if (!services.Any(s => s.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache)))
                {
                    services.AddMemoryCache();
                }
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Don't follow redirects so we can test them
        });
    }

    /// <summary>
    /// Initialize database and create test users with JWT tokens.
    /// Creates both admin and non-admin accounts.
    /// </summary>
    private async Task InitializeAsync()
    {
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Create admin user
        var adminUser = new User
        {
            Email = "admin@example.com",
            PasswordHash = passwordHasher.HashPassword("admin123"),
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(adminUser);

        // Create regular user
        var regularUser = new User
        {
            Email = "user@example.com",
            PasswordHash = passwordHasher.HashPassword("user123"),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(regularUser);

        await _context.SaveChangesAsync();

        // Generate JWT tokens
        _adminToken = jwtService.GenerateToken(adminUser);
        _adminUserId = adminUser.Id;
        _userToken = jwtService.GenerateToken(regularUser);

        // Create a test note for edit/delete tests
        var testNote = new Note
        {
            Title = "Test Note",
            Slug = "test-note",
            Content = "This is test content",
            UserId = adminUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Notes.Add(testNote);
        await _context.SaveChangesAsync();
    }

    #region Index (List) Tests

    /// <summary>
    /// GET /admin/notes - Admin user should see 200 OK with notes list
    /// </summary>
    [Fact]
    public async Task Index_AdminUser_Returns200WithNotesList()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/notes");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify it's HTML content
        Assert.Contains("<!DOCTYPE html>", html);
        
        // Verify table exists
        Assert.Contains("Test Note", html);
        Assert.Contains("test-note", html);
    }

    /// <summary>
    /// GET /admin/notes - Non-admin user should get 403 Forbidden
    /// </summary>
    [Fact]
    public async Task Index_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        // Act
        var response = await _client.GetAsync("/admin/notes");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// GET /admin/notes - Anonymous user should get 302 redirect to login
    /// </summary>
    [Fact]
    public async Task Index_AnonymousUser_Returns302Redirect()
    {
        // Arrange
        await InitializeAsync();
        // No authorization header

        // Act
        var response = await _client.GetAsync("/admin/notes");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    #endregion

    #region Create Tests

    /// <summary>
    /// GET /admin/notes/create - Admin user should see 200 OK with create form
    /// </summary>
    [Fact]
    public async Task CreateGet_AdminUser_Returns200WithForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/notes/create");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify form elements exist
        Assert.Contains("<form", html);
        Assert.Contains("Title", html);
        Assert.Contains("Slug", html);
        Assert.Contains("Content", html);
    }

    /// <summary>
    /// POST /admin/notes/create - Admin user can create a note
    /// </summary>
    [Fact]
    public async Task CreatePost_AdminUser_CreatesNoteAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var formData = new Dictionary<string, string>
        {
            ["Title"] = "New Admin Note",
            ["Slug"] = "new-admin-note",
            ["Content"] = "This is a new note created by admin"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/notes/create", content);

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/notes", response.Headers.Location?.ToString());

        // Verify note was created in database
        var createdNote = await _context.Notes.FirstOrDefaultAsync(n => n.Title == "New Admin Note");
        Assert.NotNull(createdNote);
        Assert.Equal("new-admin-note", createdNote.Slug);
        Assert.Equal("This is a new note created by admin", createdNote.Content);
        Assert.Equal(_adminUserId, createdNote.UserId);
    }

    /// <summary>
    /// POST /admin/notes/create - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task CreatePost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var formData = new Dictionary<string, string>
        {
            ["Title"] = "Unauthorized Note",
            ["Slug"] = "unauthorized-note",
            ["Content"] = "Should not be created"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/notes/create", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify note was NOT created
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Title == "Unauthorized Note");
        Assert.Null(note);
    }

    /// <summary>
    /// POST /admin/notes/create - Validation errors return to form
    /// </summary>
    [Fact]
    public async Task CreatePost_ValidationError_ReturnsToForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var formData = new Dictionary<string, string>
        {
            ["Title"] = "", // Empty title should fail validation
            ["Slug"] = "invalid",
            ["Content"] = "Content without title"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/notes/create", content);

        // Assert - Should return to create page with validation errors
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Returns form, not redirect
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", html); // Form is still displayed
    }

    #endregion

    #region Edit Tests

    /// <summary>
    /// GET /admin/notes/edit/1 - Admin user should see 200 OK with edit form
    /// </summary>
    [Fact]
    public async Task EditGet_AdminUser_Returns200WithForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var note = await _context.Notes.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/admin/notes/edit/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify form is pre-populated
        Assert.Contains("<form", html);
        Assert.Contains("Test Note", html);
        Assert.Contains("test-note", html);
        Assert.Contains("This is test content", html);
    }

    /// <summary>
    /// POST /admin/notes/edit/1 - Admin user can update a note
    /// </summary>
    [Fact]
    public async Task EditPost_AdminUser_UpdatesNoteAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var note = await _context.Notes.FirstAsync();

        var formData = new Dictionary<string, string>
        {
            ["Title"] = "Updated Note Title",
            ["Slug"] = "updated-note-title",
            ["Content"] = "Updated content"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync($"/admin/notes/edit/{note.Id}", content);

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify note was updated in database
        var updatedNote = await _context.Notes.FindAsync(note.Id);
        Assert.NotNull(updatedNote);
        Assert.Equal("Updated Note Title", updatedNote.Title);
        Assert.Equal("updated-note-title", updatedNote.Slug);
        Assert.Equal("Updated content", updatedNote.Content);
    }

    /// <summary>
    /// POST /admin/notes/edit/1 - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task EditPost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
        
        var note = await _context.Notes.FirstAsync();

        var formData = new Dictionary<string, string>
        {
            ["Title"] = "Hacked Title",
            ["Slug"] = "hacked",
            ["Content"] = "Should not update"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync($"/admin/notes/edit/{note.Id}", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify note was NOT updated
        var unchangedNote = await _context.Notes.FindAsync(note.Id);
        Assert.NotNull(unchangedNote);
        Assert.Equal("Test Note", unchangedNote.Title); // Original title
    }

    /// <summary>
    /// GET /admin/notes/edit/999 - Non-existent note should return 404
    /// </summary>
    [Fact]
    public async Task EditGet_NonExistentNote_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/notes/edit/9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Delete Tests

    /// <summary>
    /// GET /admin/notes/delete/1 - Admin user should see 200 OK with confirmation page
    /// </summary>
    [Fact]
    public async Task DeleteGet_AdminUser_Returns200WithConfirmation()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var note = await _context.Notes.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/admin/notes/delete/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify confirmation page shows note details
        Assert.Contains("Test Note", html);
        Assert.Contains("Delete", html);
    }

    /// <summary>
    /// POST /admin/notes/delete/1 - Admin user can delete a note
    /// </summary>
    [Fact]
    public async Task DeletePost_AdminUser_DeletesNoteAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var note = await _context.Notes.FirstAsync();
        var noteId = note.Id;

        // Act
        var response = await _client.PostAsync($"/admin/notes/delete/{noteId}", new StringContent(""));

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify note was deleted from database
        var deletedNote = await _context.Notes.FindAsync(noteId);
        Assert.Null(deletedNote);
    }

    /// <summary>
    /// POST /admin/notes/delete/1 - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task DeletePost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
        
        var note = await _context.Notes.FirstAsync();

        // Act
        var response = await _client.PostAsync($"/admin/notes/delete/{note.Id}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify note was NOT deleted
        var stillExists = await _context.Notes.FindAsync(note.Id);
        Assert.NotNull(stillExists);
    }

    /// <summary>
    /// GET /admin/notes/delete/999 - Non-existent note should return 404
    /// </summary>
    [Fact]
    public async Task DeleteGet_NonExistentNote_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/notes/delete/9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
