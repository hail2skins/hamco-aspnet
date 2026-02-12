using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Hamco.Core.Tests.Services;

/// <summary>
/// Unit tests for ApiKeyService.
/// Tests API key generation, validation, hashing, expiry, and revocation.
/// </summary>
/// <remarks>
/// Test-Driven Development (TDD) Approach:
///   1. Write tests FIRST (before implementation)
///   2. Tests define expected behavior (specification)
///   3. Run tests (they fail - red)
///   4. Implement minimal code to pass tests (green)
///   5. Refactor while keeping tests green
/// 
/// Why TDD?
///   ✅ Tests document expected behavior
///   ✅ Forces thinking about API design upfront
///   ✅ Prevents over-engineering (only implement what's tested)
///   ✅ Regression safety (refactor with confidence)
///   ✅ Living documentation (tests show usage examples)
/// 
/// Test Naming Convention:
///   MethodName_Scenario_ExpectedBehavior
///   
///   Examples:
///     GenerateKeyAsync_ValidInput_ReturnsKeyAndEntity
///     ValidateKeyAsync_ExpiredKey_ReturnsNull
///     RevokeKeyAsync_ValidKeyId_SetsIsActiveFalse
/// 
/// Test Structure (AAA Pattern):
///   // Arrange: Setup test data, mocks, dependencies
///   // Act: Call the method being tested
///   // Assert: Verify expected outcomes
/// 
/// SQLite In-Memory Database:
///   Why SQLite for tests?
///     - Fast (no network, pure in-memory)
///     - Isolated (each test gets fresh database)
///     - No external dependencies (no PostgreSQL needed)
///     - Disposable (garbage collected after test)
///   
///   Setup:
///     var options = new DbContextOptionsBuilder&lt;HamcoDbContext&gt;()
///         .UseSqlite("DataSource=:memory:")
///         .Options;
///     var context = new HamcoDbContext(options);
///     await context.Database.OpenConnectionAsync(); // Keep connection alive
///     await context.Database.EnsureCreatedAsync();  // Create schema
/// 
/// xUnit Test Framework:
///   - [Fact]: Simple test (no parameters)
///   - [Theory]: Parameterized test (multiple inputs)
///   - Assert.Equal, Assert.NotNull, Assert.True, etc.
///   - async Task tests supported (await operations)
/// 
/// Test Coverage Goals:
///   ✅ Happy path (valid inputs, expected outputs)
///   ✅ Edge cases (empty strings, null, boundary values)
///   ✅ Error cases (invalid input, exceptions)
///   ✅ Security (hashing, expiry, revocation)
///   ✅ Integration (database persistence)
/// </remarks>
public class ApiKeyServiceTests : IDisposable
{
    private readonly HamcoDbContext _context;
    private readonly IApiKeyService _service;

