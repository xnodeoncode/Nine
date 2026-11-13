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
    /// Sends every Electron network request that is not to a loopback address into a dead proxy.
    /// When the main window, or a window it opens, tries to go to another origin, the http(s) URL is
    /// handed to the system browser (a user-initiated action outside the app) and the app stays put.
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
            if (!IsExternalWebUrl(url, backendUrl))
            {
                return;
            }

            logger.LogInformation("Opening external URL in the system browser: {Url}", url);
            _ = Electron.Shell.OpenExternalAsync(url);
            _ = window.WebContents.LoadURLAsync(backendUrl);
        };

        logger.LogInformation("Network lockdown applied; only {Url} is reachable from the app", backendUrl);
    }

    /// <summary>
    /// Endpoint used by externalLinks.js to hand a clicked http(s) link to the system browser.
    /// Links with target="_blank" would otherwise open a new Electron window that cannot reach the network.
    /// Only active in the Electron build; elsewhere it returns 404 and the script falls back to normal behaviour.
    /// </summary>
    public static void MapExternalLinkEndpoint(WebApplication app)
    {
        app.MapPost("/api/external/open", async (HttpContext context, OpenExternalRequest request) =>
        {
            if (!HybridSupport.IsElectronActive)
            {
                return Results.NotFound();
            }

            // Only requests made by the app's own pages (not other sites or local tools).
            if (context.Request.Headers["Sec-Fetch-Site"] != "same-origin")
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target) ||
                (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
            {
                return Results.BadRequest();
            }

            app.Logger.LogInformation("Opening external URL in the system browser: {Url}", target);
            var error = await Electron.Shell.OpenExternalAsync(target.AbsoluteUri);
            return string.IsNullOrEmpty(error) ? Results.NoContent() : Results.Problem(error);
        });
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

public sealed record OpenExternalRequest(string? Url);
