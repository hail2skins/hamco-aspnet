using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hamco.Data;

/// <summary>
/// Design-time factory for creating HamcoDbContext instances.
/// Used by Entity Framework migrations tooling.
/// </summary>
/// <remarks>
/// What is a Design-Time Factory?
///   EF Core migrations need to create DbContext at design time (during `dotnet ef migrations add`).
///   But HamcoDbContext requires DbContextOptions (connection string, provider).
///   This factory provides those options when running EF commands.
/// 
/// Why needed?
///   Normal app: Program.cs configures DbContext with connection string from environment/config
///   Migrations: No Program.cs running, no configuration loaded
///   Solution: Factory provides design-time configuration
/// 
/// Alternative approaches:
///   1. This factory (most explicit, works anywhere)
///   2. IDesignTimeDbContextFactory in startup project (App.csproj)
///   3. Program.cs with CreateHostBuilder (requires full app startup)
/// 
/// Connection String:
///   Uses PostgreSQL default (localhost).
///   Migrations don't need real database (just generates SQL).
///   Production connection string comes from environment variables.
/// </remarks>
public class HamcoDbContextFactory : IDesignTimeDbContextFactory<HamcoDbContext>
{
    /// <summary>
    /// Creates a new instance of HamcoDbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments from EF tools.</param>
    /// <returns>Configured HamcoDbContext instance.</returns>
    public HamcoDbContext CreateDbContext(string[] args)
    {
        // Create options for PostgreSQL
        // Connection string doesn't matter for migrations (just generating SQL)
        var optionsBuilder = new DbContextOptionsBuilder<HamcoDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hamco_dev;Username=postgres;Password=postgres");

        return new HamcoDbContext(optionsBuilder.Options);
    }
}
