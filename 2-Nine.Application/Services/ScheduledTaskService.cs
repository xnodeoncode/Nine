using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Nine.Application.Services.Workflows;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Nine.Application.Services
{
    public class ScheduledTaskService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private Timer? _timer;
        private Timer? _dailyTimer;
        private Timer? _hourlyTimer;
        private Timer? _weeklyTimer;

        public ScheduledTaskService(
            ILogger<ScheduledTaskService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Task Service is starting.");

            // Run immediately on startup
            await DoWork(stoppingToken);

            // Then run daily at 2 AM
            _timer = new Timer(
                async _ => await DoWork(stoppingToken),
                null,
                TimeSpan.FromMinutes(GetMinutesUntil2AM()),
                TimeSpan.FromHours(24));

            await Task.CompletedTask;

            // Calculate time until next midnight for daily tasks
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var timeUntilMidnight = nextMidnight - now;

            // Start daily timer (executes at midnight)
            _dailyTimer = new Timer(
                async _ => await ExecuteDailyTasks(),
                null,
                timeUntilMidnight,
                TimeSpan.FromDays(1));

            // Start hourly timer (executes every hour)
            _hourlyTimer = new Timer(
                async _ => await ExecuteHourlyTasks(),
                null,
                TimeSpan.Zero, // Start immediately
                TimeSpan.FromHours(1));

            // Calculate time until next Monday 6 AM for weekly tasks
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0 && now.Hour >= 6)
            {
                daysUntilMonday = 7; // If it's Monday and past 6 AM, schedule for next Monday
            }
            var nextMonday = now.Date.AddDays(daysUntilMonday).AddHours(6);
            var timeUntilMonday = nextMonday - now;

            // Start weekly timer (executes every Monday at 6 AM)
            _weeklyTimer = new Timer(
                async _ => await ExecuteWeeklyTasks(),
                null,
                timeUntilMonday,
                TimeSpan.FromDays(7));

            _logger.LogInformation("Scheduled Task Service started. Daily tasks will on each application start-up or when explicityly triggered in Application");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task DoWork(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Running scheduled tasks at {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    // Check if database is locked - skip all tasks if so
                    var unlockState = scope.ServiceProvider.GetService<DatabaseUnlockState>();
                    if (unlockState?.NeedsUnlock == true)
                    {
                        _logger.LogWarning("Database locked - skipping scheduled tasks. Tasks will resume after unlock.");
                        return;
                    }

                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
                    var leaseNotificationService = scope.ServiceProvider.GetRequiredService<LeaseNotificationService>();

                    // Get all distinct organization IDs from OrganizationSettings
                    var organizations = await dbContext.OrganizationSettings
                        .Where(s => !s.IsDeleted)
                        .Select(s => s.OrganizationId)
                        .Distinct()
                        .ToListAsync(stoppingToken);

                    foreach (var organizationId in organizations)
                    {
                        // Get settings for this organization
                        var settings = await organizationService.GetOrganizationSettingsByOrgIdAsync(organizationId);
                        
                        if (settings == null)
                        {
                            _logger.LogWarning("No settings found for organization {OrganizationId}, skipping", organizationId);
                            continue;
                        }

                        // Task 1: Process overdue invoices (status update + late fees)
                        await ProcessOverdueInvoices(dbContext, organizationId, settings, stoppingToken);

                        // Task 2: Send payment reminders (if enabled)
                        if (settings.PaymentReminderEnabled)
                        {
                            await SendPaymentReminders(dbContext, organizationId, settings, stoppingToken);
                        }

                        // Task 3: Check for expiring leases and send renewal notifications
                        await leaseNotificationService.SendLeaseRenewalRemindersAsync(organizationId, stoppingToken);

                        // Task 4: Expire overdue leases using workflow service (with audit logging)
                        var expiredLeaseCount = await ExpireOverdueLeases(scope, organizationId);
                        if (expiredLeaseCount > 0)
                        {
                            _logger.LogInformation(
                                "Expired {Count} overdue lease(s) for organization {OrganizationId}",
                                expiredLeaseCount, organizationId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing scheduled tasks.");
            }
        }

        /// <summary>
        /// Process overdue invoices: Update status to Overdue and apply late fees in one atomic operation.
        /// This prevents the race condition where status is updated but late fees are not applied.
        /// </summary>
        private async Task ProcessOverdueInvoices(
            ApplicationDbContext dbContext, 
            Guid organizationId,
            OrganizationSettings settings,
            CancellationToken stoppingToken)
        {
            try
            {
                var today = DateTime.Today;
                var gracePeriodCutoff = today.AddDays(-settings.LateFeeGracePeriodDays);

                // Find ALL pending invoices that are past due
                var overdueInvoices = await dbContext.Invoices
                    .Include(i => i.Lease)
                    .Where(i => !i.IsDeleted &&
                               i.OrganizationId == organizationId &&
                               i.Status == "Pending" &&
                               i.DueOn < today)
                    .ToListAsync(stoppingToken);

                var statusUpdatedCount = 0;
                var lateFeesAppliedCount = 0;

                foreach (var invoice in overdueInvoices)
                {
                    // Always update status to Overdue
                    invoice.Status = "Overdue";
                    invoice.LastModifiedOn = DateTime.UtcNow;
                    invoice.LastModifiedBy = ApplicationConstants.SystemUser.Id;
                    statusUpdatedCount++;

                    // Apply late fee if:
                    // 1. Late fees are enabled and auto-apply is on
                    // 2. Grace period has elapsed
                    // 3. Late fee hasn't been applied yet
                    if (settings.LateFeeEnabled && 
                        settings.LateFeeAutoApply &&
                        invoice.DueOn < gracePeriodCutoff &&
                        (invoice.LateFeeApplied == null || !invoice.LateFeeApplied.Value))
                    {
                        var lateFee = Math.Min(invoice.Amount * settings.LateFeePercentage, settings.MaxLateFeeAmount);
                        
                        invoice.LateFeeAmount = lateFee;
                        invoice.LateFeeApplied = true;
                        invoice.LateFeeAppliedOn = DateTime.UtcNow;
                        invoice.Amount += lateFee;
                        invoice.Notes = string.IsNullOrEmpty(invoice.Notes)
                            ? $"Late fee of {lateFee:C} applied on {DateTime.Now:MMM dd, yyyy}"
                            : $"{invoice.Notes}\nLate fee of {lateFee:C} applied on {DateTime.Now:MMM dd, yyyy}";

                        lateFeesAppliedCount++;

                        _logger.LogInformation(
                            "Applied late fee of {LateFee:C} to invoice {InvoiceNumber} (ID: {InvoiceId}) for organization {OrganizationId}",
                            lateFee, invoice.InvoiceNumber, invoice.Id, organizationId);
                    }
                }

                if (overdueInvoices.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation(
                        "Processed {TotalCount} overdue invoice(s) for organization {OrganizationId}: {StatusUpdated} status updated, {LateFeesApplied} late fees applied",
                        overdueInvoices.Count, organizationId, statusUpdatedCount, lateFeesAppliedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing overdue invoices for organization {OrganizationId}", organizationId);
            }
        }

        private async Task SendPaymentReminders(
            ApplicationDbContext dbContext, 
            Guid organizationId,
            OrganizationSettings settings,
            CancellationToken stoppingToken)
        {
            try
            {
                var today = DateTime.Today;

                // Find invoices due soon
                var upcomingInvoices = await dbContext.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Where(i => !i.IsDeleted &&
                               i.OrganizationId == organizationId &&
                               i.Status == "Pending" &&
                               i.DueOn >= today &&
                               i.DueOn <= today.AddDays(settings.PaymentReminderDaysBefore) &&
                               (i.ReminderSent == null || !i.ReminderSent.Value))
                    .ToListAsync(stoppingToken);

                foreach (var invoice in upcomingInvoices)
                {
                    // TODO: Integrate with email service when implemented
                    // For now, just log the reminder
                    _logger.LogInformation(
                        "Payment reminder needed for invoice {InvoiceNumber} due {DueDate} for tenant {TenantName} in organization {OrganizationId}",
                        invoice.InvoiceNumber,
                        invoice.DueOn.ToString("MMM dd, yyyy"),
                        invoice.Lease?.Tenant?.FullName ?? "Unknown",
                        organizationId);

                    invoice.ReminderSent = true;
                    invoice.ReminderSentOn = DateTime.UtcNow;
                    invoice.LastModifiedOn = DateTime.UtcNow;
                    invoice.LastModifiedBy = ApplicationConstants.SystemUser.Id; // Automated task
                }

                if (upcomingInvoices.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Marked {Count} invoices as reminder sent for organization {OrganizationId}", 
                        upcomingInvoices.Count, organizationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment reminders for organization {OrganizationId}", organizationId);
            }
        }

        // Lease renewal reminder logic moved to LeaseNotificationService

        private async Task ExecuteDailyTasks()
        {
            _logger.LogInformation("Executing daily tasks at {Time}", DateTime.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();
                var propertyService = scope.ServiceProvider.GetRequiredService<PropertyService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Calculate daily payment totals
                var today = DateTime.Today;
                var todayPayments = await paymentService.GetAllAsync();
                var dailyTotal = todayPayments
                    .Where(p => p.PaidOn.Date == today && !p.IsDeleted)
                    .Sum(p => p.Amount);

                _logger.LogInformation("Daily Payment Total for {Date}: ${Amount:N2}", 
                    today.ToString("yyyy-MM-dd"), 
                    dailyTotal);

                // Check for overdue routine inspections
                var overdueInspections = await propertyService.GetPropertiesWithOverdueInspectionsAsync();
                if (overdueInspections.Any())
                {
                    _logger.LogWarning("{Count} properties have overdue routine inspections", 
                        overdueInspections.Count);
                    
                    foreach (var property in overdueInspections.Take(5)) // Log first 5
                    {
                        var daysOverdue = (DateTime.Today - property.NextRoutineInspectionDueDate!.Value).Days;
                        _logger.LogWarning("Property {Address} - Inspection overdue by {Days} days (Due: {DueDate})",
                            property.Address,
                            daysOverdue,
                            property.NextRoutineInspectionDueDate.Value.ToString("yyyy-MM-dd"));
                    }
                }

                // Check for inspections due soon (within 30 days)
                var dueSoonInspections = await propertyService.GetPropertiesWithInspectionsDueSoonAsync(30);
                if (dueSoonInspections.Any())
                {
                    _logger.LogInformation("{Count} propert(ies) have routine inspections due within 30 days", 
                        dueSoonInspections.Count);
                }

                // Check for expired rental applications
                var expiredApplicationsCount = await ExpireOldApplications(dbContext);
                if (expiredApplicationsCount > 0)
                {
                    _logger.LogInformation("Expired {Count} rental application(s) that passed their expiration date", 
                        expiredApplicationsCount);
                }

                // Check for expired lease offers (uses workflow service for audit logging)
                var expiredLeaseOffersCount = await ExpireOldLeaseOffers(scope);
                if (expiredLeaseOffersCount > 0)
                {
                    _logger.LogInformation("Expired {Count} lease offer(s) that passed their expiration date", 
                        expiredLeaseOffersCount);
                }

                // Check for year-end dividend calculation (runs in first week of January)
                if (today.Month == 1 && today.Day <= 7)
                {
                    await ProcessYearEndDividends(scope, today.Year - 1);
                }

                // Send daily digest emails to users who have opted in
                var digestService = scope.ServiceProvider.GetRequiredService<DigestService>();
                await digestService.SendDailyDigestsAsync();

                // Additional daily tasks:
                // - Generate daily reports
                // - Send payment reminders
                // - Check for overdue invoices
                // - Archive old records
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing daily tasks");
            }
        }

        // Daily digest logic moved to DigestService

        private async Task ExecuteWeeklyTasks()
        {
            _logger.LogInformation("Executing weekly tasks at {Time}", DateTime.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var digestService = scope.ServiceProvider.GetRequiredService<DigestService>();
                var documentNotificationService = scope.ServiceProvider.GetRequiredService<DocumentNotificationService>();
                var maintenanceNotificationService = scope.ServiceProvider.GetRequiredService<MaintenanceNotificationService>();

                await digestService.SendWeeklyDigestsAsync();
                await documentNotificationService.CheckDocumentExpirationsAsync();
                await maintenanceNotificationService.SendMaintenanceStatusSummaryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing weekly tasks");
            }
        }

        // Old SendDailyDigestsAsync removed - functionality in DigestService
        private async Task ExecuteHourlyTasks()
        {
            _logger.LogInformation("Executing hourly tasks at {Time}", DateTime.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();

                // Check if database is locked - skip all tasks if so
                var unlockState = scope.ServiceProvider.GetService<DatabaseUnlockState>();
                if (unlockState?.NeedsUnlock == true)
                {
                    _logger.LogWarning("Database locked - skipping hourly tasks. Tasks will resume after unlock.");
                    return;
                }

                var tourService = scope.ServiceProvider.GetRequiredService<TourService>();
                var leaseService = scope.ServiceProvider.GetRequiredService<LeaseService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get all organizations
                var organizations = await dbContext.OrganizationSettings
                    .Where(s => !s.IsDeleted)
                    .ToListAsync();

                int totalMarkedNoShow = 0;

                foreach (var orgSettings in organizations)
                {
                    var organizationId = orgSettings.OrganizationId;
                    var gracePeriodHours = orgSettings.TourNoShowGracePeriodHours;

                    // Check for tours that should be marked as no-show
                    var cutoffTime = DateTime.Now.AddHours(-gracePeriodHours);
                    
                    // Query tours directly for this organization (bypass user context)
                    var potentialNoShowTours = await dbContext.Tours
                        .Where(t => t.OrganizationId == organizationId && !t.IsDeleted)
                        .Include(t => t.ProspectiveTenant)
                        .Include(t => t.Property)
                        .ToListAsync();
                    
                    var noShowTours = potentialNoShowTours
                        .Where(t => t.Status == ApplicationConstants.TourStatuses.Scheduled &&
                                   t.ScheduledOn < cutoffTime)
                        .ToList();

                    foreach (var tour in noShowTours)
                    {
                        await tourService.MarkTourAsNoShowAsync(tour.Id);
                        totalMarkedNoShow++;
                        
                        _logger.LogInformation(
                            "Marked tour {TourId} as No Show - Scheduled: {ScheduledTime}, Grace period: {Hours} hours",
                            tour.Id,
                            tour.ScheduledOn.ToString("yyyy-MM-dd HH:mm"),
                            gracePeriodHours);
                    }
                }

                if (totalMarkedNoShow > 0)
                {
                    _logger.LogInformation("Marked {Count} tour(s) as No Show across all organizations", totalMarkedNoShow);
                }

                // Example hourly task: Check for upcoming lease expirations
                var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                var userId = httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                if (!string.IsNullOrEmpty(userId))
                {
                    var upcomingLeases = await leaseService.GetAllAsync();
                    var expiringIn30Days = upcomingLeases
                        .Where(l => l.EndDate >= DateTime.Today && 
                                   l.EndDate <= DateTime.Today.AddDays(30) && 
                                   !l.IsDeleted)
                        .Count();

                    if (expiringIn30Days > 0)
                    {
                        _logger.LogInformation("{Count} lease(s) expiring in the next 30 days", expiringIn30Days);
                    }
                }

                // You can add more hourly tasks here:
                // - Check for maintenance requests
                // - Update lease statuses
                // - Send notifications
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing hourly tasks");
            }
        }

        private double GetMinutesUntil2AM()
        {
            var now = DateTime.Now;
            var next2AM = DateTime.Today.AddDays(1).AddHours(2);
            
            if (now.Hour < 2)
            {
                next2AM = DateTime.Today.AddHours(2);
            }

            return (next2AM - now).TotalMinutes;
        }

        private async Task<int> ExpireOldApplications(ApplicationDbContext dbContext)
        {
            try
            {
                // Find all applications that are expired but not yet marked as such
                var expiredApplications = await dbContext.RentalApplications
                    .Where(a => !a.IsDeleted &&
                               (a.Status == ApplicationConstants.ApplicationStatuses.Submitted ||
                                a.Status == ApplicationConstants.ApplicationStatuses.UnderReview ||
                                a.Status == ApplicationConstants.ApplicationStatuses.Screening) &&
                               a.ExpiresOn.HasValue &&
                               a.ExpiresOn.Value < DateTime.UtcNow)
                    .ToListAsync();

                foreach (var application in expiredApplications)
                {
                    application.Status = ApplicationConstants.ApplicationStatuses.Expired;
                    application.LastModifiedOn = DateTime.UtcNow;
                    application.LastModifiedBy = ApplicationConstants.SystemUser.Id; // Automated task
                    
                    _logger.LogInformation("Expired application {ApplicationId} for property {PropertyId} (Expired on: {ExpirationDate})",
                        application.Id,
                        application.PropertyId,
                        application.ExpiresOn!.Value.ToString("yyyy-MM-dd"));
                }

                if (expiredApplications.Any())
                {
                    await dbContext.SaveChangesAsync();
                }

                return expiredApplications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring old applications");
                return 0;
            }
        }

        /// <summary>
        /// Expires lease offers that have passed their expiration date.
        /// Uses ApplicationWorkflowService for proper audit logging.
        /// </summary>
        private async Task<int> ExpireOldLeaseOffers(IServiceScope scope)
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var workflowService = scope.ServiceProvider.GetRequiredService<ApplicationWorkflowService>();

                // Find all pending lease offers that have expired
                var expiredOffers = await dbContext.LeaseOffers
                    .Where(lo => !lo.IsDeleted &&
                                lo.Status == "Pending" &&
                                lo.ExpiresOn < DateTime.UtcNow)
                    .ToListAsync();

                var expiredCount = 0;

                foreach (var offer in expiredOffers)
                {
                    try
                    {
                        var result = await workflowService.ExpireLeaseOfferAsync(offer.Id, offer.OrganizationId);
                        
                        if (result.Success)
                        {
                            expiredCount++;
                            _logger.LogInformation(
                                "Expired lease offer {LeaseOfferId} for property {PropertyId} (Expired on: {ExpirationDate})",
                                offer.Id,
                                offer.PropertyId,
                                offer.ExpiresOn.ToString("yyyy-MM-dd"));
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Failed to expire lease offer {LeaseOfferId}: {Errors}",
                                offer.Id,
                                string.Join(", ", result.Errors));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error expiring lease offer {LeaseOfferId}", offer.Id);
                    }
                }

                return expiredCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring old lease offers");
                return 0;
            }
        }

        /// <summary>
        /// Processes year-end security deposit dividend calculations.
        /// Runs in the first week of January for the previous year.
        /// </summary>
        private async Task ProcessYearEndDividends(IServiceScope scope, int year)
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var securityDepositService = scope.ServiceProvider.GetRequiredService<SecurityDepositService>();

                // Get all organizations that have security deposit investment enabled
                var organizations = await dbContext.OrganizationSettings
                    .Where(s => !s.IsDeleted && s.SecurityDepositInvestmentEnabled)
                    .Select(s => s.OrganizationId)
                    .Distinct()
                    .ToListAsync();

                foreach (var organizationId in organizations)
                {
                    try
                    {
                        // Check if pool exists and has performance recorded
                        var pool = await dbContext.SecurityDepositInvestmentPools
                            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId &&
                                                     p.Year == year &&
                                                     !p.IsDeleted);

                        if (pool == null)
                        {
                            _logger.LogInformation(
                                "No investment pool found for organization {OrganizationId} for year {Year}",
                                organizationId, year);
                            continue;
                        }

                        if (pool.Status == "Distributed" || pool.Status == "Closed")
                        {
                            _logger.LogInformation(
                                "Dividends already processed for organization {OrganizationId} for year {Year}",
                                organizationId, year);
                            continue;
                        }

                        if (pool.TotalEarnings == 0)
                        {
                            _logger.LogInformation(
                                "No earnings recorded for organization {OrganizationId} for year {Year}. " +
                                "Please record investment performance before dividend calculation.",
                                organizationId, year);
                            continue;
                        }

                        // Calculate dividends
                        var dividends = await securityDepositService.CalculateDividendsAsync(year);

                        if (dividends.Any())
                        {
                            _logger.LogInformation(
                                "Calculated {Count} dividend(s) for organization {OrganizationId} for year {Year}. " +
                                "Total tenant share: ${TenantShare:N2}",
                                dividends.Count,
                                organizationId,
                                year,
                                dividends.Sum(d => d.DividendAmount));
                        }
                        else
                        {
                            _logger.LogInformation(
                                "No dividends to calculate for organization {OrganizationId} for year {Year}",
                                organizationId, year);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing dividends for organization {OrganizationId} for year {Year}",
                            organizationId, year);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing year-end dividends for year {Year}", year);
            }
        }

        /// <summary>
        /// Expires leases that have passed their end date using LeaseWorkflowService.
        /// This provides proper audit logging for lease expiration.
        /// </summary>
        private async Task<int> ExpireOverdueLeases(IServiceScope scope, Guid organizationId)
        {
            try
            {
                var leaseWorkflowService = scope.ServiceProvider.GetRequiredService<LeaseWorkflowService>();
                var result = await leaseWorkflowService.ExpireOverdueLeaseAsync(organizationId);

                if (result.Success)
                {
                    return result.Data;
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to expire overdue leases for organization {OrganizationId}: {Errors}",
                        organizationId,
                        string.Join(", ", result.Errors));
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring overdue leases for organization {OrganizationId}", organizationId);
                return 0;
            }
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Task Service is stopping.");
            _timer?.Dispose();
            _dailyTimer?.Change(Timeout.Infinite, 0);
            _hourlyTimer?.Change(Timeout.Infinite, 0);
            _weeklyTimer?.Change(Timeout.Infinite, 0);
            return base.StopAsync(stoppingToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            _dailyTimer?.Dispose();
            _hourlyTimer?.Dispose();
            base.Dispose();
        }
    }
}