    /// <summary>
    /// Test fixture setup - runs before each test.
    /// Creates in-memory SQLite database and ApiKeyService.
    /// </summary>
    /// <remarks>
    /// xUnit Test Lifecycle:
    ///   1. Create new test class instance
    ///   2. Run constructor (this method)
    ///   3. Run one test method
    ///   4. Call Dispose()
    ///   5. Repeat for each test method
    /// 
    /// Why new instance per test?
    ///   - Isolation: Tests don't affect each other
    ///   - Parallelization: Tests can run concurrently
    ///   - Simplicity: No cleanup between tests needed
    /// 
    /// SQLite In-Memory Setup:
    ///   Must keep connection open for duration of test!
    ///   If connection closes, in-memory database destroyed.
    ///   That's why we store _context (keeps connection alive).
    /// 
    /// Why async constructor not allowed?
    ///   C# doesn't support async constructors.
    ///   Solution: Use synchronous .Result or create helper method.
    ///   For tests, .Result is acceptable (not production code!).
    /// </remarks>
    public ApiKeyServiceTests()
    {
        // Setup in-memory SQLite database
        var options = new DbContextOptionsBuilder<HamcoDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new HamcoDbContext(options);
        
        // Open connection and create schema
        // .Result is OK in test constructors (not production!)
        _context.Database.OpenConnectionAsync().Wait();
        _context.Database.EnsureCreatedAsync().Wait();

        // Create service instance (will be implemented in Hamco.Services)
        // This line will fail until we implement ApiKeyService!
        // That's TDD - write tests first, implement after.
        _service = new Hamco.Services.ApiKeyService(_context, new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// Test cleanup - runs after each test.
    /// Disposes database connection and context.
    /// </summary>
    /// <remarks>
    /// IDisposable pattern:
    ///   xUnit calls Dispose() after each test.
    ///   Clean up resources (database connection, file handles, etc.).
    /// 
    /// Why dispose?
    ///   - Free resources (memory, connections)
    ///   - Prevent resource leaks
    ///   - Good practice (even for tests)
    /// 
    /// SQLite in-memory:
    ///   When connection disposed, in-memory database destroyed.
    ///   Next test gets fresh database (isolation).
    /// </remarks>
    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region GenerateKeyAsync Tests

    /// <summary>
    /// Test: GenerateKeyAsync with valid input returns key and entity.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_ValidInput_ReturnsKeyAndEntity()
    {
        // Arrange
        var name = "Test Bot";
        var isAdmin = true;
        var createdByUserId = Guid.NewGuid().ToString();

        // Act
        var (key, entity) = await _service.GenerateKeyAsync(name, isAdmin, createdByUserId);

        // Assert
        Assert.NotNull(key);
        Assert.NotEmpty(key);
        Assert.StartsWith("hamco_sk_", key);
        Assert.True(key.Length >= 40); // hamco_sk_ (9) + 32 random chars minimum

        Assert.NotNull(entity);
        Assert.Equal(name, entity.Name);
        Assert.Equal(isAdmin, entity.IsAdmin);
        Assert.Equal(createdByUserId, entity.CreatedByUserId);
        Assert.True(entity.IsActive);
        Assert.NotEmpty(entity.Id);
        Assert.NotEmpty(entity.KeyHash);
        Assert.NotEmpty(entity.KeyPrefix);
        Assert.Equal(key[..8], entity.KeyPrefix); // First 8 chars of key
    }

    /// <summary>
    /// Test: GenerateKeyAsync stores hashed key, not plaintext.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_StoresHashedKey_NotPlaintext()
    {
        // Arrange
        var name = "Security Test";
        var createdByUserId = Guid.NewGuid().ToString();

        // Act
        var (key, entity) = await _service.GenerateKeyAsync(name, false, createdByUserId);

        // Assert
        Assert.NotEqual(key, entity.KeyHash); // Hash != plaintext
        Assert.DoesNotContain(key, entity.KeyHash); // Hash doesn't contain plaintext
        
        // BCrypt hashes start with $2a$, $2b$, or $2y$
        Assert.Matches(@"^\$2[ayb]\$", entity.KeyHash);
    }

    /// <summary>
    /// Test: GenerateKeyAsync persists key to database.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_PersistsToDatabase()
    {
        // Arrange
        var name = "Persistence Test";
        var createdByUserId = Guid.NewGuid().ToString();

        // Act
        var (key, entity) = await _service.GenerateKeyAsync(name, true, createdByUserId);

        // Assert - Query database directly
        var savedKey = await _context.Set<ApiKey>().FindAsync(entity.Id);
        Assert.NotNull(savedKey);
        Assert.Equal(name, savedKey.Name);
        Assert.Equal(entity.KeyHash, savedKey.KeyHash);
    }

    /// <summary>
    /// Test: GenerateKeyAsync creates unique keys each time.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_GeneratesUniqueKeys()
    {
        // Arrange
        var name = "Uniqueness Test";
        var createdByUserId = Guid.NewGuid().ToString();

        // Act
        var (key1, entity1) = await _service.GenerateKeyAsync(name, false, createdByUserId);
        var (key2, entity2) = await _service.GenerateKeyAsync(name, false, createdByUserId);

        // Assert
        Assert.NotEqual(key1, key2);
        Assert.NotEqual(entity1.Id, entity2.Id);
        Assert.NotEqual(entity1.KeyHash, entity2.KeyHash);
    }

    /// <summary>
    /// Test: GenerateKeyAsync sets CreatedAt to current UTC time.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_SetsCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        var createdByUserId = Guid.NewGuid().ToString();

        // Act
        var (key, entity) = await _service.GenerateKeyAsync("Time Test", false, createdByUserId);
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.InRange(entity.CreatedAt, before, after);
    }

    /// <summary>
    /// Test: GenerateKeyAsync with Admin role creates admin key.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_WithAdminTrue_CreatesAdminKey()
    {
        // Arrange & Act
        var (key, entity) = await _service.GenerateKeyAsync("Admin Key", true, Guid.NewGuid().ToString());

        // Assert
        Assert.True(entity.IsAdmin);
    }

    /// <summary>
    /// Test: GenerateKeyAsync with non-admin role creates user key.
    /// </summary>
    [Fact]
    public async Task GenerateKeyAsync_WithAdminFalse_CreatesUserKey()
    {
        // Arrange & Act
        var (key, entity) = await _service.GenerateKeyAsync("User Key", false, Guid.NewGuid().ToString());

        // Assert
        Assert.False(entity.IsAdmin);
    }

    #endregion

    #region ValidateKeyAsync Tests

    /// <summary>
    /// Test: ValidateKeyAsync with valid key returns ClaimsPrincipal.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_ValidKey_ReturnsClaimsPrincipal()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Valid Key", true, Guid.NewGuid().ToString());

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("ApiKey", principal.Identity?.AuthenticationType);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with valid admin key includes Admin role.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_ValidAdminKey_IncludesAdminRole()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Admin Key", true, Guid.NewGuid().ToString());

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.IsInRole("Admin"));
        
        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal("Admin", roleClaim.Value);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with valid user key includes User role.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_ValidUserKey_IncludesUserRole()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("User Key", false, Guid.NewGuid().ToString());

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
        Assert.False(principal.IsInRole("Admin"));
        Assert.True(principal.IsInRole("User"));
        
        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal("User", roleClaim.Value);
    }

    /// <summary>
    /// Test: ValidateKeyAsync includes expected claims.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_ValidKey_IncludesExpectedClaims()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Claims Test", true, Guid.NewGuid().ToString());

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
        
        var nameIdentifier = principal.FindFirst(ClaimTypes.NameIdentifier);
        Assert.NotNull(nameIdentifier);
        Assert.Equal(entity.Id, nameIdentifier.Value);
        
        var email = principal.FindFirst(ClaimTypes.Email);
        Assert.NotNull(email);
        Assert.Equal($"apikey:{entity.Name}", email.Value);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with invalid key returns null.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_InvalidKey_ReturnsNull()
    {
        // Arrange
        var invalidKey = "hamco_sk_invalidkeyinvalidkeyinvalidkey";

        // Act
        var principal = await _service.ValidateKeyAsync(invalidKey);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with wrong format returns null.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_WrongFormat_ReturnsNull()
    {
        // Arrange
        var wrongFormat = "invalid_format_key";

        // Act
        var principal = await _service.ValidateKeyAsync(wrongFormat);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with empty string returns null.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_EmptyString_ReturnsNull()
    {
        // Arrange
        var emptyKey = "";

        // Act
        var principal = await _service.ValidateKeyAsync(emptyKey);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with revoked key returns null.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_RevokedKey_ReturnsNull()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Revoked Key", true, Guid.NewGuid().ToString());
        await _service.RevokeKeyAsync(entity.Id);

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with expired key returns null.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_ExpiredKey_ReturnsNull()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Expired Key", true, Guid.NewGuid().ToString());
        
        // Manually set expiry to past
        entity.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _context.Update(entity);
        await _context.SaveChangesAsync();

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with future expiry returns principal.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_FutureExpiry_ReturnsPrincipal()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Future Expiry", true, Guid.NewGuid().ToString());
        
        // Set expiry to future
        entity.ExpiresAt = DateTime.UtcNow.AddDays(7);
        _context.Update(entity);
        await _context.SaveChangesAsync();

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
    }

    /// <summary>
    /// Test: ValidateKeyAsync with null expiry (never expires) returns principal.
    /// </summary>
    [Fact]
    public async Task ValidateKeyAsync_NullExpiry_ReturnsPrincipal()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("No Expiry", false, Guid.NewGuid().ToString());
        
        // Ensure ExpiresAt is null
        Assert.Null(entity.ExpiresAt);

        // Act
        var principal = await _service.ValidateKeyAsync(key);

        // Assert
        Assert.NotNull(principal);
    }

    #endregion

    #region RevokeKeyAsync Tests

    /// <summary>
    /// Test: RevokeKeyAsync sets IsActive to false.
    /// </summary>
    [Fact]
    public async Task RevokeKeyAsync_ValidKeyId_SetsIsActiveFalse()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("To Revoke", true, Guid.NewGuid().ToString());
        Assert.True(entity.IsActive); // Sanity check

        // Act
        await _service.RevokeKeyAsync(entity.Id);

        // Assert
        var revokedKey = await _context.Set<ApiKey>().FindAsync(entity.Id);
        Assert.NotNull(revokedKey);
        Assert.False(revokedKey.IsActive);
    }

    /// <summary>
    /// Test: RevokeKeyAsync is idempotent (can revoke twice without error).
    /// </summary>
    [Fact]
    public async Task RevokeKeyAsync_AlreadyRevoked_Succeeds()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Double Revoke", false, Guid.NewGuid().ToString());
        await _service.RevokeKeyAsync(entity.Id);

        // Act - revoke again
        await _service.RevokeKeyAsync(entity.Id);

        // Assert - still revoked, no exception
        var revokedKey = await _context.Set<ApiKey>().FindAsync(entity.Id);
        Assert.NotNull(revokedKey);
        Assert.False(revokedKey.IsActive);
    }

    /// <summary>
    /// Test: RevokeKeyAsync persists changes to database.
    /// </summary>
    [Fact]
    public async Task RevokeKeyAsync_PersistsToDatabase()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Persist Revoke", true, Guid.NewGuid().ToString());

        // Act
        await _service.RevokeKeyAsync(entity.Id);

        // Assert - Reload entity from same context to verify persistence
        _context.Entry(entity).Reload();
        Assert.False(entity.IsActive);
    }

    /// <summary>
    /// Test: RevokeKeyAsync with non-existent key throws exception.
    /// </summary>
    [Fact]
    public async Task RevokeKeyAsync_NonExistentKey_ThrowsException()
    {
        // Arrange
        var fakeId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.RevokeKeyAsync(fakeId));
    }

    #endregion

    #region Security Tests

    /// <summary>
    /// Test: BCrypt hash verification works correctly.
    /// </summary>
    [Fact]
    public async Task Security_BCryptHashVerification_WorksCorrectly()
    {
        // Arrange
        var (key, entity) = await _service.GenerateKeyAsync("Hash Test", true, Guid.NewGuid().ToString());

        // Act - Verify using BCrypt directly
        var isValid = BCrypt.Net.BCrypt.Verify(key, entity.KeyHash);

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Test: Different keys produce different hashes.
    /// </summary>
    [Fact]
    public async Task Security_DifferentKeys_ProduceDifferentHashes()
    {
        // Arrange & Act
        var (key1, entity1) = await _service.GenerateKeyAsync("Key 1", false, Guid.NewGuid().ToString());
        var (key2, entity2) = await _service.GenerateKeyAsync("Key 2", false, Guid.NewGuid().ToString());

        // Assert
        Assert.NotEqual(entity1.KeyHash, entity2.KeyHash);
    }

    /// <summary>
    /// Test: Key prefix matches first 8 characters of plaintext key.
    /// </summary>
    [Fact]
    public async Task Security_KeyPrefix_MatchesFirstEightCharacters()
    {
        // Arrange & Act
        var (key, entity) = await _service.GenerateKeyAsync("Prefix Test", true, Guid.NewGuid().ToString());

        // Assert
        Assert.Equal(key[..8], entity.KeyPrefix);
        Assert.Equal(8, entity.KeyPrefix.Length);
    }

    #endregion
}
