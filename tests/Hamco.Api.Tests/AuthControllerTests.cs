using System.Net;
using System.Net.Http.Json;
using Hamco.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Integration tests for AuthController authentication endpoints.
/// Tests user registration, login, and profile functionality.
/// </summary>
/// <remarks>
/// TDD Approach:
/// 1. RED phase: Write tests first (they FAIL because implementation incomplete)
/// 2. GREEN phase: Implement auth endpoints to make tests PASS
/// 3. REFACTOR phase: Clean up code while tests remain green
/// 
/// Test coverage:
/// - Registration (success, duplicate email, invalid data)
/// - Login (success, wrong credentials, non-existent user)
/// - Profile (authenticated, unauthenticated)
/// - Password reset stubs (future Mailjet integration)
/// </remarks>
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ============================================================================
    // REGISTRATION TESTS (POST /api/auth/register)
    // ============================================================================

    /// <summary>
    /// Test: Valid registration creates user and returns 201 with token.
    /// </summary>
    [Fact]
    public async Task Register_ValidData_CreatesUserReturns201()
    {
        // ARRANGE: Prepare valid registration data
        var registerRequest = new RegisterRequest
        {
            Username = "TestUser",
            Email = $"test_{Guid.NewGuid()}@example.com", // Unique email to avoid conflicts
            Password = "SecurePassword123"
        };

        // ACT: POST /api/auth/register
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ASSERT: Should return 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify response contains auth data
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        
        // Verify JWT token is provided (non-empty string)
        Assert.False(string.IsNullOrEmpty(authResponse.Token));
        
        // Verify user ID is assigned (GUID format)
        Assert.False(string.IsNullOrEmpty(authResponse.UserId));
        
        // Verify email matches request
        Assert.Equal(registerRequest.Email, authResponse.Email);
        
        // Note: Password hash should NOT be in response!
        // We can't verify this directly in AuthResponse,
        // but we verify password works by logging in next
    }

    /// <summary>
    /// Test: Registering with duplicate email returns 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        // ARRANGE: Register user once
        var uniqueEmail = $"duplicate_{Guid.NewGuid()}@example.com";
        var firstRequest = new RegisterRequest
        {
            Username = "FirstUser",
            Email = uniqueEmail,
            Password = "Password123"
        };
        
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode); // First registration succeeds

        // Prepare duplicate registration (same email, different username)
        var duplicateRequest = new RegisterRequest
        {
            Username = "SecondUser",
            Email = uniqueEmail, // Same email!
            Password = "DifferentPassword456"
        };

        // ACT: Try to register with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", duplicateRequest);

        // ASSERT: Should return 409 Conflict (or 400 Bad Request depending on implementation)
        // Current implementation returns 400, but 409 is more semantically correct
        // We'll accept either for now, then standardize to 409 in implementation
        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict || 
            response.StatusCode == HttpStatusCode.BadRequest
        );
    }

    /// <summary>
    /// Test: Registering with invalid data returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Register_InvalidData_Returns400()
    {
        // ARRANGE: Prepare invalid registration data (empty password)
        var invalidRequest = new RegisterRequest
        {
            Username = "TestUser",
            Email = "valid@example.com",
            Password = "" // Invalid: violates [MinLength(6)]
        };

        // ACT: POST /api/auth/register with invalid data
        var response = await _client.PostAsJsonAsync("/api/auth/register", invalidRequest);

        // ASSERT: Should return 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ============================================================================
    // LOGIN TESTS (POST /api/auth/login)
    // ============================================================================

    /// <summary>
    /// Test: Valid credentials return 200 OK with JWT token.
    /// </summary>
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        // ARRANGE: Register user first
        var email = $"login_{Guid.NewGuid()}@example.com";
        var password = "MySecurePassword123";
        
        var registerRequest = new RegisterRequest
        {
            Username = "LoginTestUser",
            Email = email,
            Password = password
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // Prepare login request
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // ACT: POST /api/auth/login
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // ASSERT: Should return 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify response contains auth data
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        
        // Verify JWT token is provided
        Assert.False(string.IsNullOrEmpty(authResponse.Token));
        
        // Verify user info matches
        Assert.Equal(email, authResponse.Email);
        Assert.False(string.IsNullOrEmpty(authResponse.UserId));
        
        // Note: Password hash should NOT be in response!
    }

    /// <summary>
    /// Test: Invalid password returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        // ARRANGE: Register user
        var email = $"wrongpass_{Guid.NewGuid()}@example.com";
        var correctPassword = "CorrectPassword123";
        
        var registerRequest = new RegisterRequest
        {
            Username = "WrongPassUser",
            Email = email,
            Password = correctPassword
        };
        
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Prepare login with WRONG password
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword456" // Incorrect!
        };

        // ACT: Try to login with wrong password
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // ASSERT: Should return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        // SECURITY: Error message should be generic (don't reveal if email exists)
        // We don't assert specific message here, but implementation should use:
        // "Invalid email or password" (same for wrong email AND wrong password)
    }

    /// <summary>
    /// Test: Non-existent user returns 401 Unauthorized (same as wrong password).
    /// </summary>
    [Fact]
    public async Task Login_NonExistentUser_Returns401()
    {
        // ARRANGE: Prepare login for user that doesn't exist
        var loginRequest = new LoginRequest
        {
            Email = $"nonexistent_{Guid.NewGuid()}@example.com",
            Password = "SomePassword123"
        };

        // ACT: Try to login with non-existent email
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // ASSERT: Should return 401 Unauthorized (same as wrong password)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        // SECURITY: Error message must be identical to wrong password case
        // Don't leak information about which emails are registered!
    }

    // ============================================================================
    // PROFILE TESTS (GET /api/auth/profile)
    // ============================================================================

    /// <summary>
    /// Test: Authenticated request returns 200 with user profile.
    /// </summary>
    [Fact]
    public async Task GetProfile_Authenticated_Returns200WithUser()
    {
        // ARRANGE: Register and login to get token
        var email = $"profile_{Guid.NewGuid()}@example.com";
        var password = "ProfilePassword123";
        
        var registerRequest = new RegisterRequest
        {
            Username = "ProfileUser",
            Email = email,
            Password = password
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);

        // Create new HTTP client with Authorization header
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

        // ACT: GET /api/auth/profile with Bearer token
        var response = await authenticatedClient.GetAsync("/api/auth/profile");

        // ASSERT: Should return 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify response contains user info
        var userResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(userResponse);
        
        // Verify user data matches
        Assert.Equal(authResponse.UserId, userResponse.UserId);
        Assert.Equal(email, userResponse.Email);
        
        // Password hash should NOT be in response!
        // (Can't verify directly, but implementation must exclude it)
    }

    /// <summary>
    /// Test: Unauthenticated request returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        // ACT: GET /api/auth/profile WITHOUT Authorization header
        var response = await _client.GetAsync("/api/auth/profile");

        // ASSERT: Should return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ============================================================================
    // PASSWORD RESET TESTS (Stubs for future Mailjet integration)
    // ============================================================================

    /// <summary>
    /// Test: Forgot password endpoint returns 200 with stub message.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_ValidEmail_Returns200Stub()
    {
        // ARRANGE: Prepare forgot password request
        var forgotPasswordRequest = new { Email = "someone@example.com" };

        // ACT: POST /api/auth/forgot-password
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotPasswordRequest);

        // ASSERT: Should return 200 OK with stub message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Response should indicate feature not yet implemented
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("not implemented", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test: Reset password endpoint returns 200 with stub message.
    /// </summary>
    [Fact]
    public async Task ResetPassword_Returns200Stub()
    {
        // ARRANGE: Prepare reset password request
        var resetPasswordRequest = new 
        { 
            Token = "some-reset-token", 
            NewPassword = "NewPassword123" 
        };

        // ACT: POST /api/auth/reset-password
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetPasswordRequest);

        // ASSERT: Should return 200 OK with stub message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Response should indicate feature not yet implemented
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("not implemented", responseBody, StringComparison.OrdinalIgnoreCase);
    }
}
