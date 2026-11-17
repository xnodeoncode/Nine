using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Nine.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time notification updates across browser tabs and devices.
/// Provides instant synchronization of notification state (read/unread/deleted) for users.
/// Broadcasting is handled by NotificationService using IHubContext, not directly through hub methods.
/// </summary>
[AllowAnonymous] // Blazor Server circuits already authenticated - no additional auth needed
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Logs connection for monitoring and debugging.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name;
        _logger.LogDebug($"User {userId} connected to NotificationHub with ConnectionId: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Logs disconnection for monitoring and debugging.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.Name;
        _logger.LogDebug($"User {userId} disconnected from NotificationHub. ConnectionId: {Context.ConnectionId}");
        if (exception != null)
        {
            _logger.LogError(exception, $"User {userId} disconnected with error");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
