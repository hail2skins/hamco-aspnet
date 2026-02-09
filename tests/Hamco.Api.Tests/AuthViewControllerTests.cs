using System.Net;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Integration tests for AuthViewController UI endpoints.
/// Tests the login and registration page views.
/// </summary>
public class AuthViewControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthViewControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }
    
    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    // ============================================================================
    // LOGIN PAGE TESTS (GET /auth/login)
    // ============================================================================

    /// <summary>
    /// Test: Login page returns 200 OK.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsSuccessAndCorrectContentType()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");

        // ASSERT: Should return 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Should return HTML content
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    /// <summary>
    /// Test: Login page contains email and password input fields.
    /// </summary>
    [Fact]
    public async Task Login_ContainsEmailAndPasswordFields()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT: Page should contain form fields
        // Email field
        Assert.Contains("type=\"email\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"email\"", content, StringComparison.OrdinalIgnoreCase);
        
        // Password field
        Assert.Contains("type=\"password\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"password\"", content, StringComparison.OrdinalIgnoreCase);
        
        // Form element
        Assert.Contains("<form", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: Login page contains submit button.
    /// </summary>
    [Fact]
    public async Task Login_ContainsSubmitButton()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT: Page should contain submit button
        Assert.Contains("type=\"submit\"", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: Login page contains "Forgot password?" link placeholder.
    /// </summary>
    [Fact]
    public async Task Login_ContainsForgotPasswordLink()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT: Page should contain "Forgot password?" text
        Assert.Contains("Forgot password", content, StringComparison.OrdinalIgnoreCase);
        
        // Should be a link or disabled element
        Assert.True(
            content.Contains("href=\"#\"", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("disabled", StringComparison.OrdinalIgnoreCase),
            "Forgot password link should be a placeholder (href='#' or disabled)"
        );
    }

    /// <summary>
    /// Test: Login page contains client-side JavaScript for form submission.
    /// </summary>
    [Fact]
    public async Task Login_ContainsClientSideJavaScript()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT: Page should contain JavaScript that POSTs to /api/auth/login
        Assert.Contains("/api/auth/login", content);
        
        // Should contain fetch or XMLHttpRequest
        Assert.True(
            content.Contains("fetch(", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("XMLHttpRequest", StringComparison.OrdinalIgnoreCase),
            "Page should contain client-side JavaScript for AJAX submission"
        );
    }

    /// <summary>
    /// Test: Login page uses Bootstrap Clean Blog theme styling.
    /// </summary>
    [Fact]
    public async Task Login_UsesBootstrapStyling()
    {
        // ACT: GET /auth/login
        var response = await _client.GetAsync("/auth/login");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT: Page should contain Bootstrap classes
        Assert.Contains("form-control", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("btn", content, StringComparison.OrdinalIgnoreCase);
        
        // Should use form-floating for modern Bootstrap forms
        Assert.Contains("form-floating", content, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================================
    // REGISTER PAGE TESTS (GET /auth/register)
    // ============================================================================

    /// <summary>
    /// Test: Register page returns 200 OK.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsSuccessAndCorrectContentType()
    {
        // ACT: GET /auth/register
        var response = await _client.GetAsync("/auth/register");

        // ASSERT: Should return 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Should return HTML content
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }
}
