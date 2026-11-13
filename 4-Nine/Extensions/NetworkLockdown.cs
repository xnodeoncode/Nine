using ElectronNET.API;
using ElectronNET.API.Entities;

namespace Nine.Extensions;

/// <summary>
/// Restricts the desktop (Electron) build to talking only to its own local Kestrel instance.
/// The port is chosen at startup, so everything here is derived from the URL Kestrel actually bound to.
/// </summary>
public static class NetworkLockdown
{
    private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1", "[::1]" };

    // Port 9 (discard) on loopback is not served, so any request routed through it fails immediately.
    private const string BlackholeProxy = "127.0.0.1:9";

    /// <summary>
    /// Kestrel rejects requests whose Host header is not a loopback name (blocks DNS-rebinding style access).
    /// The host filter ignores the port, so the dynamic port needs no special handling.
    /// Must be called before the host is built.
    /// </summary>
    public static void RestrictHostsToLoopback(IConfigurationManager configuration)
    {
        configuration["AllowedHosts"] = string.Join(';', LoopbackHosts);
    }

    /// <summary>
    /// Sends every Electron network request that is not to a loopback address into a dead proxy,
    /// and returns the window to the app if it tries to navigate to a different origin.
    /// </summary>
    public static async Task ApplyAsync(BrowserWindow window, string backendUrl, ILogger logger)
    {
        var session = window.WebContents.Session;
        await session.SetProxyAsync(new ProxyConfig(
            pacScript: null,
            proxyRules: BlackholeProxy,
            proxyBypassRules: string.Join(';', LoopbackHosts)));

        window.WebContents.OnDidStartNavigation += url =>
        {
            if (IsExternalWebUrl(url, backendUrl))
            {
                logger.LogWarning("Blocked navigation to external URL: {Url}", url);
                _ = window.WebContents.LoadURLAsync(backendUrl);
            }
        };

        logger.LogInformation("Network lockdown applied; only {Url} is reachable", backendUrl);
    }

    /// <summary>
    /// True for http(s) URLs whose scheme, host and port differ from the application URL.
    /// Non-web schemes (about:, blob:, data:, chrome-error:) are not treated as external here.
    /// </summary>
    public static bool IsExternalWebUrl(string? url, string backendUrl)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target) ||
            !Uri.TryCreate(backendUrl, UriKind.Absolute, out var app))
        {
            return false;
        }

        if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return !(target.Scheme == app.Scheme &&
                 string.Equals(target.Host, app.Host, StringComparison.OrdinalIgnoreCase) &&
                 target.Port == app.Port);
    }
}
