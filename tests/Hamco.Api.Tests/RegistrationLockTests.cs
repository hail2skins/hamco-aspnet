using System.Net;
using System.Net.Http.Json;
using Hamco.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hamco.Api.Tests;

/// <summary>
/// Custom factory for tests that need environment configuration.
/// </summary>
internal class ConfigurableTestFactory : TestWebApplicationFactory
{
    private readonly Action<IDictionary<string, string?>> _configure;

    public ConfigurableTestFactory(Action<IDictionary<string, string?>> configure)
    {
        _configure = configure;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        var envConfig = new Dictionary<string, string?>();
        _configure(envConfig);
        
        foreach (var kvp in envConfig)
        {
            builder.UseSetting(kvp.Key, kvp.Value);
        }
    }
}

/// <summary>
/// Integration tests for registration lock functionality.
/// Tests ALLOW_REGISTRATION environment variable enforcement.
/// </summary>
/// <remarks>
/// Test coverage:
/// - Registration blocked when ALLOW_REGISTRATION=false (default)
/// - Registration allowed when ALLOW_REGISTRATION=true
/// - API returns 403 Forbidden with appropriate message when blocked
/// </remarks>
public class RegistrationLockTests : IDisposable
{
    private TestWebApplicationFactory? _factory;
    private HttpClient? _client;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Test: Registration is blocked by default (ALLOW_REGISTRATION not set or false).
    /// </summary>
    [Fact]
    public async Task Register_WhenRegistrationBlocked_Returns403()
    {
        // ARRANGE: Create factory with ALLOW_REGISTRATION=false (default)
        _factory = new ConfigurableTestFactory(config =>
        {
            config["ALLOW_REGISTRATION"] = "false";
        });
        _client = _factory.CreateClient();

        var registerRequest = new RegisterRequest
        {
            Username = "BlockedUser",
            Email = $"blocked_{Guid.NewGuid()}@example.com",
            Password = "Password123"
        };

        // ACT: Attempt to register when registration is disabled
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ASSERT: Should return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify error message indicates registration is disabled
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Registration is currently disabled", errorResponse);
    }

    /// <summary>
    /// Test: Registration is allowed when ALLOW_REGISTRATION=true.
    /// </summary>
    [Fact]
    public async Task Register_WhenRegistrationAllowed_Returns201()
    {
        // ARRANGE: Create factory with ALLOW_REGISTRATION=true
        _factory = new ConfigurableTestFactory(config =>
        {
            config["ALLOW_REGISTRATION"] = "true";
        });
        _client = _factory.CreateClient();

        var registerRequest = new RegisterRequest
        {
            Username = "AllowedUser",
            Email = $"allowed_{Guid.NewGuid()}@example.com",
            Password = "Password123"
        };

        // ACT: Attempt to register when registration is enabled
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ASSERT: Should return 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify auth response contains token
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrEmpty(authResponse.Token));
    }

    /// <summary>
    /// Test: Registration blocked by default when ALLOW_REGISTRATION not set.
    /// </summary>
    [Fact]
    public async Task Register_WhenEnvVarNotSet_Returns403()
    {
        // ARRANGE: Create factory without setting ALLOW_REGISTRATION (should default to blocked)
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        var registerRequest = new RegisterRequest
        {
            Username = "DefaultBlockedUser",
            Email = $"default_{Guid.NewGuid()}@example.com",
            Password = "Password123"
        };

        // ACT: Attempt to register with default configuration
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // ASSERT: Should return 403 Forbidden (default is blocked)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
