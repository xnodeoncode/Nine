using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Nine.Core.Interfaces;
using Nine.Core.Interfaces.Services;
using Nine.Application;  // ✅ Application facade
using Nine.Application.Services;
using Nine.Infrastructure.Data;  // For SqlCipherConnectionInterceptor
using Nine.Data;
using Nine.Entities;
using Nine.Services;  // For ElectronPathService, WebPathService
using Nine.Infrastructure.Services;  // For DatabaseUnlockState
using Nine.Infrastructure.Interfaces;  // For IKeychainService
using Microsoft.Data.Sqlite;

namespace Nine.Extensions;

/// <summary>
/// Extension methods for configuring Web-specific services for Nine.
/// </summary>
public static class WebServiceExtensions
{
    // Toggle for verbose logging (useful for troubleshooting encryption setup)
    private const bool EnableVerboseLogging = false;
    
    /// <summary>
    /// Adds all Web-specific infrastructure services including database and identity.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register path service
        services.AddScoped<IPathService, WebPathService>();
        
        // ✅ SECURITY: Get connection string from environment variable first (production),
        // then fall back to configuration (development)
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string not found. " +
                "Set DATABASE_CONNECTION_STRING environment variable or configure DefaultConnection in appsettings.json");

        // Check if database is encrypted and retrieve password if needed
        var encryptionPassword = GetEncryptionPasswordIfNeeded(connectionString);

        // Pre-derive raw AES key from passphrase (once at startup) so each connection open
        // uses PRAGMA key = "x'hex'" and skips PBKDF2(256000), saving ~20–50 ms per connection.
        if (!string.IsNullOrEmpty(encryptionPassword))
            encryptionPassword = PrepareEncryptionKey(encryptionPassword, connectionString);

        // Register unlock state before any DbContext registration
        var unlockState = new DatabaseUnlockState
        {
            NeedsUnlock = encryptionPassword == null && IsDatabaseEncrypted(connectionString),
            DatabasePath = ExtractDatabasePath(connectionString),
            
            ConnectionString = connectionString
        };
        services.AddSingleton(unlockState);

        Console.WriteLine($"Database unlock state: NeedsUnlock={unlockState.NeedsUnlock}, DatabasePath={unlockState.DatabasePath}");

        // Register encryption status as singleton for use during startup
        services.AddSingleton(new EncryptionDetectionResult
        {
            IsEncrypted = !string.IsNullOrEmpty(encryptionPassword)
        });

        // If unlock needed, we still register services (so DI doesn't fail)
        // but they won't be able to access database until password is provided
        if (unlockState.NeedsUnlock)
        {
            Console.WriteLine("Database unlock required - services will be registered but database inaccessible until unlock");
        }

        // CRITICAL: Create interceptor instance BEFORE any DbContext registration
        // This single instance will be used by all DbContexts
        SqlCipherConnectionInterceptor? interceptor = null;
        if (!string.IsNullOrEmpty(encryptionPassword))
        {
            interceptor = new SqlCipherConnectionInterceptor(encryptionPassword);

            // Clear connection pools to ensure no connections bypass the interceptor
            SqliteConnection.ClearAllPools();

            // Set WAL mode and synchronous=NORMAL once on a writable connection.
            // These are persistent database settings — no need to repeat on every connection open.
            // This MUST happen here (not in the interceptor) because EF Core also opens read-only
            // connections (e.g. SqliteDatabaseCreator.Exists()), and PRAGMA journal_mode = WAL
            // returns SQLITE_READONLY (8) on a read-only connection.
            using var setupConn = new SqliteConnection(connectionString);
            setupConn.Open();
            using var setupCmd = setupConn.CreateCommand();
            setupCmd.CommandText = $"PRAGMA key = \"{encryptionPassword}\";";
            setupCmd.ExecuteNonQuery();
            setupCmd.CommandText = "PRAGMA journal_mode = WAL;";
            setupCmd.ExecuteNonQuery();
            setupCmd.CommandText = "PRAGMA synchronous = NORMAL;";
            setupCmd.ExecuteNonQuery();
            setupConn.Close();

            SqliteConnection.ClearAllPools();
        }

        // ✅ Register Application layer (includes Infrastructure internally) with encryption interceptor
        services.AddApplication(connectionString, encryptionPassword, interceptor);

        // Register Identity database context (Nine-specific) with encryption interceptor
        services.AddDbContext<NineDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite(connectionString);
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        
        // CRITICAL: Clear connection pools again after DbContext registration
        if (!string.IsNullOrEmpty(encryptionPassword))
        {
            SqliteConnection.ClearAllPools();
        }

        // Register DatabaseService now that both contexts are available
        services.AddScoped<IDatabaseService>(sp => 
            new DatabaseService(
                sp.GetRequiredService<Nine.Infrastructure.Data.ApplicationDbContext>(),
                sp.GetRequiredService<NineDbContext>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabaseService>>()));

        services.AddDatabaseDeveloperPageExceptionFilter();

        // Configure Identity with Web-specific settings
        services.AddIdentity<ApplicationUser, IdentityRole>(options => {
            // For web app, require confirmed email
            options.SignIn.RequireConfirmedAccount = true;
            
            // ✅ SECURITY: Strong password policy (12+ chars, special characters required)
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredUniqueChars = 4; // Prevent patterns like "aaa111!!!"
        })
        .AddEntityFrameworkStores<NineDbContext>()
        .AddDefaultTokenProviders();

        // Configure cookie authentication for Web
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        return services;
    }

    /// <summary>
    /// Detects if database is encrypted and retrieves password from keychain if needed
    /// </summary>
    /// <returns>Encryption password, or null if database is not encrypted</returns>
    internal static string? GetEncryptionPasswordIfNeeded(string connectionString)
    {
        try
        {
            // Extract database path from connection string
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (!File.Exists(dbPath))
            {
                // Database doesn't exist yet, not encrypted
                return null;
            }

            // Try to open as plaintext
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
                        cmd.ExecuteScalar();
                    }
                }
                // Success - database is not encrypted
                return null;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 26) // "file is not a database"
            {
                // Database is encrypted - try to get password from keychain
                var keychain = OperatingSystem.IsWindows()
                    ? (IKeychainService)new WindowsKeychainService("Nine-Web")
                    : new LinuxKeychainService("Nine-Web"); // Pass app name to prevent keychain conflicts
                var password = keychain.RetrieveKey();

                if (string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("Database is encrypted but password not in keychain - will prompt user");
                    return null; // Signal that unlock is needed
                }
                
                // CRITICAL: Clear connection pool to prevent reuse of unencrypted connections
                SqliteConnection.ClearAllPools();
                
                return password;
            }
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw our custom messages
        }
        catch (Exception ex)
        {
            // Log but don't fail - assume database is not encrypted
            Console.WriteLine($"Warning: Could not check database encryption status: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Helper method to check if database is encrypted
    /// </summary>
    private static bool IsDatabaseEncrypted(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dbPath = builder.DataSource;
        
        if (!File.Exists(dbPath)) return false;
        
        try
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            cmd.ExecuteScalar();
            return false; // Opened successfully = not encrypted
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            return true; // Error 26 = encrypted
        }
        catch
        {
            return false; // Other errors = assume not encrypted
        }
    }

    /// <summary>
    /// Helper method to extract database path from connection string
    /// </summary>
    private static string ExtractDatabasePath(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }

    /// <summary>
    /// Pre-derives the AES-256 key from <paramref name="password"/> using SQLCipher 4's PBKDF2
    /// parameters (HMAC-SHA512, 256 000 iterations, 32-byte output).  The salt is read from the
    /// first 16 bytes of the database file — the same salt SQLCipher embedded when the database
    /// was originally encrypted.  The returned value is in SQLCipher's raw-key format
    /// <c>x'hexbytes'</c>, which the interceptor passes directly as <c>PRAGMA key</c>,
    /// skipping all PBKDF2 work on every subsequent connection open.
    ///
    /// Falls back to the original passphrase string if the file cannot be read or is too small
    /// (e.g. first-run before the database exists), in which case the interceptor's passphrase
    /// path handles key derivation as usual.
    /// </summary>
    private static string PrepareEncryptionKey(string password, string connectionString)
    {
        try
        {
            var dbPath = ExtractDatabasePath(connectionString);

            if (!File.Exists(dbPath) || new FileInfo(dbPath).Length < 16)
                return password; // DB not yet created — passphrase path is fine

            // SQLCipher stores its PBKDF2 salt in the first 16 bytes of the database file
            var salt = new byte[16];
            using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Read(salt, 0, 16) < 16) return password;
            }

            // Derive using the same parameters SQLCipher 4 uses by default
            var keyBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(password),
                salt,
                256000,
                System.Security.Cryptography.HashAlgorithmName.SHA512,
                32); // 256-bit AES key
            return "x'" + Convert.ToHexString(keyBytes) + "'";
        }
        catch
        {
            return password; // Any I/O or crypto error — fall back to passphrase
        }
    }
}

/// <summary>
/// Tracks whether database encryption was detected during startup
/// </summary>
public class EncryptionDetectionResult
{
    public bool IsEncrypted { get; set; }
}
