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
- [API Endpoints](#api-endpoints)
- [Running Tests](#running-tests)
- [Configuration](#configuration)
- [Development Notes](#development-notes)

---

## 🎯 Overview

Hamco is a learning project demonstrating modern C# web development patterns:
- **Test-Driven Development (TDD)** with xUnit
- **Clean Architecture** with separated layers
- **Entity Framework Core** with PostgreSQL
- **JWT Authentication** with BCrypt password hashing
- **RESTful API** design following HTTP conventions

**Current Status:** CRUD operations for notes fully implemented and tested. JWT authentication implemented but not yet enforced on note endpoints.

---

## ✨ Features

### Implemented ✅
- **Notes (Blog Posts)**
  - Create, Read, Update, Delete operations
  - Auto-generated URL slugs from titles
  - Timestamps (created_at, updated_at)
  - Soft delete support (field present, hard delete currently used)

- **Authentication**
  - User registration with email/password
  - User login with JWT token generation
  - BCrypt password hashing (work factor 12)
  - JWT token validation middleware

- **Testing**
  - Comprehensive integration tests for Notes API
  - Test coverage for all CRUD operations
  - Success and failure path testing

### Coming Soon 🚧
- Authorization enforcement on note endpoints
- Role-based access control (RBAC)
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
│   │       ├── NotesController.cs  # CRUD endpoints for notes
│   │       └── AuthController.cs   # Register/Login endpoints
│   │
│   ├── Hamco.Core/                 # Domain models, services, interfaces
│   │   ├── Models/
│   │   │   ├── Note.cs             # Note entity
│   │   │   ├── User.cs             # User entity
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
    │   └── NotesControllerTests.cs # Integration tests for Notes API
    │
    └── Hamco.Core.Tests/           # Unit tests (empty, reserved)
```

**Layer Responsibilities:**
- **Hamco.Api:** HTTP layer, routing, request/response handling
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
| `created_at` | timestamp | Default: now() | Account creation time |

### `notes` Table
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | int | Primary Key, Auto-increment | Note ID |
| `title` | string(255) | Required | Note title |
| `slug` | string(255) | Required | URL-friendly slug |
| `content` | text | Required | Note content (markdown supported) |
| `user_id` | string | Foreign Key (nullable) | Author ID |
| `created_at` | timestamp | Default: now() | Creation time |
| `updated_at` | timestamp | Default: now() | Last update time |
| `deleted_at` | timestamp | Nullable | Soft delete timestamp |

**Relationships:**
- `notes.user_id` → `users.id` (Many-to-One, Optional)
- Foreign key uses `ON DELETE SET NULL` (preserves notes if user deleted)

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

6. **Test the API**
   ```bash
   curl https://localhost:5001/api/notes
   ```

---

## 📡 API Endpoints

### Authentication

#### Register New User
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "securepassword123"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "a1b2c3d4-...",
  "email": "john@example.com",
  "roles": [],
  "expiresAt": "2026-02-06T15:30:00Z"
}
```

#### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "securepassword123"
}
```

**Response:** Same as registration

---

### Notes (Blog Posts)

#### Create Note
```http
POST /api/notes
Content-Type: application/json

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
  "userId": null,
  "createdAt": "2026-02-06T13:30:00Z",
  "updatedAt": "2026-02-06T13:30:00Z"
}
```

#### Get Single Note
```http
GET /api/notes/1
```

**Response (200 OK):** Same structure as create response

**Response (404 Not Found):** If note doesn't exist or was deleted

#### Get All Notes
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
    "userId": null,
    "createdAt": "2026-02-06T13:30:00Z",
    "updatedAt": "2026-02-06T13:30:00Z"
  },
  {
    "id": 2,
    "title": "Another Post",
    "slug": "another-post",
    "content": "More content here...",
    "userId": null,
    "createdAt": "2026-02-06T14:00:00Z",
    "updatedAt": "2026-02-06T14:00:00Z"
  }
]
```

#### Update Note
```http
PUT /api/notes/1
Content-Type: application/json

{
  "title": "Updated Title",
  "content": "Updated content here..."
}
```

**Response (200 OK):** Updated note (slug regenerated from new title)

**Response (404 Not Found):** If note doesn't exist

**Response (400 Bad Request):** If validation fails (empty title, etc.)

#### Delete Note
```http
DELETE /api/notes/1
```

**Response (204 No Content):** Note deleted successfully

**Response (404 Not Found):** If note doesn't exist

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

Currently implemented:
- ✅ **NotesController:** 12 integration tests
  - Create (valid, invalid)
  - Read (single, list, not found, empty)
  - Update (valid, invalid, not found)
  - Delete (valid, not found, verification)

Coming soon:
- 🚧 AuthController tests
- 🚧 Service layer unit tests
- 🚧 Slug generator tests

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
- **Token Location:** Supports both `Authorization: Bearer <token>` header and `AuthToken` cookie

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

## 📝 Development Notes

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

### Soft Delete vs Hard Delete

⚠️ **Current Inconsistency:** The `Note` model has a `DeletedAt` field (soft delete), but the controller performs hard deletes (removes from database).

**To implement soft delete:**
```csharp
// In DeleteNote action:
note.DeletedAt = DateTime.UtcNow;
await _context.SaveChangesAsync();
```

**To remove soft delete:**
- Remove `DeletedAt` property from Note model
- Create and apply migration

### Authentication Flow

1. User registers via `/api/auth/register` → receives JWT token
2. User includes token in subsequent requests: `Authorization: Bearer <token>`
3. JWT middleware validates token and populates `HttpContext.User`
4. Controllers can use `[Authorize]` attribute to require authentication

**Note:** Currently, note endpoints do NOT require authentication. This is intentional for development.

### Testing Strategy

Tests use `WebApplicationFactory<Program>` for integration testing:
- Spins up full API in-memory
- Uses real database (hamco_dev)
- Tests entire request/response pipeline
- No mocking of infrastructure

**Pros:** Tests real behavior, catches integration issues  
**Cons:** Tests share database, may affect each other

---

## 🤝 Contributing

This is a learning project! Feel free to:
- Add tests for AuthController
- Implement soft delete
- Add pagination to note listings
- Improve error handling
- Add logging

---

## 📚 Learning Resources

If you're new to C# or ASP.NET Core, these concepts are demonstrated in this project:

- **Dependency Injection:** See `Program.cs` and `ServiceCollectionExtensions.cs`
- **Entity Framework:** See `HamcoDbContext.cs` and `Migrations/`
- **Async/Await:** See all controller actions and database operations
- **Data Annotations:** See `*Request.cs` models for validation
- **JWT Authentication:** See `JwtService.cs` and `ServiceCollectionExtensions.cs`
- **Integration Testing:** See `NotesControllerTests.cs`
- **Repository Pattern:** DbContext acts as repository
- **DTO Pattern:** Separate Request/Response models from domain entities

---

## 📄 License

[Your License Here]

---

## 🙏 Acknowledgments

- Inspired by the Django-based hamco-python project
- Built with love for learning C# and ASP.NET Core
