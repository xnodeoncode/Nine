
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using Nine.Infrastructure.Hubs;

namespace Nine.Application.Services;
public class NotificationService : BaseService<Notification>
{
    private readonly IEmailService _emailService;
    private readonly ISMSService _smsService;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(
        ApplicationDbContext context,
        IUserContextService userContext,
        IEmailService emailService,
        ISMSService smsService,
        IOptions<ApplicationSettings> appSettings,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
        : base(context, logger, userContext, appSettings)
    {
        _emailService = emailService;
        _smsService = smsService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Create and send a notification to a user
    /// </summary>
    public async Task<Notification> SendNotificationAsync(
        string recipientUserId,
        string title,
        string message,
        string type,
        string category,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? organizationId = null,
        string? senderUserId = null)
    {
        organizationId ??= await _userContext.GetActiveOrganizationIdAsync();

        // Get user preferences
        var preferences = await GetNotificationPreferencesAsync(recipientUserId, organizationId);

        // Resolve the sender: explicit caller-provided ID (e.g. SystemUser for background jobs),
        // otherwise require an authenticated user.
        string resolvedSenderUserId;
        if (!string.IsNullOrEmpty(senderUserId))
        {
            resolvedSenderUserId = senderUserId;
        }
        else
        {
            var authenticatedUserId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(authenticatedUserId))
                throw new UnauthorizedAccessException("User is not authenticated.");
            resolvedSenderUserId = authenticatedUserId;
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId!.Value,
            RecipientUserId = recipientUserId,
            Title = title,
            Message = message,
            Type = type,
            Category = category,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            SentOn = DateTime.UtcNow,
            IsRead = false,
            SendInApp = preferences.EnableInAppNotifications,
            SendEmail = preferences.EnableEmailNotifications && ShouldSendEmail(category, preferences),
            SendSMS = preferences.EnableSMSNotifications && ShouldSendSMS(category, preferences),
            CreatedBy = resolvedSenderUserId
        };

        // Save in-app notification
        await CreateAsync(notification);

        // Send email if enabled
        if (notification.SendEmail && !string.IsNullOrEmpty(preferences.EmailAddress))
        {
            try
            {
                await _emailService.SendEmailAsync(
                    preferences.EmailAddress,
                    title,
                    message);

                notification.EmailSent = true;
                notification.EmailSentOn = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email notification to {recipientUserId}");
                notification.EmailError = ex.Message;
            }
        }

        // Send SMS if enabled
        if (notification.SendSMS && !string.IsNullOrEmpty(preferences.PhoneNumber))
        {
            try
            {
                await _smsService.SendSMSAsync(
                    preferences.PhoneNumber,
                    $"{title}: {message}");

                notification.SMSSent = true;
                notification.SMSSentOn = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send SMS notification to {recipientUserId}");
                notification.SMSError = ex.Message;
            }
        }

        notification.LastModifiedBy = resolvedSenderUserId;
        await UpdateAsync(notification);

        // Broadcast new notification via SignalR
        await BroadcastNewNotificationAsync(notification);

        return notification;
    }

    public async Task<Notification> NotifyAllUsersAsync(
        Guid organizationId,
        string title,
        string message,
        string type,
        string category,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        // Query users through OrganizationUsers to find all users in the organization
        var userIds = await _context.OrganizationUsers
            .Where(uo => uo.OrganizationId == organizationId && uo.IsActive && !uo.IsDeleted)
            .Select(uo => uo.UserId)
            .ToListAsync();

        Notification? lastNotification = null;

        foreach (var userId in userIds)
        {
            lastNotification = await SendNotificationAsync(
                userId,
                title,
                message,
                type,
                category,
                relatedEntityId,
                relatedEntityType,
                organizationId,
                senderUserId: ApplicationConstants.SystemUser.Id);
        }

        return lastNotification!;
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await GetByIdAsync(notificationId);
        if (notification == null) return;

        notification.IsRead = true;
        notification.ReadOn = DateTime.UtcNow;

        await UpdateAsync(notification);

        // Broadcast notification read event via SignalR
        var unreadCount = await GetUnreadCountAsync(notification.RecipientUserId);
        await BroadcastNotificationReadAsync(notificationId, notification.RecipientUserId, unreadCount);
    }

    /// <summary>
    /// Mark notification as unread
    /// </summary>
    public async Task MarkAsUnreadAsync(Guid notificationId)
    {
        var notification = await GetByIdAsync(notificationId);
        if (notification == null) return;

        notification.IsRead = false;
        notification.ReadOn = null;

        await UpdateAsync(notification);

        // Broadcast updated unread count via SignalR
        var unreadCount = await GetUnreadCountAsync(notification.RecipientUserId);
        await BroadcastUnreadCountChangedAsync(notification.RecipientUserId, unreadCount);
    }

    /// <summary>
    /// Mark all notifications as read for the current user
    /// </summary>
    public async Task MarkAllAsReadAsync(List<Notification> notifications)
    {
        var userId = notifications.FirstOrDefault()?.RecipientUserId;
        
        foreach (var notification in notifications)
        {
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadOn = DateTime.UtcNow;
                await UpdateAsync(notification);
            }
        }

        // Broadcast updated unread count via SignalR
        if (userId != null)
        {
            var unreadCount = await GetUnreadCountAsync(userId);
            await BroadcastUnreadCountChangedAsync(userId, unreadCount);
        }
    }

