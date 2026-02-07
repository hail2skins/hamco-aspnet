# Hamco - Blog Note Management System

A modern RESTful API built with **ASP.NET Core** and **PostgreSQL** for managing blog posts (notes). This is a C# reimplementation of the Django-based hamco-python project.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Database Schema](#database-schema)
- [Getting Started](#getting-started)
- [Authentication & Authorization](#authentication--authorization)
- [API Endpoints](#api-endpoints)
- [Running Tests](#running-tests)
- [Configuration](#configuration)
- [Security Considerations](#security-considerations)
- [Development Notes](#development-notes)

---

## 🎯 Overview

Hamco is a learning project demonstrating modern C# web development patterns:
- **Test-Driven Development (TDD)** with xUnit
- **Clean Architecture** with separated layers
- **Entity Framework Core** with PostgreSQL
- **JWT Authentication** with BCrypt password hashing
- **Role-Based Authorization** (Admin vs Regular Users)
- **RESTful API** design following HTTP conventions

**Current Status:** Fully implemented authentication and authorization system:
- ✅ JWT authentication with secure token generation
- ✅ Role-based access control (RBAC)
- ✅ First user automatically becomes admin
- ✅ Admin-only write operations on notes
- ✅ Public read access to notes (blog-style)
- ✅ 23 comprehensive tests covering auth and authorization

---

## ✨ Features

### Implemented ✅

#### Notes (Blog Posts)
- **Create Note** - Admin only (POST /api/notes)
- **Read Note(s)** - Public access (GET /api/notes/{id}, GET /api/notes)
- **Update Note** - Admin only (PUT /api/notes/{id})
- **Delete Note** - Admin only (DELETE /api/notes/{id})
- Auto-generated URL slugs from titles
- Timestamps (created_at, updated_at)
- Notes are linked to authenticated user (required UserId)

#### Authentication & Authorization
- **User Registration** with email/password validation
- **User Login** with JWT token generation
- **BCrypt Password Hashing** (work factor 12)
- **JWT Token Validation** middleware
- **Role-Based Access Control** (Admin role)
- **First User Becomes Admin** automatically
- **Profile Endpoint** for authenticated users

#### Testing
- **10 Auth Endpoint Tests** - Registration, login, profile, validation
- **13 Notes Authorization Tests** - Admin write, public read, auth enforcement
- **Integration Testing** with real database
- **Success and failure path testing**

### Coming Soon 🚧
- Email verification (Mailjet integration)
- Password reset flow
- Soft delete implementation
- Pagination for note listings
- Search and filtering

---

## 🛠️ Technology Stack

| Technology | Purpose |
|------------|---------|
| **.NET 10** | Application runtime |
| **ASP.NET Core** | Web API framework |
| **Entity Framework Core** | ORM for database access |
| **PostgreSQL** | Relational database |
| **xUnit** | Testing framework |
| **BCrypt.Net** | Password hashing |
| **System.IdentityModel.Tokens.Jwt** | JWT token generation/validation |

---

## 📁 Project Structure

```
hamco/
├── src/
│   ├── Hamco.Api/                  # Web API layer (Controllers, Program.cs)
│   │   └── Controllers/
│   │       ├── NotesController.cs  # CRUD endpoints with auth enforcement
│   │       └── AuthController.cs   # Register/Login endpoints
│   │
│   ├── Hamco.Core/                 # Domain models, services, interfaces
│   │   ├── Models/
│   │   │   ├── Note.cs             # Note entity (UserId required)
│   │   │   ├── User.cs             # User entity (IsAdmin, IsEmailVerified)
│   │   │   ├── *Request.cs         # API request DTOs
│   │   │   └── *Response.cs        # API response DTOs
│   │   ├── Services/
│   │   │   ├── IJwtService.cs      # JWT interface
│   │   │   ├── JwtService.cs       # JWT implementation
│   │   │   ├── IPasswordHasher.cs  # Password hasher interface
│   │   │   └── PasswordHasher.cs   # BCrypt password hasher
│   │   ├── Utilities/
│   │   │   └── SlugGenerator.cs    # URL slug generation
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs  # DI configuration
│   │
│   ├── Hamco.Data/                 # Database layer (DbContext, Migrations)
│   │   ├── HamcoDbContext.cs       # EF Core DbContext
│   │   └── Migrations/             # EF Core migration files
│   │
│   └── Hamco.Services/             # Application services (empty, reserved)
│
└── tests/
    ├── Hamco.Api.Tests/
    │   ├── AuthControllerTests.cs  # Auth endpoint tests (10 tests)
    │   └── NotesControllerTests.cs # Notes authorization tests (13 tests)
    │
    └── Hamco.Core.Tests/           # Unit tests (empty, reserved)
```

**Layer Responsibilities:**
- **Hamco.Api:** HTTP layer, routing, request/response handling, auth enforcement
- **Hamco.Core:** Business logic, domain models, service interfaces
- **Hamco.Data:** Database access, EF Core configuration
- **Hamco.Services:** Application services (future use)

---

## 🗄️ Database Schema

### `users` Table
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | string (GUID) | Primary Key | User ID |
| `username` | string | Required | Display name |
| `email` | string | Required, Unique | Login email |
| `password_hash` | string | Required | BCrypt hashed password |
| `is_admin` | boolean | Default: false | Admin privileges |
| `is_email_verified` | boolean | Default: false | Email verification status |
| `created_at` | timestamp | Default: now() | Account creation time |

### `notes` Table
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | int | Primary Key, Auto-increment | Note ID |
| `title` | string(255) | Required | Note title |
| `slug` | string(255) | Required | URL-friendly slug |
| `content` | text | Required | Note content (markdown supported) |
| `user_id` | string | Foreign Key (required) | Author ID (JWT-based) |
| `created_at` | timestamp | Default: now() | Creation time |
| `updated_at` | timestamp | Default: now() | Last update time |
| `deleted_at` | timestamp | Nullable | Soft delete timestamp (future) |

**Relationships:**
- `notes.user_id` → `users.id` (Many-to-One, Required)
- Foreign key enforces referential integrity

---

## 🚀 Getting Started

### Prerequisites

- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/download))
- **PostgreSQL 14+** ([Download](https://www.postgresql.org/download/))
- **Git** (for cloning the repository)

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd hamco
   ```

2. **Create PostgreSQL database**
   ```bash
   psql -U postgres
   CREATE DATABASE hamco_dev;
   \q
   ```

3. **Configure connection string**
   
   Edit `src/Hamco.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=hamco_dev;Username=your_user;Password=your_password"
     },
     "Jwt": {
       "Key": "your-very-secret-key-that-is-at-least-32-characters-long",
       "Issuer": "hamco-api",
       "Audience": "hamco-client"
     }
   }
   ```

4. **Run database migrations**
   ```bash
   cd src/Hamco.Api
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

   API will start at: `https://localhost:5001` (or `http://localhost:5000`)

---

## 🔐 Authentication & Authorization

### Authentication Flow

1. **Register First Admin User** (First user automatically becomes admin)
   ```bash
   curl -X POST https://localhost:5001/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{
       "username": "admin",
       "email": "admin@example.com",
       "password": "AdminPass123"
     }'
   ```
   Response:
   ```json
   {
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "userId": "a1b2c3d4-...",
     "email": "admin@example.com",
     "roles": [],
     "expiresAt": "2026-02-06T15:30:00Z"
   }
   ```

2. **Get JWT Token** (for subsequent requests)
   - Use token from registration, OR
   - Login to get a new token:
   ```bash
   curl -X POST https://localhost:5001/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{
       "email": "admin@example.com",
       "password": "AdminPass123"
     }'
   ```

3. **Use Token in Requests**
   Include the token in the `Authorization` header:
   ```bash
   curl https://localhost:5001/api/notes \
     -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
   ```

### Authorization Rules

| Endpoint | Method | Access Level | Why? |
|----------|--------|--------------|------|
| `/api/auth/register` | POST | Public | Anyone can create account |
| `/api/auth/login` | POST | Public | Anyone can authenticate |
| `/api/auth/profile` | GET | Authenticated | Only for logged-in users |
| `/api/notes` | GET | Public | Blog is readable by all |
| `/api/notes/{id}` | GET | Public | Individual posts are public |
| `/api/notes` | POST | Admin Only | Only admins can publish |
| `/api/notes/{id}` | PUT | Admin Only | Only admins can edit |
| `/api/notes/{id}` | DELETE | Admin Only | Only admins can delete |

### Why Public Read + Admin Write?

**Blog-style architecture:**
- **Public GET**: Blog content should be accessible to everyone (SEO, sharing)
- **Admin-only POST/PUT/DELETE**: Content management requires elevated privileges
- **First user = Admin**: Simplifies initial setup (no separate admin creation)

**Security benefits:**
- Reduces attack surface (write operations protected)
- Clear separation of concerns (readers vs authors)
- Easy to audit (all writes linked to authenticated admin)

---

## 📡 API Endpoints

### Authentication Endpoints

#### Register New User
Creates a new user account. First user automatically becomes admin.

```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "securepassword123"
}
```

**Response (201 Created):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "a1b2c3d4-...",
  "email": "john@example.com",
  "roles": [],
  "expiresAt": "2026-02-06T15:30:00Z"
}
```

**Response (409 Conflict):** If email already exists

---

#### Login
Authenticates user and returns JWT token.

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "securepassword123"
}
```

**Response (200 OK):** Same format as registration

**Response (401 Unauthorized):** Invalid credentials (generic error for security)

---

#### Get Profile
Returns current user's profile (requires authentication).

```http
GET /api/auth/profile
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "token": "",
  "userId": "a1b2c3d4-...",
  "email": "john@example.com",
  "roles": [],
  "expiresAt": "0001-01-01T00:00:00"
}
```

**Response (401 Unauthorized):** No token or invalid token

---

### Notes Endpoints

#### Create Note (Admin Only)
Creates a new blog post. Requires admin authentication.

```http
POST /api/notes
Content-Type: application/json
Authorization: Bearer {admin_token}

{
  "title": "My First Blog Post",
  "content": "This is the content of my first post!"
}
```

**Response (201 Created):**
```json
{
  "id": 1,
  "title": "My First Blog Post",
  "slug": "my-first-blog-post",
  "content": "This is the content of my first post!",
  "userId": "a1b2c3d4-...",
  "createdAt": "2026-02-06T13:30:00Z",
  "updatedAt": "2026-02-06T13:30:00Z"
}
```

**Response (401 Unauthorized):** No token provided

**Response (403 Forbidden):** Token valid but user is not admin

---

#### Get Single Note (Public)
Retrieves a single note by ID. No authentication required.

```http
GET /api/notes/1
```

**Response (200 OK):**
```json
{
  "id": 1,
  "title": "My First Blog Post",
  "slug": "my-first-blog-post",
  "content": "This is the content of my first post!",
  "userId": "a1b2c3d4-...",
  "createdAt": "2026-02-06T13:30:00Z",
  "updatedAt": "2026-02-06T13:30:00Z"
}
```

**Response (404 Not Found):** Note doesn't exist

---

#### Get All Notes (Public)
Retrieves all notes. No authentication required.

```http
GET /api/notes
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "title": "My First Blog Post",
    "slug": "my-first-blog-post",
    "content": "This is the content of my first post!",
    "userId": "a1b2c3d4-...",
    "createdAt": "2026-02-06T13:30:00Z",
    "updatedAt": "2026-02-06T13:30:00Z"
  }
]
```

---

#### Update Note (Admin Only)
Updates an existing note. Requires admin authentication.

```http
PUT /api/notes/1
Content-Type: application/json
Authorization: Bearer {admin_token}

{
  "title": "Updated Title",
  "content": "Updated content here..."
}
```

**Response (200 OK):** Updated note (slug regenerated from new title)

**Response (401 Unauthorized):** No token provided

**Response (403 Forbidden):** Token valid but user is not admin

**Response (404 Not Found):** Note doesn't exist

---

#### Delete Note (Admin Only)
Deletes a note from the database. Requires admin authentication.

```http
DELETE /api/notes/1
Authorization: Bearer {admin_token}
```

**Response (204 No Content):** Note deleted successfully

**Response (401 Unauthorized):** No token provided

**Response (403 Forbidden):** Token valid but user is not admin

**Response (404 Not Found):** Note doesn't exist

---

## 🧪 Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/Hamco.Api.Tests/Hamco.Api.Tests.csproj
```

### Run Tests with Detailed Output
```bash
dotnet test --verbosity detailed
```

### Test Coverage

#### AuthController Tests (10 tests)
- ✅ `Register_ValidData_CreatesUserReturns201` - Successful registration
- ✅ `Register_DuplicateEmail_Returns409` - Duplicate email prevention
- ✅ `Register_InvalidData_Returns400` - Input validation
- ✅ `Login_ValidCredentials_Returns200WithToken` - Successful login
- ✅ `Login_InvalidCredentials_Returns401` - Wrong password handling
- ✅ `Login_NonExistentUser_Returns401` - Non-existent user handling
- ✅ `GetProfile_Authenticated_Returns200WithUser` - Profile retrieval
- ✅ `GetProfile_Unauthenticated_Returns401` - Auth requirement
- ✅ `ForgotPassword_ValidEmail_Returns200Stub` - Password reset stub
- ✅ `ResetPassword_Returns200Stub` - Password reset stub

#### NotesController Tests (13 tests)
- ✅ `CreateNote_AdminUser_Returns201` - Admin can create notes
- ✅ `CreateNote_Unauthenticated_Returns401` - Auth required
- ✅ `CreateNote_NonAdmin_Returns403` - Admin role required
- ✅ `UpdateNote_AdminUser_Returns200` - Admin can update notes
- ✅ `UpdateNote_Unauthenticated_Returns401` - Auth required
- ✅ `UpdateNote_NonAdmin_Returns403` - Admin role required
- ✅ `DeleteNote_AdminUser_Returns204` - Admin can delete notes
- ✅ `DeleteNote_Unauthenticated_Returns401` - Auth required
- ✅ `DeleteNote_NonAdmin_Returns403` - Admin role required
- ✅ `GetAllNotes_Public_Returns200` - Public read access
- ✅ `GetNoteById_Public_Returns200` - Public read access
- ✅ `GetNoteById_NotFound_Returns404` - Not found handling

---

## ⚙️ Configuration

### Environment Variables

You can override appsettings.json with environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=hamco_dev;Username=art;Password="
export Jwt__Key="your-secret-key-here"
export Jwt__Issuer="hamco-api"
export Jwt__Audience="hamco-client"
```

### JWT Configuration

- **Key:** Must be at least 32 characters (enforced by JwtService)
- **Expiration:** Default 60 minutes (configurable in Program.cs)
- **Algorithm:** HMAC-SHA256
- **Token Location:** Supports both `Authorization: Bearer <token>` header

### Database Configuration

**Connection String Format:**
```
Host=<hostname>;Database=<database>;Username=<user>;Password=<password>;Port=5432
```

**Migrations:**
```bash
# Create new migration
dotnet ef migrations add MigrationName --project src/Hamco.Data --startup-project src/Hamco.Api

# Apply migrations
dotnet ef database update --project src/Hamco.Data --startup-project src/Hamco.Api

# Rollback migration
dotnet ef database update PreviousMigrationName --project src/Hamco.Data --startup-project src/Hamco.Api
```

---

## 🔒 Security Considerations

### Password Security

- **BCrypt Hashing:** Passwords are hashed with work factor 12 (~300ms per hash)
- **Salt:** Each password has a unique random salt
- **No Plain Text:** Original passwords are never stored or logged
- **Constant-Time Comparison:** Prevents timing attacks

### JWT Security

- **Secret Key:** Must be at least 32 characters, kept secure
- **Expiration:** Tokens expire after 60 minutes
- **HTTPS Required:** Always use HTTPS in production
- **Bearer Token:** Sent in Authorization header

### Authorization Security

- **Server-Side Validation:** Never trust client-side role checks
- **Admin-Only Writes:** POST/PUT/DELETE require admin role
- **Public Reads:** GET endpoints accessible without authentication
- **Generic Error Messages:** Don't reveal if email exists (prevents enumeration)

### Best Practices

1. **Change Default JWT Key** in production
2. **Enable HTTPS** in production
3. **Use Strong Passwords** (12+ characters, mixed case, numbers, symbols)
4. **Implement Rate Limiting** to prevent brute force attacks
5. **Enable Email Verification** before allowing note creation
6. **Add Audit Logging** for admin actions

---

## 📝 Development Notes

### First User = Admin

The first user registered automatically gets `IsAdmin = true`. This simplifies initial setup:

```csharp
// In AuthController.Register()
var isFirstUser = !await _context.Users.AnyAsync();
var user = new User
{
    // ...
    IsAdmin = isFirstUser,  // First user becomes admin
    // ...
};
```

Subsequent users are regular users (`IsAdmin = false`).

### Role-Based Authorization

Admin-only endpoints use the `[Authorize(Roles = "Admin")]` attribute:

```csharp
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<NoteResponse>> CreateNote(CreateNoteRequest request)
{
    // Only admins can reach this code
}
```

The role is extracted from JWT token claims and checked by the authorization middleware.

### Slug Generation

Slugs are automatically generated from note titles:
- Converts to lowercase
- Removes special characters (keeps a-z, 0-9, spaces, hyphens)
- Replaces spaces with hyphens
- Removes duplicate hyphens
- Trims leading/trailing hyphens

**Examples:**
- "Hello World" → "hello-world"
- "ASP.NET Core Tips!" → "aspnet-core-tips"
- "  Multiple   Spaces  " → "multiple-spaces"

### Testing Strategy

Tests use `WebApplicationFactory<Program>` for integration testing:
- Spins up full API in-memory
- Uses real database (hamco_dev)
- Tests entire request/response pipeline
- Sequential execution for notes tests (prevents race conditions)

**Collection attribute ensures sequential execution:**
```csharp
[CollectionDefinition("NotesTests", DisableParallelization = true)]
public class NotesTestsCollection { }
```

---

## 🤝 Contributing

This is a learning project! Feel free to:
- Add email verification (Mailjet integration)
- Implement soft delete with restore functionality
- Add pagination to note listings
- Add search and filtering capabilities
- Improve error handling with problem details
- Add logging with Serilog

---

## 📚 Learning Resources

If you're new to C# or ASP.NET Core, these concepts are demonstrated in this project:

- **Dependency Injection:** See `Program.cs` and `ServiceCollectionExtensions.cs`
- **Entity Framework:** See `HamcoDbContext.cs` and `Migrations/`
- **Async/Await:** See all controller actions and database operations
- **Data Annotations:** See `*Request.cs` models for validation
- **JWT Authentication:** See `JwtService.cs` and `ServiceCollectionExtensions.cs`
- **Authorization:** See `NotesController.cs` with `[Authorize]` attributes
- **Integration Testing:** See `AuthControllerTests.cs` and `NotesControllerTests.cs`
- **Repository Pattern:** DbContext acts as repository
- **DTO Pattern:** Separate Request/Response models from domain entities

---

## 📄 License

[Your License Here]

---

## 🙏 Acknowledgments

- Inspired by the Django-based hamco-python project
- Built with love for learning C# and ASP.NET Core
- Special thanks to the .NET community for excellent documentation
