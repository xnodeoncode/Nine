using Nine.Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services;

/// <summary>
/// Service responsible for monitoring lease expirations and sending renewal reminders.
/// Sends notifications at 90, 60, and 30 days before lease expiration.
/// </summary>
public class LeaseNotificationService
{
    private readonly ILogger<LeaseNotificationService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly NotificationService _notificationService;

    public LeaseNotificationService(
        ILogger<LeaseNotificationService> logger,
        ApplicationDbContext dbContext,
        NotificationService notificationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Checks for leases expiring and sends renewal notifications at 90, 60, and 30 day marks.
    /// Updates lease renewal tracking fields accordingly.
    /// </summary>
    public async Task SendLeaseRenewalRemindersAsync(Guid organizationId, CancellationToken stoppingToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var addresses = string.Empty;

            // Check for leases expiring in 90 days (initial notification)
            var leasesExpiring90Days = await _dbContext.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .Where(l => !l.IsDeleted &&
                           l.OrganizationId == organizationId &&
                           l.Status == "Active" &&
                           l.EndDate >= today.AddDays(85) &&
                           l.EndDate <= today.AddDays(95) &&
                           (l.RenewalNotificationSent == null || !l.RenewalNotificationSent.Value))
                .ToListAsync(stoppingToken);

            foreach (var lease in leasesExpiring90Days)
            {
                _logger.LogInformation(
                    "Lease expiring in 90 days: Lease ID {LeaseId}, Property: {PropertyAddress}, Tenant: {TenantName}, End Date: {EndDate}",
                    lease.Id,
                    lease.Property?.Address ?? "Unknown",
                    lease.Tenant?.FullName ?? "Unknown",
                    lease.EndDate.ToString("MMM dd, yyyy"));

                lease.RenewalNotificationSent = true;
                lease.RenewalNotificationSentOn = DateTime.UtcNow;
                lease.RenewalStatus = "Pending";
                lease.LastModifiedOn = DateTime.UtcNow;
                lease.LastModifiedBy = ApplicationConstants.SystemUser.Id; // Automated task

                addresses += lease.Property?.Address + "\n";
            }

            // Send organization-wide notification if any leases are expiring
            if (!string.IsNullOrEmpty(addresses))
            {
                await _notificationService.NotifyAllUsersAsync(
                    organizationId,
                    "90-Day Lease Renewal Notification",
                    $"The following properties have leases expiring in 90 days:\n\n{addresses}",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    null,
                    ApplicationConstants.EntityTypes.Lease);
            }

            // clear addresses for next use
            addresses = string.Empty;

            // Check for leases expiring in 60 days (reminder)
            var leasesExpiring60Days = await _dbContext.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .Where(l => !l.IsDeleted &&
                           l.OrganizationId == organizationId &&
                           l.Status == "Active" &&
                           l.EndDate >= today.AddDays(55) &&
                           l.EndDate <= today.AddDays(65) &&
                           l.RenewalNotificationSent == true &&
                           l.RenewalReminderSentOn == null)
                .ToListAsync(stoppingToken);

            foreach (var lease in leasesExpiring60Days)
            {
                _logger.LogInformation(
                    "Lease expiring in 60 days (reminder): Lease ID {LeaseId}, Property: {PropertyAddress}, Tenant: {TenantName}, End Date: {EndDate}",
                    lease.Id,
                    lease.Property?.Address ?? "Unknown",
                    lease.Tenant?.FullName ?? "Unknown",
                    lease.EndDate.ToString("MMM dd, yyyy"));

                lease.RenewalReminderSentOn = DateTime.UtcNow;
                lease.LastModifiedOn = DateTime.UtcNow;
                lease.LastModifiedBy = ApplicationConstants.SystemUser.Id; // Automated task

                addresses += lease.Property?.Address + "\n";
            }

            // Send organization-wide notification if any leases are expiring
            if (!string.IsNullOrEmpty(addresses))
            {
                await _notificationService.NotifyAllUsersAsync(
                    organizationId,
                    "60-Day Lease Renewal Notification",
                    $"The following properties have leases expiring in 60 days:\n\n{addresses}",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    null,
                    ApplicationConstants.EntityTypes.Lease);
            }

            // clear addresses for next use
            addresses = string.Empty;

            // Check for leases expiring in 30 days (final reminder)
            var leasesExpiring30Days = await _dbContext.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .Where(l => !l.IsDeleted &&
                           l.OrganizationId == organizationId &&
                           l.Status == "Active" &&
                           l.EndDate >= today.AddDays(25) &&
                           l.EndDate <= today.AddDays(35) &&
                           l.RenewalStatus == "Pending")
                .ToListAsync(stoppingToken);

            foreach (var lease in leasesExpiring30Days)
            {
                _logger.LogInformation(
                    "Lease expiring in 30 days (final reminder): Lease ID {LeaseId}, Property: {PropertyAddress}, Tenant: {TenantName}, End Date: {EndDate}",
                    lease.Id,
                    lease.Property?.Address ?? "Unknown",
                    lease.Tenant?.FullName ?? "Unknown",
                    lease.EndDate.ToString("MMM dd, yyyy"));

                lease.LastModifiedOn = DateTime.UtcNow;
                lease.LastModifiedBy = ApplicationConstants.SystemUser.Id; // Automated task

                addresses += lease.Property?.Address + "\n";
            }

            // Send organization-wide notification if any leases are expiring
            if (!string.IsNullOrEmpty(addresses))
            {
                await _notificationService.NotifyAllUsersAsync(
                    organizationId,
                    "30-Day Lease Renewal Notification",
                    $"The following properties have leases expiring in 30 days:\n\n{addresses}",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    null,
                    ApplicationConstants.EntityTypes.Lease);
            }

            // Save all updates
            var totalUpdated = leasesExpiring90Days.Count + leasesExpiring60Days.Count + 
                              leasesExpiring30Days.Count;

            if (totalUpdated > 0)
            {
                await _dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation(
                    "Processed {Count} lease renewal notifications for organization {OrganizationId}: {Initial} initial, {Reminder60} 60-day, {Reminder30} 30-day reminders",
                    totalUpdated,
                    organizationId,
                    leasesExpiring90Days.Count,
                    leasesExpiring60Days.Count,
                    leasesExpiring30Days.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking lease renewals for organization {OrganizationId}",
                organizationId);
        }
    }
}
