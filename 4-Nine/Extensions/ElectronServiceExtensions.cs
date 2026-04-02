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
/// Extension methods for configuring Electron-specific services for Nine.
/// </summary>
public static class ElectronServiceExtensions
{
    // Toggle for verbose logging (useful for troubleshooting encryption setup)
    private const bool EnableVerboseLogging = false;
    
    /// <summary>
    /// Adds all Electron-specific infrastructure services including database, identity, and path services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElectronServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register path service
        services.AddScoped<IPathService, ElectronPathService>();

        // Get connection string using the path service (synchronous to avoid startup deadlock)
        var connectionString = GetElectronConnectionString(configuration);

        // Check if database is encrypted and retrieve password if needed.
        // encryptedSiblingNoKey is true when a version-scan sibling is encrypted but the
        // keychain has no entry — the target DB may not exist yet so IsDatabaseEncrypted
        // would return false, but we must still show the unlock dialog.
        var encryptionPassword = GetEncryptionPasswordIfNeeded(connectionString, out bool encryptedSiblingNoKey, out string? encryptedSiblingPath);

        // Pre-derive raw AES key from passphrase (once at startup) so each connection open
        // uses PRAGMA key = "x'hex'" and skips PBKDF2(256000), saving ~20–50 ms per connection.
        if (!string.IsNullOrEmpty(encryptionPassword))
            encryptionPassword = PrepareEncryptionKey(encryptionPassword, connectionString);

        // Register unlock state before any DbContext registration.
        // encryptedSiblingNoKey forces NeedsUnlock when the target DB doesn't exist yet but
        // an encrypted sibling was found with no matching keychain entry.
        var unlockState = new DatabaseUnlockState
        {
            NeedsUnlock = encryptedSiblingNoKey || (encryptionPassword == null && IsDatabaseEncrypted(connectionString)),
            RequiresRestartAfterUnlock = encryptedSiblingNoKey,
            EncryptedSiblingPath = encryptedSiblingPath,
            DatabasePath = ExtractDatabasePath(connectionString),
            ConnectionString = connectionString
        };
        services.AddSingleton(unlockState);

        // Register encryption status as singleton for use during startup
        services.AddSingleton(new EncryptionDetectionResult
        {
            IsEncrypted = !string.IsNullOrEmpty(encryptionPassword)
        });

        // If unlock needed, we still register services (so DI doesn't fail)
        // but they won't be able to access database until password is provided
        if (unlockState.NeedsUnlock)
        {
            Console.WriteLine("[ElectronServiceExtensions] Database unlock required - services will be registered but database inaccessible until unlock");
        }

        // CRITICAL: Create interceptor instance BEFORE any DbContext registration.
        // Always created — even for unencrypted DBs — because the interceptor also sets
        // busy_timeout and journal_mode = WAL on every connection.
        SqlCipherConnectionInterceptor interceptor = new SqlCipherConnectionInterceptor(encryptionPassword);

        if (!string.IsNullOrEmpty(encryptionPassword))
        {
            // Clear connection pools so no pre-registration connections bypass the interceptor
            SqliteConnection.ClearAllPools();
        }

        // ✅ Register Application layer (includes Infrastructure internally) with encryption interceptor
        services.AddApplication(connectionString, encryptionPassword, interceptor);

        // Register Identity database context (Nine-specific) with encryption interceptor
        services.AddDbContext<NineDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(interceptor);
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

        // Configure Identity with Electron-specific settings
        services.AddIdentity<ApplicationUser, IdentityRole>(options => {
            // For desktop app, simplify registration (email confirmation can be enabled later via settings)
            options.SignIn.RequireConfirmedAccount = false; // Electron mode
            
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

        // Configure cookie authentication for Electron
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            
            // For Electron desktop app, use longer cookie lifetime
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            
            // Ensure cookie is persisted (not session-only)
            options.Cookie.MaxAge = TimeSpan.FromDays(30);
            options.Cookie.IsEssential = true;
            
            // For localhost Electron app, allow non-HTTPS cookies
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;

            // When the database is locked, the SecurityStampValidator would immediately query
            // the DB to validate the persistent session cookie — crashing with Error 26 before
            // the unlock page can render. Guard against this by rejecting the principal silently
            // (signs out the cookie in-memory) so the user is redirected to the unlock page
            // without any DB access.
            options.Events.OnValidatePrincipal = async context =>
            {
                var lockState = context.HttpContext.RequestServices.GetRequiredService<DatabaseUnlockState>();
                if (lockState.NeedsUnlock)
                {
                    context.RejectPrincipal();
                    return;
                }
                await Microsoft.AspNetCore.Identity.SecurityStampValidator.ValidatePrincipalAsync(context);
            };
        });

