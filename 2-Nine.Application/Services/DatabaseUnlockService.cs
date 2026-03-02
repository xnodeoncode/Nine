using Nine.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services;

/// <summary>
/// Service for unlocking encrypted databases by prompting user for password
/// and verifying it against the database
/// </summary>
public class DatabaseUnlockService
{
    private readonly IKeychainService _keychain;
    private readonly ILogger<DatabaseUnlockService> _logger;
    
    public DatabaseUnlockService(
        IKeychainService keychain,
        ILogger<DatabaseUnlockService> logger)
    {
        _keychain = keychain;
        _logger = logger;
    }
    
    /// <summary>
    /// Verify password can decrypt the database and store in keychain
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="password">User-provided password to verify</param>
    /// <returns>(Success, ErrorMessage)</returns>
    public async Task<(bool Success, string? ErrorMessage)> UnlockDatabaseAsync(
        string connectionString, 
        string password)
    {
        try
        {
            _logger.LogInformation("Attempting database unlock verification");
            
            // Try to open database with password
            using var conn = new SqliteConnection($"{connectionString};Password={password}");
            await conn.OpenAsync();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
            
            _logger.LogInformation("Password verification successful");
            
            // Store password in keychain for future use
            var stored = _keychain.StoreKey(password, "Nine Database Encryption Password");
            if (stored)
            {
                _logger.LogInformation("Password stored in keychain successfully");
            }
            else
            {
                _logger.LogWarning("Failed to store password in keychain");
            }
            
            return (true, null);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            _logger.LogWarning("Incorrect password provided");
            return (false, "Incorrect password. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking database");
            return (false, $"Error unlocking database: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Archive encrypted database and create fresh database when password forgotten.
    /// </summary>
    /// <param name="databasePath">Path to encrypted database</param>
    /// <returns>(Success, ArchivedPath, ErrorMessage)</returns>
    public async Task<(bool Success, string? ArchivedPath, string? ErrorMessage)> StartWithNewDatabaseAsync(
        string databasePath)
    {
        try
        {
            _logger.LogWarning("User requested new database - archiving encrypted database");

            // Create backups directory if it doesn't exist
            var dbDirectory = Path.GetDirectoryName(databasePath)!;
            var backupsDir = Path.Combine(dbDirectory, "Backups");
            Directory.CreateDirectory(backupsDir);

            // Generate archived filename with timestamp and .db extension for easy identification
            var dbFileNameWithoutExt = Path.GetFileNameWithoutExtension(databasePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var archivedPath = Path.Combine(backupsDir, $"{dbFileNameWithoutExt}.{timestamp}.encrypted.db");

            // On Windows the OS enforces mandatory file locks. Even after SqliteConnection is
            // disposed, the connection pool keeps the Win32 file handle open until explicitly
            // cleared. Clear all pools and give the GC a chance to release any lingering
            // handles before we attempt the file move.
            SqliteConnection.ClearAllPools();
            if (OperatingSystem.IsWindows())
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            // Move the main database file. Retry once on Windows in case a finalizer
            // hadn't yet released its handle on the first attempt.
            try
            {
                File.Move(databasePath, archivedPath);
            }
            catch (IOException) when (OperatingSystem.IsWindows())
            {
                _logger.LogWarning("File move failed on first attempt (Windows lock), retrying after 500 ms");
                await Task.Delay(500);
                File.Move(databasePath, archivedPath);
            }

            // Remove WAL companion files if present. These are created when WAL mode is active
            // and must be cleaned up so the fresh database starts without a stale journal.
            // On Windows these files may also be locked; delete rather than move since the
            // archived backup doesn't need them.
            foreach (var sidecar in new[] { databasePath + "-wal", databasePath + "-shm" })
            {
                if (!File.Exists(sidecar)) continue;
                try
                {
                    File.Delete(sidecar);
                    _logger.LogInformation("Removed WAL companion file: {Sidecar}", sidecar);
                }
                catch (Exception ex)
                {
                    // Non-fatal: a stale WAL without its main database is harmless.
                    _logger.LogWarning("Could not remove WAL companion file {Sidecar}: {Message}", sidecar, ex.Message);
                }
            }

            _logger.LogInformation("Encrypted database archived to: {ArchivedPath}", archivedPath);

            // New database will be created automatically on app restart — the app detects
            // no database exists and runs through first-time setup / migrations.
            return (true, archivedPath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving encrypted database");
            return (false, null, $"Error archiving database: {ex.Message}");
        }
    }
}
