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
    public bool IsActive => true; // App is Electron-only

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
        var userDataPath = await GetUserDataPathAsync();
        var dbPath = Path.Combine(userDataPath, dbFileName);

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return dbPath;
    }

    /// <summary>
    /// Gets the database path synchronously (for startup initialization before Electron is ready).
    /// </summary>
    public string GetDatabasePathSync()
    {
        var dbFileName = _configuration["ApplicationSettings:DatabaseFileName"] ?? "app.db";
        var userDataPath = GetUserDataPathSync();
        var dbPath = Path.Combine(userDataPath, dbFileName);

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return dbPath;
    }

    /// <inheritdoc/>
    public Task<string> GetUserDataPathAsync()
    {
        return Task.FromResult(GetUserDataPathSync());
    }

    /// <summary>
    /// Gets the OS-specific user data path for the Nine app.
    /// </summary>
    private string GetUserDataPathSync()
    {
        string basePath;

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

        var userDataPath = Path.Combine(basePath, "Nine");

        if (!Directory.Exists(userDataPath))
        {
            Directory.CreateDirectory(userDataPath);
        }

        return userDataPath;
    }

}
