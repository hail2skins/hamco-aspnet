using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hamco.Api.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_NewEmail_CreatesUnverifiedUserAndSendsVerificationEmail()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"new_{Guid.NewGuid():N}@example.com";
        var response = await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "newuser",
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        Assert.False(user.IsEmailVerified);
        Assert.False(string.IsNullOrWhiteSpace(user.EmailVerificationTokenHash));
        Assert.NotNull(user.EmailVerificationTokenExpiresAt);

        var sent = Assert.Single(spy.VerificationEmails);
        Assert.Equal(email, sent.ToEmail);
        Assert.Contains("/auth/verify-email?token=", sent.Link);
    }

    [Fact]
    public async Task Register_ExistingVerifiedEmail_Returns409()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"verified_{Guid.NewGuid():N}@example.com";
        await RegisterAndMarkVerifiedAsync(ctx, email);

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "otheruser",
            Email = email,
            Password = "AnotherPassword123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ExistingUnverifiedEmail_ResendsTokenWithoutDuplicateUser()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"unverified_{Guid.NewGuid():N}@example.com";

        await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "user1",
            Email = email,
            Password = "Password123!"
        });
        var originalHash = (await ctx.Db.Users.SingleAsync(u => u.Email == email)).EmailVerificationTokenHash;

        var second = await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "user2",
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        ctx.Db.ChangeTracker.Clear();
        var users = await ctx.Db.Users.AsNoTracking().Where(u => u.Email == email).ToListAsync();
        var user = Assert.Single(users);

        Assert.NotEqual(originalHash, user.EmailVerificationTokenHash);
        Assert.Equal(2, spy.VerificationEmails.Count);
    }

    [Fact]
    public async Task VerifyEmail_ValidToken_MarksUserVerifiedAndRedirects()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy, allowAutoRedirect: false);

        var email = $"verify_{Guid.NewGuid():N}@example.com";
        await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "verify-user",
            Email = email,
            Password = "Password123!"
        });

        var token = ExtractTokenFromLink(spy.VerificationEmails.Last().Link);
        var response = await ctx.Client.GetAsync($"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/login?verified=1", response.Headers.Location?.ToString());

        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        Assert.True(user.IsEmailVerified);
        Assert.Null(user.EmailVerificationTokenHash);
        Assert.Null(user.EmailVerificationTokenExpiresAt);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_FailsAndRedirects()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy, allowAutoRedirect: false);

        var email = $"expiredverify_{Guid.NewGuid():N}@example.com";
        await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "expired-user",
            Email = email,
            Password = "Password123!"
        });

        var token = ExtractTokenFromLink(spy.VerificationEmails.Last().Link);
        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await ctx.Db.SaveChangesAsync();

        var response = await ctx.Client.GetAsync($"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/login?error=expired_or_invalid_verification_token", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_UnverifiedUser_Returns403()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"loginunverified_{Guid.NewGuid():N}@example.com";
        await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "login-unverified",
            Email = email,
            Password = "Password123!"
        });

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_VerifiedUser_Returns200()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"loginverified_{Guid.NewGuid():N}@example.com";
        await RegisterAndMarkVerifiedAsync(ctx, email);

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ExistingEmail_GeneratesTokenAndSendsEmail()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"forgot_{Guid.NewGuid():N}@example.com";
        await RegisterAndMarkVerifiedAsync(ctx, email);

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        ctx.Db.ChangeTracker.Clear();
        var user = await ctx.Db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordResetTokenHash));
        Assert.NotNull(user.PasswordResetTokenExpiresAt);

        Assert.Single(spy.PasswordResetEmails);
        Assert.Contains("/auth/reset-password?token=", spy.PasswordResetEmails.Last().Link);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsGenericSuccessWithoutEnumeration()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = $"missing_{Guid.NewGuid():N}@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("If your email is registered", await response.Content.ReadAsStringAsync());
        Assert.Empty(spy.PasswordResetEmails);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesPassword()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"resetok_{Guid.NewGuid():N}@example.com";
        await RegisterAndMarkVerifiedAsync(ctx, email);

        await ctx.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        var token = ExtractTokenFromLink(spy.PasswordResetEmails.Last().Link);

        var resetResponse = await ctx.Client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = token,
            NewPassword = "BrandNewPassword123!"
        });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var oldLogin = await ctx.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Password123!" });
        var newLogin = await ctx.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "BrandNewPassword123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_Returns400()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"resetexpired_{Guid.NewGuid():N}@example.com";
        await RegisterAndMarkVerifiedAsync(ctx, email);

        await ctx.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        var token = ExtractTokenFromLink(spy.PasswordResetEmails.Last().Link);

        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await ctx.Db.SaveChangesAsync();

        var response = await ctx.Client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = token,
            NewPassword = "BrandNewPassword123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task RegisterAndMarkVerifiedAsync(TestContext ctx, string email)
    {
        var response = await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "verified-user",
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        user.IsEmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;
        await ctx.Db.SaveChangesAsync();
    }

    private static TestContext CreateContext(SpyTransactionalEmailService emailSpy, bool allowAutoRedirect = true)
    {
        var baseFactory = new TestWebApplicationFactory();
        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ALLOW_REGISTRATION"] = "true",
                    ["APP_BASE_URL"] = "https://hamco.test"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(s => s.ServiceType == typeof(ITransactionalEmailService)).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<ITransactionalEmailService>(emailSpy);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        return new TestContext(baseFactory, factory, scope, db, client);
    }

    public static string ExtractTokenFromLink(string link)
    {
        var uri = new Uri(link);
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var tokenPart = query.Single(p => p.StartsWith("token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(tokenPart.Substring("token=".Length));
    }

    private sealed class TestContext : IDisposable
    {
        private readonly TestWebApplicationFactory _baseFactory;
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public HamcoDbContext Db { get; }
        public HttpClient Client { get; }

        public TestContext(
            TestWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            IServiceScope scope,
            HamcoDbContext db,
            HttpClient client)
        {
            _baseFactory = baseFactory;
            _factory = factory;
            _scope = scope;
            Db = db;
            Client = client;
        }

        public void Dispose()
        {
            Client.Dispose();
            Db.Dispose();
            _scope.Dispose();
            _factory.Dispose();
            _baseFactory.Dispose();
        }
    }
}

public class AuthTokenGenerationTests
{
    [Fact]
    public async Task VerificationTokens_AreHighEntropyAndUnique()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var tokens = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 5; i++)
        {
            var response = await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                Username = $"u{i}",
                Email = $"entropy_{i}_{Guid.NewGuid():N}@example.com",
                Password = "Password123!"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var token = AuthControllerTests.ExtractTokenFromLink(spy.VerificationEmails.Last().Link);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
            Assert.True(token.Length >= 43);
            Assert.True(tokens.Add(token));
        }
    }

    [Fact]
    public async Task VerificationAndResetTokens_AreStoredAsSha256HashNotPlaintext()
    {
        var spy = new SpyTransactionalEmailService();
        using var ctx = CreateContext(spy);

        var email = $"hashing_{Guid.NewGuid():N}@example.com";
        await ctx.Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "hashing-user",
            Email = email,
            Password = "Password123!"
        });

        var verificationToken = AuthControllerTests.ExtractTokenFromLink(spy.VerificationEmails.Last().Link);
        var user = await ctx.Db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(Sha256Hex(verificationToken), user.EmailVerificationTokenHash);
        Assert.NotEqual(verificationToken, user.EmailVerificationTokenHash);

        user.IsEmailVerified = true;
        await ctx.Db.SaveChangesAsync();

        await ctx.Client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        var resetToken = AuthControllerTests.ExtractTokenFromLink(spy.PasswordResetEmails.Last().Link);

        ctx.Db.ChangeTracker.Clear();
        user = await ctx.Db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Equal(Sha256Hex(resetToken), user.PasswordResetTokenHash);
        Assert.NotEqual(resetToken, user.PasswordResetTokenHash);
    }

    private static TestContext CreateContext(SpyTransactionalEmailService emailSpy)
    {
        var baseFactory = new TestWebApplicationFactory();
        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ALLOW_REGISTRATION"] = "true",
                    ["APP_BASE_URL"] = "https://hamco.test"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(s => s.ServiceType == typeof(ITransactionalEmailService)).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<ITransactionalEmailService>(emailSpy);
            });
        });

        var client = factory.CreateClient();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();

        return new TestContext(baseFactory, factory, scope, db, client);
    }

    private static string Sha256Hex(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly TestWebApplicationFactory _baseFactory;
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public HamcoDbContext Db { get; }
        public HttpClient Client { get; }

        public TestContext(
            TestWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            IServiceScope scope,
            HamcoDbContext db,
            HttpClient client)
        {
            _baseFactory = baseFactory;
            _factory = factory;
            _scope = scope;
            Db = db;
            Client = client;
        }

        public void Dispose()
        {
            Client.Dispose();
            Db.Dispose();
            _scope.Dispose();
            _factory.Dispose();
            _baseFactory.Dispose();
        }
    }
}

public sealed class SpyTransactionalEmailService : ITransactionalEmailService
{
    public List<(string ToEmail, string Link)> VerificationEmails { get; } = new();
    public List<(string ToEmail, string Link)> PasswordResetEmails { get; } = new();

    public Task SendVerificationEmailAsync(string toEmail, string verificationLink)
    {
        VerificationEmails.Add((toEmail, verificationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        PasswordResetEmails.Add((toEmail, resetLink));
        return Task.CompletedTask;
    }
}
