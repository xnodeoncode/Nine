using Nine.Infrastructure.Data;
using Nine.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using ElectronNET.API;

namespace Nine.Shared.Services
{
    /// <summary>
    /// Service for managing database backups and recovery operations.
    /// Provides automatic backups before migrations, manual backup capability,
    /// and recovery from corrupted databases.
    /// </summary>
    public class DatabaseBackupService
    {
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IPathService _pathService;

        public DatabaseBackupService(
            ILogger<DatabaseBackupService> logger,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            IPathService pathService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
            _pathService = pathService;
        }

        /// <summary>
        /// Creates a backup of the SQLite database file
        /// </summary>
        /// <param name="backupReason">Reason for backup (e.g., "Manual", "Pre-Migration", "Scheduled")</param>
        /// <returns>Path to the backup file, or null if backup failed</returns>
        public async Task<string?> CreateBackupAsync(string backupReason = "Manual")
        {
            try
            {
                var dbPath = await GetDatabasePathAsync();
                _logger.LogInformation("Attempting to create backup of database at: {DbPath}", dbPath);
                
                if (!File.Exists(dbPath))
                {
                    _logger.LogWarning("Database file not found at {DbPath}, skipping backup", dbPath);
                    return null;
                }

                var backupDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");
                _logger.LogInformation("Creating backup directory: {BackupDir}", backupDir);
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"Nine_Backup_{backupReason}_{timestamp}.db";
                var backupPath = Path.Combine(backupDir, backupFileName);

                _logger.LogInformation("Backup will be created at: {BackupPath}", backupPath);

                // Force WAL checkpoint to flush all data from WAL file into main database file
                try
                {
                    var connection = _dbContext.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        await command.ExecuteNonQueryAsync();
                        _logger.LogInformation("WAL checkpoint completed - all data flushed to main database file");
                    }
                    await connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to checkpoint WAL before backup");
                }

                // Try to close any open connections before backup
                try
                {
                    await _dbContext.Database.CloseConnectionAsync();
                    _logger.LogInformation("Database connection closed successfully");
                }
                catch (Exception closeEx)
                {
                    _logger.LogWarning(closeEx, "Error closing database connection, continuing anyway");
                }

                // Small delay to ensure file handles are released
                await Task.Delay(100);

                // Copy the database file with retry logic
                int retries = 3;
                bool copied = false;
                Exception? lastException = null;
                
                for (int i = 0; i < retries && !copied; i++)
                {
                    try
                    {
                        File.Copy(dbPath, backupPath, overwrite: false);
                        copied = true;
                        _logger.LogInformation("Database file copied successfully on attempt {Attempt}", i + 1);
                    }
                    catch (IOException ioEx) when (i < retries - 1)
                    {
                        lastException = ioEx;
                        _logger.LogWarning("File copy attempt {Attempt} failed, retrying after delay: {Error}", 
                            i + 1, ioEx.Message);
                        await Task.Delay(500); // Wait before retry
                    }
                }

                if (!copied)
                {
                    throw new IOException($"Failed to copy database file after {retries} attempts", lastException);
                }

                _logger.LogInformation("Database backup created successfully: {BackupPath} (Reason: {Reason})", 
                    backupPath, backupReason);

                // Clean up old backups (keep last 10)
                await CleanupOldBackupsAsync(backupDir, keepCount: 10);

                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create database backup. Error: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Validates database integrity by attempting to open a connection and run a simple query
        /// </summary>
        /// <returns>True if database is healthy, false if corrupted</returns>
        public async Task<(bool IsHealthy, string Message)> ValidateDatabaseHealthAsync()
        {
            try
            {
                // Try to open connection
                await _dbContext.Database.OpenConnectionAsync();

                // SQLite-specific integrity check
                var connection = _dbContext.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";
                
                var result = await command.ExecuteScalarAsync();
                var integrityResult = result?.ToString() ?? "unknown";

                if (integrityResult.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Database integrity check passed");
                    return (true, "Database is healthy");
                }
                else
                {
                    _logger.LogWarning("Database integrity check failed: {Result}", integrityResult);
                    return (false, $"Integrity check failed: {integrityResult}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return (false, $"Health check error: {ex.Message}");
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }

        /// <summary>
        /// Restores database from a backup file
        /// </summary>
        /// <param name="backupPath">Path to the backup file to restore</param>
        /// <returns>True if restore was successful</returns>
        public async Task<bool> RestoreFromBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                    return false;
                }

                var dbPath = await GetDatabasePathAsync();

                // Close all connections and clear connection pool
                await _dbContext.Database.CloseConnectionAsync();
                _dbContext.Dispose();
                
                // Clear SQLite connection pool to release file locks
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                
                // Give the system a moment to release file locks
                await Task.Delay(100);

                // Create a backup of current database before restoring (with unique filename)
                // Use milliseconds and a counter to ensure uniqueness
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                var corruptedBackupPath = $"{dbPath}.corrupted.{timestamp}";
                
                // If file still exists (very rare), add a counter
                int counter = 1;
                while (File.Exists(corruptedBackupPath))
                {
                    corruptedBackupPath = $"{dbPath}.corrupted.{timestamp}.{counter}";
                    counter++;
                }
                
                if (File.Exists(dbPath))
                {
                    // Move the current database to the corrupted backup path
                    File.Move(dbPath, corruptedBackupPath);
                    _logger.LogInformation("Current database moved to: {CorruptedPath}", corruptedBackupPath);
                }

                // Restore from backup (now the original path is free)
                File.Copy(backupPath, dbPath, overwrite: true);

                _logger.LogInformation("Database restored from backup: {BackupPath}", backupPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore database from backup");
                return false;
            }
        }

        /// <summary>
        /// Lists all available backup files
        /// </summary>
        public async Task<List<BackupInfo>> GetAvailableBackupsAsync()
        {
            try
            {
                var dbPath = await GetDatabasePathAsync();
                var backupDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");

                if (!Directory.Exists(backupDir))
                {
                    return new List<BackupInfo>();
                }

                var backupFiles = Directory.GetFiles(backupDir, "*.db")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Select(f => new BackupInfo
                    {
                        FilePath = f,
                        FileName = Path.GetFileName(f),
                        CreatedDate = File.GetCreationTime(f),
                        SizeBytes = new FileInfo(f).Length,
                        SizeFormatted = FormatFileSize(new FileInfo(f).Length)
                    })
                    .ToList();

                return backupFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list backup files");
                return new List<BackupInfo>();
            }
        }

        /// <summary>
        /// Attempts to recover from a corrupted database by finding the most recent valid backup
        /// </summary>
        public async Task<(bool Success, string Message)> AutoRecoverFromCorruptionAsync()
        {
            try
            {
                _logger.LogWarning("Attempting automatic recovery from database corruption");

                var backups = await GetAvailableBackupsAsync();
                if (!backups.Any())
                {
                    return (false, "No backup files available for recovery");
                }

                // Try each backup starting with the most recent
                foreach (var backup in backups)
                {
                    _logger.LogInformation("Attempting to restore from backup: {FileName}", backup.FileName);
                    
                    var restored = await RestoreFromBackupAsync(backup.FilePath);
                    if (restored)
                    {
                        return (true, $"Successfully recovered from backup: {backup.FileName}");
                    }
                }

                return (false, "All backup restoration attempts failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-recovery failed");
                return (false, $"Auto-recovery error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a backup before applying migrations (called from Program.cs)
        /// </summary>
        public async Task<string?> CreatePreMigrationBackupAsync()
        {
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
            if (!pendingMigrations.Any())
            {
                _logger.LogInformation("No pending migrations, skipping backup");
                return null;
            }

            var migrationsCount = pendingMigrations.Count();
            var backupReason = $"PreMigration_{migrationsCount}Pending";
            
            return await CreateBackupAsync(backupReason);
        }

        /// <summary>
        /// Gets the database file path for both Electron and web modes
        /// </summary>
        public async Task<string> GetDatabasePathAsync()
        {
            if (HybridSupport.IsElectronActive)
            {
                return await _pathService.GetDatabasePathAsync();
            }
            else
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
                }
                
                // Parse SQLite connection string - supports both "Data Source=" and "DataSource="
                var dbPath = connectionString
                    .Replace("Data Source=", "")
                    .Replace("DataSource=", "")
                    .Split(';')[0]
                    .Trim();
                
                // Make absolute path if relative
                if (!Path.IsPathRooted(dbPath))
                {
                    dbPath = Path.Combine(Directory.GetCurrentDirectory(), dbPath);
                }
                
                _logger.LogInformation("Database path resolved to: {DbPath}", dbPath);
                return dbPath;
            }
        }

        private async Task CleanupOldBackupsAsync(string backupDir, int keepCount)
        {
            await Task.Run(() =>
            {
                try
                {
                    var backupFiles = Directory.GetFiles(backupDir, "*.db")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(keepCount)
                        .ToList();

                    foreach (var file in backupFiles)
                    {
                        file.Delete();
                        _logger.LogInformation("Deleted old backup: {FileName}", file.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup old backups");
                }
            });
        }

        /// <summary>
        /// Prepares the database for file operations by checkpointing WAL, closing connections, and clearing pools.
        /// Call this before attempting to copy or move the live database file.
        /// </summary>
        /// <param name="connectionString">Optional connection string if database is encrypted (include password)</param>
        public async Task PrepareForFileCopyAsync(string? connectionString = null)
        {
            try
            {
                // Step 1: Checkpoint WAL to consolidate all data into main database file
                _logger.LogInformation("Checkpointing WAL before file operation");
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Use DbContext connection for plaintext database
                    var connection = _dbContext.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        await command.ExecuteNonQueryAsync();
                    }
                    await connection.CloseAsync();
                }
                else
                {
                    // Use provided connection string for encrypted database
                    using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                
                _logger.LogInformation("WAL checkpoint completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WAL checkpoint failed, continuing anyway");
            }

            // Step 2: Close DbContext connection
            try
            {
                await _dbContext.Database.CloseConnectionAsync();
                _logger.LogInformation("Database connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing database connection");
            }

            // Step 3: Clear all SQLite connection pools to release file locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            _logger.LogInformation("Connection pools cleared");

            // Step 4: Wait for file system to release locks (increased from 100ms for encryption reliability)
            await Task.Delay(500);
            _logger.LogInformation("Database prepared for file operations");
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class BackupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; } = string.Empty;
    }
}
