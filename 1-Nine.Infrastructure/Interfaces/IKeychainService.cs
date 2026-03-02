namespace Nine.Infrastructure.Interfaces;

/// <summary>
/// Abstraction for platform-specific secure key storage.
/// Linux: uses libsecret (secret-tool). Windows: uses DPAPI encrypted file.
/// </summary>
public interface IKeychainService
{
    /// <summary>Store a password/key in the platform keychain</summary>
    bool StoreKey(string password, string label = "Nine Database Encryption Key");

    /// <summary>Retrieve the stored password/key, or null if not found</summary>
    string? RetrieveKey();

    /// <summary>Remove the stored password/key</summary>
    bool RemoveKey();

    /// <summary>Check if the keychain service is available on this platform</summary>
    bool IsAvailable();
}
