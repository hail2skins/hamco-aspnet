namespace Hamco.Core.Services;

/// <summary>
/// BCrypt implementation of password hashing for secure credential storage.
/// Uses the BCrypt.Net library for industry-standard password hashing.
/// </summary>
/// <remarks>
/// BCrypt overview:
/// 
/// What is BCrypt?
///   - Password hashing algorithm designed in 1999
///   - Based on Blowfish cipher
///   - Slow by design (protects against brute force)
///   - Includes automatic salt generation
///   - Adaptive (work factor can increase over time)
/// 
/// Why BCrypt?
///   ✅ Battle-tested (25+ years in production use)
///   ✅ Slow hashing prevents brute force attacks
///   ✅ Automatic salt management (no separate storage)
///   ✅ Configurable work factor (adapts to faster hardware)
///   ✅ Recommended by OWASP (security best practices org)
/// 
/// Alternatives:
///   - Argon2: Winner of Password Hashing Competition 2015 (more modern)
///   - scrypt: Good for memory-hard hashing
///   - PBKDF2: Older, still acceptable but less resistant
/// 
/// BCrypt is the "safe default" choice for most applications.
/// 
/// Work factor explained:
///   - Work factor = number of hashing rounds as power of 2
///   - Work factor 12 = 2^12 = 4,096 rounds (~300ms)
///   - Each +1 doubles the time (13 = ~600ms, 14 = ~1200ms)
///   - Balance: Slow enough to prevent attacks, fast enough for users
/// 
/// This class implements IPasswordHasher interface:
///   - 'public class PasswordHasher : IPasswordHasher' means:
///     "PasswordHasher is a concrete implementation of IPasswordHasher"
///   - Must implement all interface methods (HashPassword, VerifyPassword)
///   - Can be injected wherever IPasswordHasher is needed
/// </remarks>
public class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Hashes a plain text password using BCrypt with work factor 12.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>BCrypt hash string (60 characters) including salt.</returns>
    /// <remarks>
    /// BCrypt.Net.BCrypt.HashPassword() does:
    /// 1. Generates random 128-bit salt
    /// 2. Combines password + salt
    /// 3. Applies Blowfish cipher 2^12 times (workFactor: 12)
    /// 4. Encodes result in BCrypt format
    /// 
    /// Hash format breakdown:
    ///   $2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW
    ///   \_/ \_/ \____________________/\_____________________________/
    ///    |   |           |                          |
    ///    |   |           |                          +- Hash (31 chars)
    ///    |   |           +- Salt (22 chars, base64)
    ///    |   +- Work factor (12 = 4096 rounds)
    ///    +- Algorithm version (2a = current BCrypt)
    /// 
    /// Work factor 12 timing (approximate):
    ///   - Intel i7: ~300ms per hash
    ///   - Server CPU: ~200ms per hash
    ///   - Mobile CPU: ~500ms per hash
    /// 
    /// This is INTENTIONALLY slow!
    ///   - User registration/login: 300ms is acceptable
    ///   - Attacker brute force: 300ms × 1 billion tries = 9.5 years
    /// 
    /// The 'workFactor: 12' parameter is a named argument in C#:
    ///   - Makes code more readable (explicit what 12 means)
    ///   - Optional parameter (BCrypt has default, we override it)
    ///   - Syntax: 'parameterName: value'
    /// 
    /// Security best practice: Use highest work factor that's acceptable
    /// for your user experience (12-14 is common, 10 is minimum).
    /// </remarks>
    public string HashPassword(string password)
    {
        // BCrypt.Net.BCrypt is a static class (like our SlugGenerator)
        // HashPassword() generates salt automatically
        // workFactor: 12 means 2^12 = 4,096 rounds of hashing
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Verifies that a plain text password matches a BCrypt hash.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="hash">The BCrypt hash to verify against.</param>
    /// <returns>True if password matches hash, false otherwise.</returns>
    /// <remarks>
    /// BCrypt.Net.BCrypt.Verify() process:
    /// 1. Extract salt from hash (first 29 characters)
    /// 2. Extract work factor from hash (characters 4-5)
    /// 3. Hash the input password with extracted salt + work factor
    /// 4. Compare computed hash with stored hash (constant-time comparison)
    /// 5. Return true if hashes match, false otherwise
    /// 
    /// Why this is secure:
    ///   ✅ Constant-time comparison (prevents timing attacks)
    ///   ✅ Salt prevents rainbow table attacks
    ///   ✅ Work factor makes brute force impractical
    ///   ✅ No need to store salt separately (embedded in hash)
    /// 
    /// Timing attack explained:
    ///   Bad: if (hash == computedHash) return true;
    ///        Stops comparing at first different character (variable time)
    ///        Attacker can measure time to guess correct characters
    /// 
    ///   Good: Constant-time comparison (always checks all characters)
    ///         Takes same time whether hashes match or not
    ///         BCrypt.Verify() uses constant-time comparison internally
    /// 
    /// Example scenario:
    ///   User types: "wrongPassword"
    ///   Stored hash: "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW"
    ///   
    ///   Process:
    ///   1. Extract salt: "R9h/cIPz0gi.URNNX3kh2O"
    ///   2. Hash "wrongPassword" with that salt
    ///   3. Result: "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0XXXXX"
    ///   4. Compare: XXXXX ≠ jWMUW
    ///   5. Return false
    /// 
    /// No exceptions thrown if hash is invalid:
    ///   - Returns false for malformed hashes
    ///   - Returns false for empty/null password
    ///   - Safe to use without try-catch
    /// </remarks>
    public bool VerifyPassword(string password, string hash)
    {
        // BCrypt.Net.BCrypt.Verify() handles all complexity internally
        // Just pass password + hash, get back true/false
        // Simple API, but extremely secure under the hood!
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
