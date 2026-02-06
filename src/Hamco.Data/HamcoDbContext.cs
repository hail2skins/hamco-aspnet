using Microsoft.EntityFrameworkCore;
using Hamco.Core.Models;

namespace Hamco.Data;

/// <summary>
/// Entity Framework Core database context for the Hamco application.
/// Manages database connections, entity tracking, and query translation.
/// </summary>
/// <remarks>
/// What is DbContext?
///   DbContext is Entity Framework Core's main class for database operations.
///   Think of it as a "database session" that:
///   - Tracks changes to entities (change tracking)
///   - Translates LINQ queries to SQL
///   - Manages database connections
///   - Saves changes back to database
/// 
/// Inheritance in C#:
///   'public class HamcoDbContext : DbContext'
///   Means: HamcoDbContext inherits from DbContext (parent class)
///   - Gets all DbContext functionality automatically
///   - Can override/extend with custom behavior
///   - 'base(options)' calls parent constructor
/// 
/// Entity Framework Core (EF Core):
///   - Object-Relational Mapper (ORM)
///   - Maps C# classes (entities) to database tables
///   - Generates SQL automatically from LINQ queries
///   - Handles migrations (database schema versioning)
/// 
/// Example usage in controller:
///   var note = await _context.Notes.FindAsync(id);
///   _context.Notes.Add(newNote);
///   await _context.SaveChangesAsync();
/// 
/// DbContext lifecycle (per HTTP request):
///   1. Request arrives
///   2. DI container creates new HamcoDbContext
///   3. Controller receives context, performs operations
///   4. Request completes
///   5. DbContext disposed (connection closed, changes discarded if not saved)
/// 
/// This is why DbContext is registered as Scoped (one per request).
/// </remarks>
public class HamcoDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the HamcoDbContext.
    /// </summary>
    /// <param name="options">
    /// Configuration options (connection string, provider, etc.).
    /// Passed from Program.cs when registering with DI container.
    /// </param>
    /// <remarks>
    /// Constructor parameter injection:
    ///   When DI container creates HamcoDbContext, it provides DbContextOptions.
    ///   These options contain:
    ///   - Database provider (PostgreSQL, SQL Server, SQLite, etc.)
    ///   - Connection string
    ///   - Logging configuration
    ///   - Other EF Core settings
    /// 
    /// Configured in Program.cs:
    ///   services.AddDbContext&lt;HamcoDbContext&gt;(options =>
    ///       options.UseNpgsql(connectionString));
    /// 
    /// 'base(options)' explained:
    ///   - 'base' refers to parent class (DbContext)
    ///   - Calls DbContext constructor with options
    ///   - Required because DbContext needs configuration
    ///   - Constructor chaining in C# (this class → parent class)
    /// 
    /// DbContextOptions&lt;HamcoDbContext&gt;:
    ///   Generic type: DbContextOptions specialized for our context
    ///   Why generic? Ensures type safety (can't accidentally use wrong context)
    /// </remarks>
    public HamcoDbContext(DbContextOptions<HamcoDbContext> options) : base(options)
    {
        // Constructor body is empty because:
        // - All configuration passed to base class (DbContext)
        // - No additional initialization needed
        // - DbContext handles everything internally
    }

    /// <summary>
    /// Gets or sets the Users table.
    /// Used to query and manipulate user data.
    /// </summary>
    /// <remarks>
    /// DbSet&lt;User&gt; explained:
    ///   - DbSet represents a table in the database
    ///   - Generic type (User) specifies the entity type
    ///   - Acts like a collection of User objects
    ///   - Provides LINQ query methods (Where, Select, etc.)
    /// 
    /// Example queries:
    ///   // Get all users
    ///   var users = await Users.ToListAsync();
    ///   
    ///   // Find by ID
    ///   var user = await Users.FindAsync(id);
    ///   
    ///   // Filter with LINQ
    ///   var admins = await Users
    ///       .Where(u => u.Roles.Contains("Admin"))
    ///       .ToListAsync();
    ///   
    ///   // Add new user
    ///   Users.Add(newUser);
    ///   await SaveChangesAsync();
    /// 
    /// 'null!' explained (null-forgiving operator):
    ///   - EF Core initializes this property (not null at runtime)
    ///   - But compiler doesn't know that (warns about null)
    ///   - '!' tells compiler "trust me, this won't be null"
    ///   - Required for nullable reference types (C# 8+)
    /// 
    /// Why not initialize in constructor?
    ///   EF Core uses reflection to set this property.
    ///   We don't set it manually.
    /// </remarks>
    public DbSet<User> Users { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the Notes table.
    /// Used to query and manipulate blog note data.
    /// </summary>
    /// <remarks>
    /// Same concept as Users DbSet (see above).
    /// 
    /// Example operations:
    ///   // Create note
    ///   var note = new Note { Title = "Hello", Content = "World" };
    ///   Notes.Add(note);
    ///   await SaveChangesAsync();
    ///   
    ///   // Update note
    ///   note.Title = "Updated";
    ///   await SaveChangesAsync();  // EF tracks changes automatically
    ///   
    ///   // Delete note
    ///   Notes.Remove(note);
    ///   await SaveChangesAsync();
    ///   
    ///   // Soft delete (current approach: hard delete)
    ///   note.DeletedAt = DateTime.UtcNow;
    ///   await SaveChangesAsync();
    /// 
    /// Change tracking:
    ///   EF Core tracks modifications to entities loaded from database.
    ///   When you change a property, EF remembers it.
    ///   SaveChangesAsync() generates UPDATE SQL for changed properties only.
    /// </remarks>
    public DbSet<Note> Notes { get; set; } = null!;

    /// <summary>
    /// Configures the database schema using Fluent API.
    /// Called by EF Core when building the database model.
    /// </summary>
    /// <param name="modelBuilder">
    /// Builder object for configuring entity mappings.
    /// </param>
    /// <remarks>
    /// OnModelCreating() is called once when DbContext is first used.
    /// Used to configure:
    ///   - Table names
    ///   - Column names and types
    ///   - Primary keys
    ///   - Foreign keys and relationships
    ///   - Indexes
    ///   - Default values
    ///   - Constraints
    /// 
    /// Fluent API vs Data Annotations:
    ///   Data Annotations: Attributes on entity classes ([Key], [Required])
    ///   Fluent API: Configuration in OnModelCreating() (what we use here)
    /// 
    /// Why Fluent API?
    ///   ✅ More powerful (can do things Data Annotations can't)
    ///   ✅ Keeps entity classes clean (no database-specific attributes)
    ///   ✅ All configuration in one place (easier to review)
    ///   ✅ Better for complex mappings
    /// 
    /// 'protected override' explained:
    ///   - 'protected': Only this class and derived classes can call it
    ///   - 'override': Overriding parent class (DbContext) method
    ///   - Parent class has 'virtual' method (can be overridden)
    ///   - Our version extends/replaces parent behavior
    /// 
    /// 'base.OnModelCreating(modelBuilder)' calls parent implementation first.
    /// Important! Always call base method to preserve default behavior.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Call parent class's OnModelCreating first
        // This ensures EF Core's default configurations are applied
        base.OnModelCreating(modelBuilder);

        // Configure User entity (maps to 'users' table)
        // 'modelBuilder.Entity&lt;User&gt;()' returns EntityTypeBuilder&lt;User&gt;
        // Lambda syntax: entity => { ... } configures the entity
        modelBuilder.Entity<User>(entity =>
        {
            // ToTable(): Specifies database table name
            // By default, EF uses plural class name (Users)
            // We override to use lowercase 'users' (PostgreSQL convention)
            entity.ToTable("users");
            
            // HasKey(): Specifies primary key column(s)
            // 'e => e.Id' is a lambda that selects the Id property
            // Lambda: e (entity) => e.Id (select Id property)
            entity.HasKey(e => e.Id);
            
            // Property(): Configures individual properties (columns)
            // Chained configuration: Property(...).HasColumnName(...).IsRequired()
            // This is the Fluent API pattern (method chaining)
            
            // Configure Id column
            entity.Property(e => e.Id)
                .HasColumnName("id");  // PostgreSQL column name (lowercase)
            
            // Configure Username column
            entity.Property(e => e.Username)
                .HasColumnName("username")
                .IsRequired();  // NOT NULL constraint in database
            
            // Configure Email column
            entity.Property(e => e.Email)
                .HasColumnName("email")
                .IsRequired();
            
            // Configure PasswordHash column
            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash")  // snake_case (PostgreSQL convention)
                .IsRequired();
            
            // Configure CreatedAt column
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");  // Database generates default
            
            // Ignore(): Exclude property from database mapping
            // Roles property is NOT stored in database
            // It's populated from JWT claims at runtime
            // 
            // Why ignore?
            //   - Roles could change frequently
            //   - Would need separate user_roles table for proper storage
            //   - For now, roles come from JWT token only
            //   - Future: Create UserRole entity and table
            entity.Ignore(e => e.Roles);
        });

        // Configure Note entity (maps to 'notes' table)
        modelBuilder.Entity<Note>(entity =>
        {
            // Table name: 'notes' (lowercase, PostgreSQL convention)
            entity.ToTable("notes");
            
            // Primary key: Id column
            entity.HasKey(e => e.Id);
            
            // Configure Id column (auto-increment in PostgreSQL)
            entity.Property(e => e.Id)
                .HasColumnName("id");
            
            // Configure Title column
            entity.Property(e => e.Title)
                .HasColumnName("title")
                .IsRequired()  // NOT NULL
                .HasMaxLength(255);  // VARCHAR(255) in database
            
            // Configure Slug column
            entity.Property(e => e.Slug)
                .HasColumnName("slug")
                .IsRequired()
                .HasMaxLength(255);
            
            // Configure Content column
            entity.Property(e => e.Content)
                .HasColumnName("content")
                .IsRequired();
            // No max length = TEXT type in PostgreSQL (unlimited)
            
            // Configure UserId column (foreign key)
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            // Not required (nullable) - allows anonymous notes
            
            // Configure CreatedAt column
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Configure UpdatedAt column
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Configure DeletedAt column (soft delete)
            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");
            // Nullable (only set when deleted)

            // Configure relationship: Note → User
            // HasOne(): This entity (Note) has one related entity (User)
            // WithMany(): Related entity (User) has many of this entity (Notes)
            // HasForeignKey(): Foreign key column (UserId)
            // OnDelete(): What happens when User is deleted
            // IsRequired(false): Relationship is optional (UserId can be null)
            entity.HasOne(e => e.User)        // Note.User navigation property
                .WithMany()                   // User has many Notes (no navigation property on User)
                .HasForeignKey(e => e.UserId) // Foreign key: note.user_id → users.id
                .OnDelete(DeleteBehavior.SetNull)  // When user deleted, set note.user_id = NULL
                .IsRequired(false);           // Foreign key is nullable
            
            // DeleteBehavior options:
            //   - Cascade: Delete notes when user deleted (orphan removal)
            //   - SetNull: Set note.user_id = NULL (preserve notes)
            //   - Restrict: Prevent user deletion if they have notes
            //   - NoAction: Do nothing (database may enforce constraint)
            // 
            // We use SetNull to preserve notes even if user is deleted.
        });
    }
}
