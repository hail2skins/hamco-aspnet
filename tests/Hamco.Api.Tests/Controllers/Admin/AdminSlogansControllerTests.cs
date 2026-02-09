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
/// Integration tests for AdminSlogansController (MVC CRUD pages).
/// Tests all CRUD operations with 3 authentication scenarios:
/// 1. Admin user JWT - all operations succeed (200)
/// 2. Non-admin user JWT - all operations fail (403)
/// 3. Anonymous - all operations fail (401 Unauthorized)
/// </summary>
/// <remarks>
/// Why MVC tests instead of API tests?
///   - This controller returns HTML views, not JSON
///   - We test status codes and basic rendering
///   - We verify forms and buttons exist
///   - We test actual create/update/delete operations
///   - We test IsActive toggle functionality
/// 
/// Test Strategy:
///   - Integration tests (full HTTP stack)
///   - SQLite in-memory database (isolated, fast)
///   - WebApplicationFactory (real server, real auth)
///   - HtmlAgilityPack to parse and verify HTML content
/// 
/// Pages under test:
///   GET  /admin/slogans              - List all slogans
///   GET  /admin/slogans/create       - Create form
///   POST /admin/slogans/create       - Create action
///   GET  /admin/slogans/edit/{id}    - Edit form
///   POST /admin/slogans/edit/{id}    - Edit action
///   GET  /admin/slogans/delete/{id}  - Delete confirmation
///   POST /admin/slogans/delete/{id}  - Delete action
///   POST /admin/slogans/toggle/{id}  - Toggle IsActive
/// </remarks>
public class AdminSlogansControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HamcoDbContext _context = null!;
    
    // Admin user credentials
    private string _adminToken = string.Empty;
    private string _adminUserId = string.Empty;
    
    // Non-admin user credentials
    private string _userToken = string.Empty;

    public AdminSlogansControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Don't follow redirects so we can test them
        });
    }

    /// <summary>
    /// Initialize database and create test users with JWT tokens.
    /// Creates both admin and non-admin accounts.
    /// Clears existing data first to ensure test isolation.
    /// </summary>
    private async Task InitializeAsync()
    {
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        // TestWebApplicationFactory already created the schema
        // Clear any existing data from previous tests (shared database instance)
        _context.Notes.RemoveRange(_context.Notes);
        _context.ApiKeys.RemoveRange(_context.ApiKeys);
        _context.Users.RemoveRange(_context.Users);
        _context.Slogans.RemoveRange(_context.Slogans);
        await _context.SaveChangesAsync();

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

        // Create a test slogan for edit/delete tests
        var testSlogan = new Slogan
        {
            Text = "Test Slogan Content",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id
        };
        _context.Slogans.Add(testSlogan);
        await _context.SaveChangesAsync();
    }

    #region Index (List) Tests

    /// <summary>
    /// GET /admin/slogans - Admin user should see 200 OK with slogans list
    /// </summary>
    [Fact]
    public async Task Index_AdminUser_Returns200WithSlogansList()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify it's HTML content
        Assert.Contains("<!DOCTYPE html>", html);
        
        // Verify slogan content exists
        Assert.Contains("Test Slogan Content", html);
    }

    /// <summary>
    /// GET /admin/slogans - Non-admin user should get 403 Forbidden
    /// </summary>
    [Fact]
    public async Task Index_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        // Act
        var response = await _client.GetAsync("/admin/slogans");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// GET /admin/slogans - Anonymous user should get 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task Index_AnonymousUser_Returns401()
    {
        // Arrange
        await InitializeAsync();
        // No authorization header

        // Act
        var response = await _client.GetAsync("/admin/slogans");

        // Assert
        // JWT Bearer authentication returns 401, not 302 redirect
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Create Tests

    /// <summary>
    /// GET /admin/slogans/create - Admin user should see 200 OK with create form
    /// </summary>
    [Fact]
    public async Task CreateGet_AdminUser_Returns200WithForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/slogans/create");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify form elements exist
        Assert.Contains("<form", html);
        Assert.Contains("Content", html);
        Assert.Contains("IsActive", html);
    }

    /// <summary>
    /// POST /admin/slogans/create - Admin user can create a slogan
    /// </summary>
    [Fact]
    public async Task CreatePost_AdminUser_CreatesSloganAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var formData = new Dictionary<string, string>
        {
            ["Content"] = "New Admin Slogan",
            ["IsActive"] = "true"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/slogans/create", content);

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/slogans", response.Headers.Location?.ToString());

        // Verify slogan was created in database
        var createdSlogan = await _context.Slogans.FirstOrDefaultAsync(s => s.Text == "New Admin Slogan");
        Assert.NotNull(createdSlogan);
        Assert.True(createdSlogan.IsActive);
        Assert.Equal(_adminUserId, createdSlogan.CreatedByUserId);
    }

    /// <summary>
    /// POST /admin/slogans/create - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task CreatePost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var formData = new Dictionary<string, string>
        {
            ["Content"] = "Unauthorized Slogan",
            ["IsActive"] = "true"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/slogans/create", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify slogan was NOT created
        var slogan = await _context.Slogans.FirstOrDefaultAsync(s => s.Text == "Unauthorized Slogan");
        Assert.Null(slogan);
    }

    /// <summary>
    /// POST /admin/slogans/create - Validation errors return to form
    /// </summary>
    [Fact]
    public async Task CreatePost_ValidationError_ReturnsToForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var formData = new Dictionary<string, string>
        {
            ["Content"] = "", // Empty content should fail validation
            ["IsActive"] = "true"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync("/admin/slogans/create", content);

        // Assert - Should return to create page with validation errors
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Returns form, not redirect
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", html); // Form is still displayed
    }

    #endregion

    #region Edit Tests

    /// <summary>
    /// GET /admin/slogans/edit/1 - Admin user should see 200 OK with edit form
    /// </summary>
    [Fact]
    public async Task EditGet_AdminUser_Returns200WithForm()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var slogan = await _context.Slogans.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/admin/slogans/edit/{slogan.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify form is pre-populated
        Assert.Contains("<form", html);
        Assert.Contains("Test Slogan Content", html);
    }

    /// <summary>
    /// POST /admin/slogans/edit/1 - Admin user can update a slogan
    /// </summary>
    [Fact]
    public async Task EditPost_AdminUser_UpdatesSloganAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var slogan = await _context.Slogans.FirstAsync();
        var sloganId = slogan.Id;

        var formData = new Dictionary<string, string>
        {
            ["Content"] = "Updated Slogan Content",
            ["IsActive"] = "false"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync($"/admin/slogans/edit/{sloganId}", content);

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify slogan was updated in database using a fresh context
        using var scope = _factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        var updatedSlogan = await freshContext.Slogans.FindAsync(sloganId);
        Assert.NotNull(updatedSlogan);
        Assert.Equal("Updated Slogan Content", updatedSlogan.Text);
        Assert.False(updatedSlogan.IsActive);
    }

    /// <summary>
    /// POST /admin/slogans/edit/1 - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task EditPost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
        
        var slogan = await _context.Slogans.FirstAsync();

        var formData = new Dictionary<string, string>
        {
            ["Content"] = "Hacked Content",
            ["IsActive"] = "false"
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var response = await _client.PostAsync($"/admin/slogans/edit/{slogan.Id}", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify slogan was NOT updated
        var unchangedSlogan = await _context.Slogans.FindAsync(slogan.Id);
        Assert.NotNull(unchangedSlogan);
        Assert.Equal("Test Slogan Content", unchangedSlogan.Text); // Original content
    }

    /// <summary>
    /// GET /admin/slogans/edit/999 - Non-existent slogan should return 404
    /// </summary>
    [Fact]
    public async Task EditGet_NonExistentSlogan_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/slogans/edit/9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Delete Tests

    /// <summary>
    /// GET /admin/slogans/delete/1 - Admin user should see 200 OK with confirmation page
    /// </summary>
    [Fact]
    public async Task DeleteGet_AdminUser_Returns200WithConfirmation()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var slogan = await _context.Slogans.FirstAsync();

        // Act
        var response = await _client.GetAsync($"/admin/slogans/delete/{slogan.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify confirmation page shows slogan details
        Assert.Contains("Test Slogan Content", html);
        Assert.Contains("Delete", html);
    }

    /// <summary>
    /// POST /admin/slogans/delete/1 - Admin user can delete a slogan
    /// </summary>
    [Fact]
    public async Task DeletePost_AdminUser_DeletesSloganAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var slogan = await _context.Slogans.FirstAsync();
        var sloganId = slogan.Id;

        // Act
        var response = await _client.PostAsync($"/admin/slogans/delete/{sloganId}", new StringContent(""));

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify slogan was deleted from database using a fresh context
        using var scope = _factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        var deletedSlogan = await freshContext.Slogans.FindAsync(sloganId);
        Assert.Null(deletedSlogan);
    }

    /// <summary>
    /// POST /admin/slogans/delete/1 - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task DeletePost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
        
        var slogan = await _context.Slogans.FirstAsync();

        // Act
        var response = await _client.PostAsync($"/admin/slogans/delete/{slogan.Id}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify slogan was NOT deleted
        var stillExists = await _context.Slogans.FindAsync(slogan.Id);
        Assert.NotNull(stillExists);
    }

    /// <summary>
    /// GET /admin/slogans/delete/999 - Non-existent slogan should return 404
    /// </summary>
    [Fact]
    public async Task DeleteGet_NonExistentSlogan_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/admin/slogans/delete/9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Toggle IsActive Tests

    /// <summary>
    /// POST /admin/slogans/toggle/1 - Admin user can toggle IsActive
    /// </summary>
    [Fact]
    public async Task TogglePost_AdminUser_TogglesIsActiveAndRedirects()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        
        var slogan = await _context.Slogans.FirstAsync();
        var sloganId = slogan.Id;
        var originalIsActive = slogan.IsActive;

        // Act
        var response = await _client.PostAsync($"/admin/slogans/toggle/{sloganId}", new StringContent(""));

        // Assert - Should redirect to index
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify IsActive was toggled using a fresh context
        using var scope = _factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        var toggledSlogan = await freshContext.Slogans.FindAsync(sloganId);
        Assert.NotNull(toggledSlogan);
        Assert.NotEqual(originalIsActive, toggledSlogan.IsActive);
    }

    /// <summary>
    /// POST /admin/slogans/toggle/1 - Non-admin user should get 403
    /// </summary>
    [Fact]
    public async Task TogglePost_NonAdminUser_Returns403()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);
        
        var slogan = await _context.Slogans.FirstAsync();

        // Act
        var response = await _client.PostAsync($"/admin/slogans/toggle/{slogan.Id}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// POST /admin/slogans/toggle/999 - Non-existent slogan should return 404
    /// </summary>
    [Fact]
    public async Task TogglePost_NonExistentSlogan_Returns404()
    {
        // Arrange
        await InitializeAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.PostAsync("/admin/slogans/toggle/9999", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