    /// <summary>
    /// Get unread notifications for current user
    /// Returns empty list if user is not authenticated or has no organization.
    /// </summary>
    public async Task<List<Notification>> GetUnreadNotificationsAsync()
    {
        var userId = await _userContext.GetUserIdAsync();
        var organizationId = await _userContext.GetActiveOrganizationIdAsync();

        // Return empty list if not authenticated or no organization
        if (string.IsNullOrEmpty(userId) || !organizationId.HasValue)
        {
            return new List<Notification>();
        }

        return await _context.Notifications
            .Where(n => n.OrganizationId == organizationId
                && n.RecipientUserId == userId
                && !n.IsRead
                && !n.IsDeleted)
            .OrderByDescending(n => n.SentOn)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>
    /// Get notification history for current user
    /// </summary>
    public async Task<List<Notification>> GetNotificationHistoryAsync(int count = 100)
    {
        var userId = await _userContext.GetUserIdAsync();
        var organizationId = await _userContext.GetActiveOrganizationIdAsync();

        return await _context.Notifications
            .Where(n => n.OrganizationId == organizationId
                && n.RecipientUserId == userId
                && !n.IsDeleted)
            .OrderByDescending(n => n.SentOn)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Get notification preferences for current user
    /// </summary>
    public async Task<NotificationPreferences> GetUserPreferencesAsync()
    {
        var userId = await _userContext.GetUserIdAsync();
        return await GetNotificationPreferencesAsync(userId!);
    }

    /// <summary>
    /// Update notification preferences for current user
    /// </summary>
    public async Task<NotificationPreferences> UpdateUserPreferencesAsync(NotificationPreferences preferences)
    {
        var userId = await _userContext.GetUserIdAsync();
        var organizationId = await _userContext.GetActiveOrganizationIdAsync();

        // Ensure the preferences belong to the current user and organization
        if (preferences.UserId != userId || preferences.OrganizationId != organizationId)
        {
            throw new UnauthorizedAccessException("Cannot update preferences for another user");
        }

        _context.NotificationPreferences.Update(preferences);
        await _context.SaveChangesAsync();
        return preferences;
    }

    /// <summary>
    /// Get or create notification preferences for user
    /// </summary>
    private async Task<NotificationPreferences> GetNotificationPreferencesAsync(string userId, Guid? organizationId = null)
    {
        organizationId ??= await _userContext.GetActiveOrganizationIdAsync();

        var preferences = await _context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId
                && p.UserId == userId
                && !p.IsDeleted);

        if (preferences == null)
        {
            // Create default preferences
            preferences = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId!.Value,
                UserId = userId,
                EnableInAppNotifications = true,
                EnableEmailNotifications = true,
                EnableSMSNotifications = false,
                EmailLeaseExpiring = true,
                EmailPaymentDue = true,
                EmailPaymentReceived = true,
                EmailApplicationStatusChange = true,
                EmailMaintenanceUpdate = true,
                EmailInspectionScheduled = true
            };

            _context.NotificationPreferences.Add(preferences);
            await _context.SaveChangesAsync();
        }

        return preferences;
    }

    private bool ShouldSendEmail(string category, NotificationPreferences prefs)
    {
        return category switch
        {
            NotificationConstants.Categories.Lease => prefs.EmailLeaseExpiring,
            NotificationConstants.Categories.Payment => prefs.EmailPaymentDue,
            NotificationConstants.Categories.Application => prefs.EmailApplicationStatusChange,
            NotificationConstants.Categories.Maintenance => prefs.EmailMaintenanceUpdate,
            NotificationConstants.Categories.Inspection => prefs.EmailInspectionScheduled,
            _ => true
        };
    }

    private bool ShouldSendSMS(string category, NotificationPreferences prefs)
    {
        return category switch
        {
            NotificationConstants.Categories.Payment => prefs.SMSPaymentDue,
            NotificationConstants.Categories.Maintenance => prefs.SMSMaintenanceEmergency,
            NotificationConstants.Categories.Lease => prefs.SMSLeaseExpiringUrgent,
            _ => false
        };
    }

    /// <summary>
    /// Sends an email directly without creating a notification record.
    /// Useful for digest emails and system notifications.
    /// </summary>
    public async Task SendEmailDirectAsync(string to, string subject, string body, string? fromName = null)
    {
        await _emailService.SendEmailAsync(to, subject, body, fromName);
    }