        return services;
    }

    /// <summary>
    /// Detects if database is encrypted and retrieves password from keychain if needed.
    /// </summary>
    /// <param name="connectionString">The EF Core connection string for the target database.</param>
    /// <param name="encryptedSiblingNoKey">
    /// Set to <c>true</c> when a version-scan sibling DB is encrypted but no keychain entry
    /// exists. Callers must treat this as NeedsUnlock=true even if the target DB doesn't exist.
    /// </param>
    /// <param name="encryptedSiblingPath">
    /// The file path of the encrypted sibling DB when <paramref name="encryptedSiblingNoKey"/> is true.
    /// Callers must verify the user's password against this path, NOT the target connection string,
    /// to avoid SQLite silently creating an empty DB at the target path during verification.
    /// </param>
    /// <returns>Encryption password, or <c>null</c> if the database is not encrypted or the key is unavailable.</returns>
    private static string? GetEncryptionPasswordIfNeeded(string connectionString, out bool encryptedSiblingNoKey, out string? encryptedSiblingPath)
    {
        encryptedSiblingNoKey = false;
        encryptedSiblingPath = null;
        try
        {
            // Extract database path from connection string
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (!File.Exists(dbPath))
            {
                // Target DB doesn't exist yet — but a version upgrade may be about to copy an
                // encrypted sibling (app_v*.db) to this path. If any existing DB in the same
                // directory is encrypted, we must register the interceptor now so that EF
                // migrations can open the newly-copied file after the upgrade copy runs.
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir) && Directory.Exists(dbDir))
                {
                    foreach (var candidate in Directory.GetFiles(dbDir, "app_v*.db"))
                    {
                        try
                        {
                            using var testConn = new SqliteConnection($"Data Source={candidate}");
                            testConn.Open();
                            using var testCmd = testConn.CreateCommand();
                            testCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
                            testCmd.ExecuteScalar();
                            // Opened fine — not encrypted, keep scanning
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
                        {
                            // Found an encrypted sibling — get the key so the interceptor is ready
                            Console.WriteLine($"Found encrypted sibling database {Path.GetFileName(candidate)} — retrieving key for upgrade path");
                            var keychain = OperatingSystem.IsWindows()
                                ? (IKeychainService)new WindowsKeychainService("Nine-Electron")
                                : new LinuxKeychainService("Nine-Electron");
                            var password = keychain.RetrieveKey();
                            if (!string.IsNullOrEmpty(password))
                            {
                                SqliteConnection.ClearAllPools();
                                return password;
                            }
                            // Key not in keychain — signal caller to set NeedsUnlock=true even
                            // though the target DB doesn't exist yet (it will be copied later).
                            // Record the sibling path so the unlock page can verify against it
                            // rather than the target path (which SQLite would create empty).
                            encryptedSiblingNoKey = true;
                            encryptedSiblingPath = candidate;
                            return null;
                        }
                    }
                }
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
                    ? (IKeychainService)new WindowsKeychainService("Nine-Electron")
                    : new LinuxKeychainService("Nine-Electron"); // Pass app name to prevent keychain conflicts
                
                Console.WriteLine("Attempting to retrieve encryption password from keychain...");
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
    /// Gets the connection string for Electron mode using the path service synchronously.
    /// This avoids deadlocks during service registration before Electron is fully initialized.
    /// </summary>
    private static string GetElectronConnectionString(IConfiguration configuration)
    {
        var pathService = new ElectronPathService(configuration);
        var dbPath = pathService.GetDatabasePathSync();
        return $"DataSource={dbPath};Cache=Shared";
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
