namespace Hamco.Core.Services;

/// <summary>
/// Interface for password hashing operations.
/// Defines the contract for securely hashing and verifying passwords.
/// </summary>
/// <remarks>
/// Why use an interface?
/// 
/// 1. **Testability:** Can mock this interface in unit tests
///    (test without real BCrypt hashing, which is slow)
/// 
/// 2. **Flexibility:** Can swap implementations without changing code
///    (BCrypt → Argon2, scrypt, PBKDF2, etc.)
/// 
/// 3. **Dependency Injection:** ASP.NET Core can inject this
///    (controllers depend on interface, not concrete class)
/// 
/// 4. **Separation of Concerns:** Business logic doesn't care HOW
///    passwords are hashed, just that they ARE hashed
/// 
/// Interface naming convention in C#:
///   - Starts with 'I' (IPasswordHasher, ILogger, IRepository)
///   - Followed by descriptive name (what it does)
/// 
/// The 'I' prefix is a C# convention inherited from COM/Windows programming.
/// It's optional but widely adopted in .NET ecosystem.
/// 
/// This is the Dependency Inversion Principle (SOLID):
///   "Depend on abstractions, not concretions"
///   High-level code (AuthController) depends on interface (IPasswordHasher)
///   Low-level code (PasswordHasher) implements the interface
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plain text password using a secure one-way hashing algorithm.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>A secure hash string that can be stored in the database.</returns>
    /// <remarks>
    /// Contract guarantees:
    ///   - Same password → different hash (due to random salt)
    ///   - Hash is irreversible (cannot get password back)
    ///   - Hash includes salt (no need to store salt separately)
    ///   - Hash is slow to compute (protects against brute force)
    /// 
    /// Example usage:
    ///   var hash = _passwordHasher.HashPassword("mySecretPassword");
    ///   user.PasswordHash = hash;  // Store in database
    /// 
    /// Security notes:
    ///   - NEVER log the input password!
    ///   - NEVER return or expose the plain password
    ///   - Hash should be at least 60 characters (BCrypt standard)
    /// </remarks>
    string HashPassword(string password);

    /// <summary>
    /// Verifies that a plain text password matches a stored hash.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="hash">The stored hash to verify against.</param>
    /// <returns>True if password matches hash, false otherwise.</returns>
    /// <remarks>
    /// Contract guarantees:
    ///   - Returns true ONLY if password matches the hash
    ///   - Timing-attack resistant (takes same time regardless of correctness)
    ///   - Extracts salt from hash automatically (salt embedded in hash)
    /// 
    /// Example usage in login:
    ///   var user = await GetUserByEmail(email);
    ///   if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
    ///       return Unauthorized("Invalid password");
    /// 
    /// Why we can verify without storing the original password:
    ///   1. Hash contains the salt used during hashing
    ///   2. VerifyPassword() extracts salt from hash
    ///   3. Hashes the input password with same salt
    ///   4. Compares the two hashes (constant-time comparison)
    /// 
    /// Security notes:
    ///   - NEVER expose why verification failed (timing attack)
    ///   - Return generic "Invalid email or password" message
    ///   - Log failed attempts for security monitoring
    /// </remarks>
    bool VerifyPassword(string password, string hash);
}
