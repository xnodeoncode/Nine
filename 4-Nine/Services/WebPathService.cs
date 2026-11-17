using Nine.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Nine.Services;

/// <summary>
/// Path service for web/server applications.
/// Uses standard server file system paths.
/// </summary>
public class WebPathService : IPathService
{
    private readonly IConfiguration _configuration;
    
    public WebPathService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public bool IsActive => true;
    
    public async Task<string> GetConnectionStringAsync(object configuration)
    {
        if (configuration is IConfiguration config)
        {
            return await Task.Run(() => config.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));
        }
        throw new ArgumentException("Configuration must be IConfiguration", nameof(configuration));
    }
    
    public async Task<string> GetDatabasePathAsync()
    {
        var connectionString = await GetConnectionStringAsync(_configuration);
        // Extract Data Source from connection string (supports both "Data Source=" and "DataSource=")
        var dbPath = connectionString
            .Replace("Data Source=", "")
            .Replace("DataSource=", "")
            .Split(';')[0]
            .Trim();
        
        if (string.IsNullOrEmpty(dbPath))
        {
            dbPath = "nine.db"; // Default
        }
        
        // Make absolute path if relative
        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.Combine(Directory.GetCurrentDirectory(), dbPath);
        }
        
        return dbPath;
    }
    
    public async Task<string> GetUserDataPathAsync()
    {
        return await Task.Run(() => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }
}
