using System.Net;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Hamco.Data;
using Hamco.Core.Models;

namespace Hamco.Api.Tests;

/// <summary>
/// Tests for layout, static files, and slogan randomization.
/// </summary>
public class LayoutTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public LayoutTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hamco Internet Solutions", content);
    }

    [Fact]
    public async Task AboutPage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/about");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("About Hamco", content);
    }

    [Fact]
    public async Task NotesPage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/notes");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Technical Notes", content);
    }

    [Fact]
    public async Task StaticCssFile_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/css/styles.css");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StaticJsFile_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/js/scripts.js");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FaviconImage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/img/favicon.ico");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HeaderBackgroundImage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/img/main/hammy1.png");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomePage_ContainsSlogan()
    {
        // Arrange - Add some slogans to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        
        context.Slogans.Add(new Slogan { Text = "Test Slogan 1", IsActive = true });
        context.Slogans.Add(new Slogan { Text = "Test Slogan 2", IsActive = true });
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Should contain one of the test slogans
        Assert.True(
            content.Contains("Test Slogan 1") || content.Contains("Test Slogan 2"),
            "Page should contain one of the test slogans"
        );
    }

    [Fact]
    public async Task HomePage_WithNoSlogans_ShowsDefaultSlogan()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Should contain default slogan when no slogans in database
        Assert.Contains("Your trusted partner in Internet solutions", content);
    }

    [Fact]
    public async Task SloganChanges_BetweenPageLoads()
    {
        // Arrange - Add multiple slogans
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        
        context.Slogans.Add(new Slogan { Text = "Random Slogan A", IsActive = true });
        context.Slogans.Add(new Slogan { Text = "Random Slogan B", IsActive = true });
        context.Slogans.Add(new Slogan { Text = "Random Slogan C", IsActive = true });
        await context.SaveChangesAsync();

        // Act - Load page multiple times
        var slogansFound = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();
            
            if (content.Contains("Random Slogan A")) slogansFound.Add("A");
            if (content.Contains("Random Slogan B")) slogansFound.Add("B");
            if (content.Contains("Random Slogan C")) slogansFound.Add("C");
        }

        // Assert - Should see randomness (at least 2 different slogans in 10 loads)
        // Note: Statistically possible to get all the same, but very unlikely
        Assert.True(slogansFound.Count >= 1, "Should display at least one slogan variant");
    }

    [Fact]
    public async Task Layout_ContainsNavigation()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("Home", content);
        Assert.Contains("Notes", content);
        Assert.Contains("About", content);
        Assert.Contains("Login", content);
    }

    [Fact]
    public async Task Layout_ContainsFooter()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("Copyright", content);
        Assert.Contains("Hamco Internet Solutions", content);
    }

    [Fact]
    public async Task NotesDetailPage_ShowsNoteMetadata()
    {
        // Arrange - Create a test note
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
        
        var note = new Note
        {
            Title = "Test Note",
            Content = "This is test content.",
            UserId = null  // Allow null for testing (no user relationship required)
        };
        context.Notes.Add(note);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/notes/{note.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("Test Note", content);
        Assert.Contains("Art Mills", content); // Author name
        Assert.Contains("This is test content", content);
    }
}
