using System.Runtime.InteropServices;
using System.Text;
using Nine.Infrastructure.Interfaces;

namespace Nine.Infrastructure.Services;

/// <summary>
/// Service for storing and retrieving encryption keys from Linux Secret Service (libsecret).
/// Provides convenient auto-decryption on trusted devices.
/// </summary>
public class LinuxKeychainService : IKeychainService
{
    private const string Schema = "co.nineapp.database";
    private const string KeyAttribute = "key-type";
    private readonly string _keyValue;
    
    /// <summary>
    /// Initialize keychain service with app-specific identifier
    /// </summary>
    /// <param name="appName">Application name (e.g., "Nine-Web", "Nine-Electron", "Professional-Web") to prevent keychain conflicts</param>
    public LinuxKeychainService(string appName = "Nine-Electron")
    {
        // Make keychain entry unique per application to prevent password conflicts
        _keyValue = $"database-encryption-{appName}";
        Console.WriteLine($"[LinuxKeychainService] Initialized with key attribute value: {_keyValue}");
    }
    
    /// <summary>
    /// Store encryption key in Linux keychain (libsecret)
    /// </summary>
    /// <param name="keyHex">Hex-encoded encryption key</param>
    /// <param name="label">Human-readable label for the key</param>
    /// <returns>True if stored successfully</returns>
    public bool StoreKey(string keyHex, string label = "Nine Database Encryption Key")
    {
        if (!OperatingSystem.IsLinux())
            return false;
        
        try
        {
            // Use secret-tool command line utility (part of libsecret)
            // This is more reliable than P/Invoke for cross-distribution compatibility
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"store --label=\"{label}\" {KeyAttribute} {_keyValue}",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.StandardInput.WriteLine(keyHex);
            process.StandardInput.Close();
            process.WaitForExit(5000);
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to store key in keychain: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Retrieve encryption key from Linux keychain
    /// </summary>
    /// <returns>Hex-encoded encryption key, or null if not found</returns>
    public string? RetrieveKey()
    {
        if (!OperatingSystem.IsLinux())
            return null;
        
        try
        {
            Console.WriteLine($"[LinuxKeychainService] Retrieving key with attribute value: {_keyValue}");
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"lookup {KeyAttribute} {_keyValue}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            
            // Read both stdout and stderr to prevent deadlocks
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            process.WaitForExit(5000);
            
            Console.WriteLine($"[LinuxKeychainService] secret-tool exit code: {process.ExitCode}");
            Console.WriteLine($"[LinuxKeychainService] secret-tool output: '{output}'");
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[LinuxKeychainService] secret-tool error: {error}");
            }
            
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to retrieve key from keychain: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Remove encryption key from Linux keychain
    /// </summary>
    /// <returns>True if removed successfully</returns>
    public bool RemoveKey()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"clear {KeyAttribute} {_keyValue}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.WaitForExit(5000);
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to remove key from keychain: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check if secret-tool is available on the system
    /// </summary>
    public bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "secret-tool",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.WaitForExit(2000);
            
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
