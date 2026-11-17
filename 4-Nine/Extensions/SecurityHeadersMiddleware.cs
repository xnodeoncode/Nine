using Microsoft.Extensions.Primitives;

namespace Nine.Extensions;

/// <summary>
/// Middleware that adds security headers including Content Security Policy (CSP)
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly bool _isElectron;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _isElectron = ElectronNET.API.HybridSupport.IsElectronActive;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip CSP for Electron (desktop app doesn't need browser security restrictions)
        if (!_isElectron)
        {
            AddSecurityHeaders(context);
        }

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Content Security Policy (CSP)
        // Note: Blazor Server requires 'unsafe-inline' and 'unsafe-eval' for scripts
        var csp = new List<string>
        {
            "default-src 'self'",
            
            // Scripts: Allow self, inline scripts (Blazor requirement), and eval (Blazor SignalR requirement)
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
            
            // Styles: Allow self and inline styles (Bootstrap/Blazor requirement)
            "style-src 'self' 'unsafe-inline'",
            
            // Fonts: Allow self and data URIs (Bootstrap Icons are served locally)
            "font-src 'self' data:",
            
            // Images: Allow self, data URIs (for inline images), and blob URIs (for PDF viewer)
            "img-src 'self' data: blob:",
            
            // Connect: Allow self for Blazor SignalR WebSocket connections
            "connect-src 'self'",
            
            // Frames: Prevent clickjacking by not allowing this site to be framed
            "frame-ancestors 'none'",
            
            // Base: Restrict base tag to prevent base tag hijacking
            "base-uri 'self'",
            
            // Forms: Only allow form submissions to same origin
            "form-action 'self'",
            
            // Objects: Block all plugins (Flash, Java, etc.)
            "object-src 'none'",
            
            // Media: Allow self for audio/video
            "media-src 'self'",
            
            // Workers: Allow self for web workers
            "worker-src 'self' blob:",
            
            // Child frames: Don't allow embedding iframes
            "child-src 'none'",
            
            // Manifests: Allow self for PWA manifest
            "manifest-src 'self'"
        };

        var cspHeader = string.Join("; ", csp);
        headers["Content-Security-Policy"] = new StringValues(cspHeader);

        // X-Content-Type-Options: Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = new StringValues("nosniff");

        // X-Frame-Options: Prevent clickjacking (defense in depth with CSP frame-ancestors)
        headers["X-Frame-Options"] = new StringValues("DENY");

        // X-XSS-Protection: Enable browser XSS filtering (legacy browsers)
        headers["X-XSS-Protection"] = new StringValues("1; mode=block");

        // Referrer-Policy: Control referrer information
        headers["Referrer-Policy"] = new StringValues("strict-origin-when-cross-origin");

        // Permissions-Policy: Restrict browser features
        var permissionsPolicy = new List<string>
        {
            "accelerometer=()",
            "camera=()",
            "geolocation=()",
            "gyroscope=()",
            "magnetometer=()",
            "microphone=()",
            "payment=()",
            "usb=()"
        };
        headers["Permissions-Policy"] = new StringValues(string.Join(", ", permissionsPolicy));

        _logger.LogDebug("Security headers added to response");
    }
}

/// <summary>
/// Extension methods for registering SecurityHeadersMiddleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
