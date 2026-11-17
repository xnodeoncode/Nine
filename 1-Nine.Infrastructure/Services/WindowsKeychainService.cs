using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nine.Infrastructure.Interfaces;

namespace Nine.Infrastructure.Services;

/// <summary>
/// Secure key storage for Windows using DPAPI (Data Protection API).
/// Encrypts the database password with the current user's Windows credentials
/// and stores it in a file under %APPDATA%\Nine\. Only the same user on the
/// same machine can decrypt the file — no user interaction needed on retrieval.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsKeychainService : IKeychainService
{
    private readonly string _keyFilePath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialize the Windows DPAPI keychain service.
    /// </summary>
    /// <param name="appName">App-specific identifier to prevent key conflicts (e.g. "Nine-Electron")</param>
    /// <param name="logger">Optional logger for routine diagnostics (written at Debug level). Omit during startup, before DI is available.</param>
    public WindowsKeychainService(string appName = "Nine-Electron", ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var nineDir = Path.Combine(appDataPath, "Nine");
        Directory.CreateDirectory(nineDir);

        // Sanitize appName for use as a filename component
        var safeAppName = new string(appName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        _keyFilePath = Path.Combine(nineDir, $"aquiis_{safeAppName}.key");

        _logger.LogDebug("Keychain initialized with key file {KeyFilePath}", _keyFilePath);
    }

    /// <summary>
    /// Store the encryption password using DPAPI. The data is encrypted with the
    /// current user's credentials and written to a binary key file.
    /// </summary>
    public bool StoreKey(string password, string label = "Nine Database Encryption Key")
    {
        try
        {
            _logger.LogDebug("Storing password using DPAPI");
            var plainBytes = Encoding.UTF8.GetBytes(password);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_keyFilePath, encryptedBytes);
            _logger.LogDebug("Password stored successfully using DPAPI");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowsKeychainService] Failed to store password: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieve the encryption password by decrypting the key file with DPAPI.
    /// Returns null if the file does not exist or cannot be decrypted.
    /// </summary>
    public string? RetrieveKey()
    {
        if (!File.Exists(_keyFilePath))
        {
            Console.WriteLine("[WindowsKeychainService] Key file not found");
            return null;
        }

        try
        {
            _logger.LogDebug("Retrieving password using DPAPI");
            var encryptedBytes = File.ReadAllBytes(_keyFilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var password = Encoding.UTF8.GetString(plainBytes);
            _logger.LogDebug("Password retrieved using DPAPI. Password status: {Status}", string.IsNullOrEmpty(password) ? "empty" : "received");
            return password;
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"[WindowsKeychainService] Failed to decrypt password (DPAPI): {ex.Message}");
            Console.WriteLine("[WindowsKeychainService] This usually means the key file was encrypted by a different user or machine");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowsKeychainService] Failed to retrieve password: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete the key file, effectively removing the stored password.
    /// </summary>
    public bool RemoveKey()
    {
        try
        {
            if (File.Exists(_keyFilePath))
            {
                File.Delete(_keyFilePath);
                _logger.LogDebug("Key file deleted successfully");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowsKeychainService] Failed to remove key file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// DPAPI is always available on Windows.
    /// </summary>
    public bool IsAvailable() => OperatingSystem.IsWindows();
}