    /// <summary>
    /// Delete a notification
    /// </summary>
    public async Task DeleteNotificationAsync(Guid notificationId)
    {
        var notification = await GetByIdAsync(notificationId);
        if (notification == null) return;

        var userId = notification.RecipientUserId;
        await DeleteAsync(notificationId);

        // Broadcast notification deleted event via SignalR
        var unreadCount = await GetUnreadCountAsync(userId);
        await BroadcastNotificationDeletedAsync(notificationId, userId, unreadCount);
    }

    /// <summary>
    /// Get unread count for a specific user
    /// </summary>
    private async Task<int> GetUnreadCountAsync(string userId)
    {
        var organizationId = await _userContext.GetActiveOrganizationIdAsync();
        
        return await _context.Notifications
            .CountAsync(n => n.OrganizationId == organizationId
                && n.RecipientUserId == userId
                && !n.IsRead
                && !n.IsDeleted);
    }

    #region SignalR Broadcasting

    /// <summary>
    /// Broadcasts a new notification to the user via SignalR
    /// </summary>
    private async Task BroadcastNewNotificationAsync(Notification notification)
    {
        try
        {
            await _hubContext.Clients
                .User(notification.RecipientUserId)
                .SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.Category,
                    notification.SentOn,
                    notification.IsRead
                });

            _logger.LogInformation(
                "Broadcasted notification {NotificationId} to user {UserId} via SignalR",
                notification.Id,
                notification.RecipientUserId);
        }
        catch (Exception ex)
        {
            // Don't fail notification creation if SignalR fails
            _logger.LogWarning(ex,
                "Failed to broadcast notification {NotificationId} via SignalR",
                notification.Id);
        }
    }

    /// <summary>
    /// Broadcasts that a notification was marked as read
    /// </summary>
    private async Task BroadcastNotificationReadAsync(Guid notificationId, string userId, int newUnreadCount)
    {
        try
        {
            await _hubContext.Clients
                .User(userId)
                .SendAsync("NotificationRead", notificationId, newUnreadCount);

            _logger.LogInformation(
                "Broadcasted notification read {NotificationId} to user {UserId} via SignalR",
                notificationId,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast notification read via SignalR");
        }
    }

    /// <summary>
    /// Broadcasts that a notification was deleted
    /// </summary>
    private async Task BroadcastNotificationDeletedAsync(Guid notificationId, string userId, int newUnreadCount)
    {
        try
        {
            await _hubContext.Clients
                .User(userId)
                .SendAsync("NotificationDeleted", notificationId, newUnreadCount);

            _logger.LogInformation(
                "Broadcasted notification deleted {NotificationId} to user {UserId} via SignalR",
                notificationId,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast notification deletion via SignalR");
        }
    }

    /// <summary>
    /// Broadcasts updated unread count (e.g., mark all as read)
    /// </summary>
    private async Task BroadcastUnreadCountChangedAsync(string userId, int newUnreadCount)
    {
        try
        {
            await _hubContext.Clients
                .User(userId)
                .SendAsync("UpdateUnreadCount", newUnreadCount);

            _logger.LogInformation(
                "Broadcasted unread count {Count} to user {UserId} via SignalR",
                newUnreadCount,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast unread count change via SignalR");
        }
    }

    #endregion

    #region Sample Data Inheritance

    /// <summary>
    /// Automatically inherits IsSampleData from the related entity so that
    /// notifications created from sample data actions are themselves flagged
    /// as sample data and removed when sample data is cleared.
    /// </summary>
    protected override async Task<Notification> SetCreateDefaultsAsync(Notification entity)
    {
        entity = await base.SetCreateDefaultsAsync(entity);

        if (!entity.IsSampleData && entity.RelatedEntityId.HasValue && entity.RelatedEntityType != null)
        {
            entity.IsSampleData = await IsRelatedEntitySampleDataAsync(
                entity.RelatedEntityId.Value,
                entity.RelatedEntityType);
        }

        return entity;
    }

    private async Task<bool> IsRelatedEntitySampleDataAsync(Guid entityId, string entityType)
    {
        return entityType switch
        {
            ApplicationConstants.EntityTypes.Lease =>
                await _context.Leases.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            ApplicationConstants.EntityTypes.Application =>
                await _context.RentalApplications.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            ApplicationConstants.EntityTypes.Property =>
                await _context.Properties.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            ApplicationConstants.EntityTypes.Tenant =>
                await _context.Tenants.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            ApplicationConstants.EntityTypes.Invoice =>
                await _context.Invoices.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            ApplicationConstants.EntityTypes.MaintenanceRequest =>
                await _context.MaintenanceRequests.Where(e => e.Id == entityId).Select(e => e.IsSampleData).FirstOrDefaultAsync(),
            _ => false
        };
    }

    #endregion
}