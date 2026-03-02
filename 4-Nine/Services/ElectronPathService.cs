using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Configuration;
using Nine.Core.Interfaces;

namespace Nine.Services;

/// <summary>
/// Electron-specific implementation of path service.
/// Manages file paths and connection strings for Electron desktop applications.
/// </summary>
public class ElectronPathService : IPathService
{
    private readonly IConfiguration _configuration;

    public ElectronPathService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public bool IsActive => HybridSupport.IsElectronActive;

    /// <inheritdoc/>
    public async Task<string> GetConnectionStringAsync(object configuration)
    {
        var dbPath = await GetDatabasePathAsync();
        return $"DataSource={dbPath};Cache=Shared";
    }

    /// <inheritdoc/>
    public async Task<string> GetDatabasePathAsync()
    {
        var dbFileName = _configuration["ApplicationSettings:DatabaseFileName"] ?? "app.db";
        
        if (HybridSupport.IsElectronActive)
        {
            var userDataPath = await GetUserDataPathAsync();
            var dbPath = Path.Combine(userDataPath, dbFileName);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            return dbPath;
        }
        else
        {
            // Fallback to local path if not in Electron mode
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            return Path.Combine(dataDir, dbFileName);
        }
    }

    /// <summary>
    /// Gets the database path synchronously (for startup initialization before Electron is ready).
    /// </summary>
    public string GetDatabasePathSync()
    {
        var dbFileName = _configuration["ApplicationSettings:DatabaseFileName"] ?? "app.db";
        
        if (HybridSupport.IsElectronActive)
        {
            // Use OS-specific user data path without requiring Electron to be initialized
            var userDataPath = GetUserDataPathSync();
            var dbPath = Path.Combine(userDataPath, dbFileName);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            return dbPath;
        }
        else
        {
            // Fallback to local path if not in Electron mode
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            return Path.Combine(dataDir, dbFileName);
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetUserDataPathAsync()
    {
        if (HybridSupport.IsElectronActive)
        {
            // Use sync method to ensure consistent path resolution
            // This matches the startup behavior and uses "Nine" as the app name
            return GetUserDataPathSync();
        }
        else
        {
            // Fallback for non-Electron mode
            return Path.Combine(Directory.GetCurrentDirectory(), "Data");
        }
    }

    /// <summary>
    /// Gets the user data path synchronously.
    /// </summary>
    private string GetUserDataPathSync()
    {
        if (HybridSupport.IsElectronActive)
        {
            // Determine OS-specific user data path without Electron API
            string basePath;
            var appName = "Nine";
            
            if (OperatingSystem.IsWindows())
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else if (OperatingSystem.IsMacOS())
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Library", "Application Support");
            }
            else // Linux
            {
                basePath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }
            
            var userDataPath = Path.Combine(basePath, appName);
            
            // Ensure directory exists
            if (!Directory.Exists(userDataPath))
            {
                Directory.CreateDirectory(userDataPath);
            }
            
            return userDataPath;
        }
        else
        {
            // Fallback for non-Electron mode
            return Path.Combine(Directory.GetCurrentDirectory(), "Data");
        }
    }

}
