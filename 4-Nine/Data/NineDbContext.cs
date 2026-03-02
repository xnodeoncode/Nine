using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nine.Entities;

namespace Nine.Data;

/// <summary>
/// Nine database context for Identity management.
/// Handles all ASP.NET Core Identity tables and Nine-specific user data.
/// Shares the same database as ApplicationDbContext using the same connection string.
/// </summary>
public class NineDbContext : IdentityDbContext<ApplicationUser>
{
    public NineDbContext(DbContextOptions<NineDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Identity table configuration is handled by base IdentityDbContext
        // Add any Nine-specific user configurations here if needed
    }
}
