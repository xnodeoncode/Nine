using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nine.Infrastructure.Data;

/// <summary>
/// Design-time factory to allow dotnet-ef migrations to run without the full application host.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite("DataSource=design-time-temp.db");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
