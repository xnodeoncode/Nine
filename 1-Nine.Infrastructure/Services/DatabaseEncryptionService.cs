using Nine.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Nine.Infrastructure.Services;

/// <summary>
/// Service for encrypting and decrypting SQLite databases using SQLCipher.
/// Handles the conversion between encrypted and unencrypted database files.
/// </summary>
public class DatabaseEncryptionService
{
    private readonly PasswordDerivationService _passwordDerivation;
    private readonly IKeychainService _keychain;
    private readonly ILogger<DatabaseEncryptionService> _logger;
    
    public DatabaseEncryptionService(
        PasswordDerivationService passwordDerivation,
        IKeychainService keychain,
        ILogger<DatabaseEncryptionService> logger)
    {
        _passwordDerivation = passwordDerivation;
        _keychain = keychain;
        _logger = logger;
    }
    
    /// <summary>
    /// Encrypt an unencrypted database file
    /// </summary>
    /// <param name="sourcePath">Path to unencrypted database</param>
    /// <param name="password">User's master password (passed directly to SQLCipher)</param>
    /// <returns>(Success, EncryptedPath, ErrorMessage)</returns>
    public async Task<(bool Success, string? EncryptedPath, string? ErrorMessage)> 
        EncryptDatabaseAsync(string sourcePath, string password)
    {
        try
        {
            _logger.LogInformation("Starting database encryption for {Path}", sourcePath);
            
            // Validate password
            var (isValid, validationError) = _passwordDerivation.ValidatePasswordStrength(password);
            if (!isValid)
            {
                return (false, null, validationError);
            }
            
            // Create temporary encrypted database path
            var encryptedPath = $"{sourcePath}.encrypted";
            
            // Delete if exists from previous attempt
            if (File.Exists(encryptedPath))
            {
                File.Delete(encryptedPath);
            }
            
            // Initialize SQLCipher
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_initialize();
            
            // CRITICAL: Clear connection pools to reset any cached cipher state
            SqliteConnection.ClearAllPools();
            _logger.LogInformation("Connection pools cleared before encryption");
            
            // Attach and copy database using SQLCipher
            using (var sourceConn = new SqliteConnection($"Data Source={sourcePath}"))
            {
                await sourceConn.OpenAsync();
                _logger.LogInformation("Source database opened successfully");
                
                // Attach encrypted database with password (SQLCipher handles PBKDF2 internally)
                using (var cmd = sourceConn.CreateCommand())
                {
                    cmd.CommandText = $"ATTACH DATABASE '{encryptedPath}' AS encrypted KEY '{password}';";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Encrypted database attached");
                }
                
                // Set SQLCipher 4 parameters for the attached database
                using (var cmd = sourceConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        PRAGMA encrypted.cipher_page_size = 4096;
                        PRAGMA encrypted.kdf_iter = 256000;
                        PRAGMA encrypted.cipher_hmac_algorithm = HMAC_SHA512;
                        PRAGMA encrypted.cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("SQLCipher parameters set");
                }
                
                // Export schema and data to encrypted database
                using (var cmd = sourceConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT sqlcipher_export('encrypted');";
                    var result = await cmd.ExecuteScalarAsync();
                    _logger.LogInformation("SQLCipher export completed: {Result}", result);
                }
                
                // Detach encrypted database
                using (var cmd = sourceConn.CreateCommand())
                {
                    cmd.CommandText = "DETACH DATABASE encrypted;";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Encrypted database detached");
                }
            }
            
            _logger.LogInformation("Waiting for file system to settle after encryption");
            await Task.Delay(200);
            
            // Verify encrypted database can be opened
            var verifySuccess = await VerifyEncryptedDatabaseAsync(encryptedPath, password);
            if (!verifySuccess)
            {
                if (File.Exists(encryptedPath))
                {
                    File.Delete(encryptedPath);
                }
                return (false, null, "Failed to verify encrypted database");
            }
            
            // Clear pools so Windows releases the verified file handle before the caller moves the file
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            // Store password in keychain (best effort - don't fail if keychain unavailable)
            var stored = _keychain.StoreKey(password, "Nine Database Encryption Password");
            if (!stored)
            {
                _logger.LogWarning("Failed to store password in keychain - you'll need to enter it manually on next startup");
            }
            else
            {
                _logger.LogInformation("Password stored in keychain successfully");
            }
            
            _logger.LogInformation("Database encryption completed successfully");
            return (true, encryptedPath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt database");
            return (false, null, $"Encryption failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Decrypt an encrypted database file
    /// </summary>
    /// <param name="encryptedPath">Path to encrypted database</param>
    /// <param name="password">User's master password</param>
    /// <param name="removeKeychainEntry">Whether to remove the keychain entry after decryption. Set false when decrypting for backup export.</param>
    /// <returns>(Success, DecryptedPath, ErrorMessage)</returns>
    public async Task<(bool Success, string? DecryptedPath, string? ErrorMessage)> 
        DecryptDatabaseAsync(string encryptedPath, string password, bool removeKeychainEntry = true)
    {
        try
        {
            _logger.LogInformation("Starting database decryption for {Path}", encryptedPath);
            
            // Create temporary decrypted database path
            var decryptedPath = $"{encryptedPath}.decrypted";
            
            // Delete if exists from previous attempt
            if (File.Exists(decryptedPath))
            {
                File.Delete(decryptedPath);
                _logger.LogInformation("Deleted existing decrypted database at {Path}", decryptedPath);
            }
            
            // Initialize SQLCipher
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_initialize();
            
            // CRITICAL: Clear connection pools to reset any cached cipher state from interceptor
            // This is especially important in Electron where the interceptor may have initialized
            // SQLCipher with different parameters for the main database
            SqliteConnection.ClearAllPools();
            _logger.LogInformation("Connection pools cleared before decryption");
            
            // Open encrypted database and export to unencrypted
            _logger.LogInformation("Creating SqliteConnection for encrypted database: {Path}", encryptedPath);
            using (var encryptedConn = new SqliteConnection($"Data Source={encryptedPath}"))
            {
                _logger.LogInformation("SqliteConnection object created, calling OpenAsync()...");
                _logger.LogInformation("Password length: {Length} characters", password.Length);
                await encryptedConn.OpenAsync();
                _logger.LogInformation("✅ Encrypted database opened successfully");
                
                // Set password using PRAGMA — must be first operation on the connection
                using (var cmd = encryptedConn.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA key = '{password}';";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Encryption key set with PRAGMA");
                }

                // Explicitly set SQLCipher 4 parameters to match how the database was created.
                // On Windows the native library does not always auto-detect these from the file
                // header, causing decryption to silently fail with wrong defaults.
                using (var cmd = encryptedConn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA kdf_iter = 256000;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogInformation("SQLCipher 4 cipher parameters set explicitly");
                }
                
                // Attach unencrypted database
                using (var cmd = encryptedConn.CreateCommand())
                {
                    cmd.CommandText = $"ATTACH DATABASE '{decryptedPath}' AS plaintext KEY '';";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Plaintext database attached");
                }
                
                // Export schema and data to plaintext database
                using (var cmd = encryptedConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT sqlcipher_export('plaintext');";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("SQLCipher export to plaintext completed");
                }
                
                // Detach plaintext database
                using (var cmd = encryptedConn.CreateCommand())
                {
                    cmd.CommandText = "DETACH DATABASE plaintext;";
                    
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Plaintext database detached");
                }
            }
            
            // Verify decrypted database can be opened
            var verifySuccess = await VerifyPlaintextDatabaseAsync(decryptedPath);
            if (!verifySuccess)
            {
                if (File.Exists(decryptedPath))
                {
                    File.Delete(decryptedPath);
                    _logger.LogWarning("Deleted existing decrypted database at {Path}", decryptedPath);
                }
                return (false, null, "Failed to verify decrypted database");
            }
            
            // Clear pools so Windows releases the verified file handle before the caller moves the file
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            // Remove password from keychain only when permanently disabling encryption
            if (removeKeychainEntry)
                _keychain.RemoveKey();
            
            _logger.LogInformation("Database decryption completed successfully");
            return (true, decryptedPath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt database");
            return (false, null, $"Decryption failed: {ex.Message} Inner exception:{ex.InnerException?.Message}");
        }
    }
    
    /// <summary>
    /// Verify encrypted database can be opened with the provided password
    /// </summary>
    private async Task<bool> VerifyEncryptedDatabaseAsync(string dbPath, string password)
    {
        try
        {
            _logger.LogInformation("Verifying encrypted database at {Path}", dbPath);
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();
                
                // Set password then explicit SQLCipher 4 params — mirrors DecryptDatabaseAsync
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA key = '{password}';";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA kdf_iter = 256000;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();
                }
                
                _logger.LogInformation("Encrypted database opened successfully");
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                    var result = await cmd.ExecuteScalarAsync();
                    _logger.LogInformation("Query executed successfully, result: {Result}", result);
                    return result != null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify encrypted database");
            return false;
        }
    }
    
    /// <summary>
    /// Verify plaintext database can be opened
    /// </summary>
    private async Task<bool> VerifyPlaintextDatabaseAsync(string dbPath)
    {
        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                    var result = await cmd.ExecuteScalarAsync();
                    return result != null;
                }
            }
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Try to retrieve encryption key from keychain
    /// </summary>
    public string? TryGetKeyFromKeychain() => _keychain.RetrieveKey();
    
    /// <summary>
    /// Check if keychain service is available
    /// </summary>
    public bool IsKeychainAvailable() => _keychain.IsAvailable();

    /// <summary>
    /// Change the encryption password of an already-encrypted database using PRAGMA rekey.
    /// Verifies the current password before applying the change, then updates the OS keychain.
    /// </summary>
    /// <param name="dbPath">Absolute path to the encrypted database file</param>
    /// <param name="currentPassword">Current master password</param>
    /// <param name="newPassword">New master password (minimum 12 characters)</param>
    /// <returns>(Success, ErrorMessage)</returns>
    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
        string dbPath, string currentPassword, string newPassword)
    {
        try
        {
            _logger.LogInformation("Starting password change for {Path}", dbPath);

            // Validate new password strength
            var (isValid, validationError) = _passwordDerivation.ValidatePasswordStrength(newPassword);
            if (!isValid)
                return (false, validationError);

            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_initialize();
            SqliteConnection.ClearAllPools();

            // Step 1 — open with current password and verify
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA key = '{currentPassword}';";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "PRAGMA cipher_page_size = 4096;";
                    await cmd.ExecuteNonQueryAsync();
                    cmd.CommandText = "PRAGMA kdf_iter = 256000;";
                    await cmd.ExecuteNonQueryAsync();
                    cmd.CommandText = "PRAGMA cipher_hmac_algorithm = HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();
                    cmd.CommandText = "PRAGMA cipher_kdf_algorithm = PBKDF2_HMAC_SHA512;";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Verify current password is correct
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                        await cmd.ExecuteScalarAsync();
                    }
                }
                catch
                {
                    return (false, "Current password is incorrect.");
                }

                // Step 2 — rekey in-place
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA rekey = '{newPassword}';";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("PRAGMA rekey executed");
                }
            }

            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            // Step 3 — verify new password opens the database
            var verifySuccess = await VerifyEncryptedDatabaseAsync(dbPath, newPassword);
            if (!verifySuccess)
                return (false, "Password change completed but verification with the new password failed. " +
                               "The original password may still be active — please try again.");

            // Step 4 — update keychain
            var stored = _keychain.StoreKey(newPassword, "Nine Database Encryption Password");
            if (!stored)
                _logger.LogWarning("Password changed successfully but keychain update failed. " +
                                   "You will be prompted for the new password on next startup.");

            _logger.LogInformation("Database password changed successfully");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change database password");
            return (false, $"Password change failed: {ex.Message}");
        }
    }
}
