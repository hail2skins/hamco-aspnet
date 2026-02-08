# API Key Permissions

## Overview

Hamco API supports two types of API keys with different permission levels:
- **Admin API Keys**: Full CRUD access (Create, Read, Update, Delete)
- **User (Non-Admin) API Keys**: Read-only access

This document outlines the permission matrix and testing procedures.

**📚 Quick Start:** See [main README](../README.md#-api-keys) for setup and basic usage examples.

## Permission Matrix

| Operation | Endpoint | Admin API Key | User API Key | No Auth |
|-----------|----------|--------------|--------------|---------|
| **List Notes** | `GET /api/notes` | ✅ 200 OK | ✅ 200 OK | ✅ 200 OK |
| **Get Note by ID** | `GET /api/notes/{id}` | ✅ 200 OK | ✅ 200 OK | ✅ 200 OK |
| **Create Note** | `POST /api/notes` | ✅ 201 Created | ❌ 403 Forbidden | ❌ 401 Unauthorized |
| **Update Note** | `PUT /api/notes/{id}` | ✅ 200 OK | ❌ 403 Forbidden | ❌ 401 Unauthorized |
| **Delete Note** | `DELETE /api/notes/{id}` | ✅ 204 No Content | ❌ 403 Forbidden | ❌ 401 Unauthorized |

**Legend:**
- ✅ = Operation allowed
- ❌ = Operation denied
- 200 OK = Success (read operation)
- 201 Created = Success (resource created)
- 204 No Content = Success (no content returned)
- 401 Unauthorized = Not authenticated
- 403 Forbidden = Authenticated but not authorized

## Design Philosophy

### Public Read Access
All read operations (GET) are publicly accessible. This allows:
- Anyone to browse blog content
- Search engines to index the site
- RSS readers to fetch updates
- No authentication needed for casual readers

### Protected Write Access
Write operations (POST, PUT, DELETE) require authentication and appropriate permissions:
- Only authenticated users can modify content
- Admin role required for all write operations
- Prevents anonymous spam and vandalism

### API Key Roles

**Admin API Keys:**
- Full CRUD permissions
- Can create, read, update, and delete notes
- Intended for: automation, bots, trusted integrations
- Example use cases:
  - Publishing posts from external systems
  - Automated content management
  - Admin tools and dashboards

**User (Non-Admin) API Keys:**
- Read-only access (same as public)
- Cannot create, update, or delete notes
- Intended for: monitoring, analytics, untrusted clients
- Example use cases:
  - Read-only monitoring tools
  - Public API consumers
  - Third-party integrations without write privileges

## Creating API Keys

### Prerequisites
- You must be logged in as an admin user
- Admin login credentials (JWT token or admin API key)

### Via API

**Login to get admin JWT token:**
```bash
curl -X POST http://localhost:5250/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"YourPassword"}'
```

**Create Admin API Key:**
```bash
curl -X POST http://localhost:5250/api/admin/api-keys \
  -H "Authorization: Bearer <admin-jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{"name":"My Admin Bot","isAdmin":true}'
```

**Create User (Read-Only) API Key:**
```bash
curl -X POST http://localhost:5250/api/admin/api-keys \
  -H "Authorization: Bearer <admin-jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Read Only Bot","isAdmin":false}'
```

**Response:**
```json
{
  "key": "hamco_sk_abc123...",
  "id": "uuid-here",
  "name": "My Admin Bot",
  "prefix": "hamco_sk",
  "isAdmin": true,
  "createdAt": "2026-02-08T18:00:00Z",
  "message": "Save this key securely. You won't see it again!"
}
```

⚠️ **Important:** Save the API key immediately. It cannot be retrieved later!

## Using API Keys

Include the API key in the `X-API-Key` header:

```bash
curl http://localhost:5250/api/notes \
  -H "X-API-Key: hamco_sk_abc123..."
```

## Testing Permissions

### Manual Testing

**1. Test Admin API Key (Full Access):**
```bash
# Set your admin API key
ADMIN_KEY="hamco_sk_admin_key_here"

# Read (should work)
curl -w "\nHTTP_CODE:%{http_code}" http://localhost:5250/api/notes \
  -H "X-API-Key: $ADMIN_KEY"

# Create (should work)
curl -w "\nHTTP_CODE:%{http_code}" -X POST http://localhost:5250/api/notes \
  -H "X-API-Key: $ADMIN_KEY" \
  -H "Content-Type: application/json" \
  -d '{"title":"Test","content":"Content"}'

# Update (should work - replace {id})
curl -w "\nHTTP_CODE:%{http_code}" -X PUT http://localhost:5250/api/notes/{id} \
  -H "X-API-Key: $ADMIN_KEY" \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated","content":"Updated content"}'

# Delete (should work - replace {id})
curl -w "\nHTTP_CODE:%{http_code}" -X DELETE http://localhost:5250/api/notes/{id} \
  -H "X-API-Key: $ADMIN_KEY"
```

**2. Test User API Key (Read-Only):**
```bash
# Set your user API key
USER_KEY="hamco_sk_user_key_here"

# Read (should work - 200 OK)
curl -w "\nHTTP_CODE:%{http_code}" http://localhost:5250/api/notes \
  -H "X-API-Key: $USER_KEY"

# Create (should fail - 403 Forbidden)
curl -w "\nHTTP_CODE:%{http_code}" -X POST http://localhost:5250/api/notes \
  -H "X-API-Key: $USER_KEY" \
  -H "Content-Type: application/json" \
  -d '{"title":"Test","content":"Content"}'

# Update (should fail - 403 Forbidden)
curl -w "\nHTTP_CODE:%{http_code}" -X PUT http://localhost:5250/api/notes/{id} \
  -H "X-API-Key: $USER_KEY" \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated","content":"Updated content"}'

# Delete (should fail - 403 Forbidden)
curl -w "\nHTTP_CODE:%{http_code}" -X DELETE http://localhost:5250/api/notes/{id} \
  -H "X-API-Key: $USER_KEY"
```

### Automated Integration Tests

Run the full test suite:
```bash
cd /path/to/hamco
dotnet test tests/Hamco.Api.Tests/Hamco.Api.Tests.csproj --filter "FullyQualifiedName~ApiKeyPermissionsTests"
```

**Test Coverage:**
- ✅ Admin API key can read notes
- ✅ Admin API key can create notes
- ✅ Admin API key can update notes
- ✅ Admin API key can delete notes
- ✅ User API key can read notes (list)
- ✅ User API key can read notes (single)
- ✅ User API key cannot create notes (403)
- ✅ User API key cannot update notes (403)
- ✅ User API key cannot delete notes (403)
- ✅ Invalid API key returns 401

## Security Best Practices

1. **Store API Keys Securely**
   - Never commit API keys to version control
   - Use environment variables or secret management systems
   - Rotate keys periodically

2. **Use the Principle of Least Privilege**
   - Create non-admin (user) API keys for read-only access
   - Only create admin API keys when write access is absolutely necessary

3. **Monitor API Key Usage**
   - Track API key activity in logs
   - Set up alerts for suspicious patterns
   - Revoke compromised keys immediately

4. **Key Rotation**
   - Rotate API keys regularly (e.g., every 90 days)
   - Have a process for emergency key rotation
   - Keep track of which keys are in use where

## API Key Format

Hamco API keys use the format: `hamco_sk_<64-character-hex-string>`

- **Prefix**: `hamco_sk_` (Hamco Secret Key)
- **Hash**: 64 character SHA-256 hash
- **Example**: `hamco_sk_YOUR_API_KEY_HERE`

The prefix allows for:
- Easy identification in logs and code
- Programmatic detection of leaked keys
- Future support for different key types (e.g., `hamco_pk_` for public keys)

## Troubleshooting

**403 Forbidden on Write Operations:**
- Verify the API key has `isAdmin: true`
- Check that you're using the correct key
- Ensure the key hasn't been revoked

**401 Unauthorized:**
- Verify the API key is in the `X-API-Key` header
- Check for typos in the key
- Confirm the key hasn't expired or been revoked

**Invalid API Key Errors:**
- API key must start with `hamco_sk_`
- Full key must be exactly 74 characters (10 char prefix + 64 char hash)
- Key is case-sensitive

## Implementation Details

**Authentication Flow:**
1. Client sends request with `X-API-Key` header
2. `ApiKeyMiddleware` intercepts request
3. Middleware extracts key prefix and looks up key in database
4. If found and active, validates full key hash
5. Sets `User` claims with role information
6. Authorization policy checks role for protected endpoints

**Authorization Policies:**
- Read operations: No auth required (public)
- Write operations: Require `Admin` role
- Admin endpoints: Require `Admin` role

For implementation details, see:
- `src/Hamco.Api/Middleware/ApiKeyMiddleware.cs`
- `tests/Hamco.Api.Tests/ApiKeyPermissionsTests.cs`
