using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nine.Application.Services.Workflows
{
    /// <summary>
    /// Lease status enumeration for state machine validation.
    /// </summary>
    public enum LeaseStatus
    {
        Pending,
        Active,
        Renewed,
        MonthToMonth,
        NoticeGiven,
        Expired,
        Terminated
    }

    /// <summary>
    /// Workflow service for lease lifecycle management.
    /// Handles lease activation, renewals, termination notices, and move-out workflows.
    /// </summary>
    public class LeaseWorkflowService : BaseWorkflowService, IWorkflowState<LeaseStatus>
    {
        private readonly NoteService _noteService;
        private readonly NotificationService _notificationService;

        public LeaseWorkflowService(
            ApplicationDbContext context,
            IUserContextService userContext,
            NoteService noteService,
            NotificationService notificationService)
            : base(context, userContext)
        {
            _noteService = noteService;
            _notificationService = notificationService;
        }

        #region State Machine Implementation

        public bool IsValidTransition(LeaseStatus fromStatus, LeaseStatus toStatus)
        {
            var validTransitions = GetValidNextStates(fromStatus);
            return validTransitions.Contains(toStatus);
        }

        public List<LeaseStatus> GetValidNextStates(LeaseStatus currentStatus)
        {
            return currentStatus switch
            {
                LeaseStatus.Pending => new()
                {
                    LeaseStatus.Active,
                    LeaseStatus.Terminated // Can cancel before activation
                },
                LeaseStatus.Active => new()
                {
                    LeaseStatus.Renewed,
                    LeaseStatus.MonthToMonth,
                    LeaseStatus.NoticeGiven,
                    LeaseStatus.Expired,
                    LeaseStatus.Terminated
                },
                LeaseStatus.Renewed => new()
                {
                    LeaseStatus.Active, // New term starts
                    LeaseStatus.NoticeGiven,
                    LeaseStatus.Terminated
                },
                LeaseStatus.MonthToMonth => new()
                {
                    LeaseStatus.NoticeGiven,
                    LeaseStatus.Renewed, // Sign new fixed-term lease
                    LeaseStatus.Terminated
                },
                LeaseStatus.NoticeGiven => new()
                {
                    LeaseStatus.Expired, // Notice period ends naturally
                    LeaseStatus.Terminated // Early termination
                },
                _ => new List<LeaseStatus>() // Terminal states have no valid transitions
            };
        }

        public string GetInvalidTransitionReason(LeaseStatus fromStatus, LeaseStatus toStatus)
        {
            var validStates = GetValidNextStates(fromStatus);
            return $"Cannot transition from {fromStatus} to {toStatus}. Valid next states: {string.Join(", ", validStates)}";
        }

        #endregion

        #region Core Workflow Methods

        /// <summary>
        /// Activates a pending lease when all conditions are met (deposit paid, documents signed).
        /// Updates property status to Occupied.
        /// </summary>
        public async Task<WorkflowResult> ActivateLeaseAsync(Guid leaseId, DateTime? moveInDate = null)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult.Fail("Lease not found");

                if (lease.Status != ApplicationConstants.LeaseStatuses.Pending)
                    return WorkflowResult.Fail(
                        $"Lease must be in Pending status to activate. Current status: {lease.Status}");

                // Validate start date is not too far in the future
                if (lease.StartDate > DateTime.Today.AddDays(30))
                    return WorkflowResult.Fail(
                        "Cannot activate lease more than 30 days before start date");

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();
                var oldStatus = lease.Status;

                // Update lease
                lease.Status = ApplicationConstants.LeaseStatuses.Active;
                lease.SignedOn = moveInDate ?? DateTime.Today;
                lease.LastModifiedBy = userId;
                lease.LastModifiedOn = DateTime.UtcNow;

                // Update property status
                if (lease.Property != null)
                {
                    lease.Property.Status = ApplicationConstants.PropertyStatuses.Occupied;
                    lease.Property.LastModifiedBy = userId;
                    lease.Property.LastModifiedOn = DateTime.UtcNow;
                }

                // Update tenant status to active
                if (lease.Tenant != null)
                {
                    lease.Tenant.IsActive = true;
                    lease.Tenant.LastModifiedBy = userId;
                    lease.Tenant.LastModifiedOn = DateTime.UtcNow;
                }

                await LogTransitionAsync(
                    "Lease",
                    leaseId,
                    oldStatus,
                    lease.Status,
                    "ActivateLease");

                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Lease Activated for {lease.Tenant?.FullName}",
                    $"Lease for {lease.Tenant?.FullName} property '{lease.Property?.Address}' has been activated effective {lease.SignedOn:MMM dd, yyyy}.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    leaseId,
                    ApplicationConstants.EntityTypes.Lease);
                
                // TODO: Send email to tenant with welcome info
                // _notificatoinService.SendLeaseActivationEmailToTenant(lease);

                return WorkflowResult.Ok("Lease activated successfully");
            });
        }

        /// <summary>
        /// Records a termination notice from tenant or landlord.
        /// Sets expected move-out date and changes lease status.
        /// </summary>
        public async Task<WorkflowResult> RecordTerminationNoticeAsync(
            Guid leaseId,
            DateTime noticeDate,
            DateTime expectedMoveOutDate,
            string noticeType, // "Tenant", "Landlord", "Mutual"
            string reason)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult.Fail("Lease not found");

                var activeStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.Active,
                    ApplicationConstants.LeaseStatuses.MonthToMonth,
                    ApplicationConstants.LeaseStatuses.Renewed
                };

                if (!activeStatuses.Contains(lease.Status))
                    return WorkflowResult.Fail(
                        $"Can only record termination notice for active leases. Current status: {lease.Status}");

                if (expectedMoveOutDate <= DateTime.Today)
                    return WorkflowResult.Fail("Expected move-out date must be in the future");

                if (string.IsNullOrWhiteSpace(reason))
                    return WorkflowResult.Fail("Termination notice reason is required");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = lease.Status;

                // Update lease
                lease.Status = ApplicationConstants.LeaseStatuses.NoticeGiven;
                lease.TerminationNoticedOn = noticeDate;
                lease.ExpectedMoveOutDate = expectedMoveOutDate;
                lease.TerminationReason = $"[{noticeType}] {reason}";
                lease.LastModifiedBy = userId;
                lease.LastModifiedOn = DateTime.UtcNow;

                // Add note for audit trail
                var noteContent = $"Termination notice recorded. Type: {noticeType}. Expected move-out: {expectedMoveOutDate:MMM dd, yyyy}. Reason: {reason}";
                await _noteService.AddNoteAsync(ApplicationConstants.EntityTypes.Lease, leaseId, noteContent);

                await LogTransitionAsync(
                    "Lease",
                    leaseId,
                    oldStatus,
                    lease.Status,
                    "RecordTerminationNotice",
                    reason);

                // Send notification about termination notice
                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Termination Notice Recorded for {lease.Tenant?.FullName}",
                    $"A termination notice has been recorded for lease of {lease.Tenant?.FullName} at property '{lease.Property?.Address}'. Expected move-out date: {expectedMoveOutDate:MMM dd, yyyy}.",
                    NotificationConstants.Types.Warning,
                    NotificationConstants.Categories.Lease,
                    leaseId,
                    ApplicationConstants.EntityTypes.Lease);

                return WorkflowResult.Ok($"Termination notice recorded. Move-out date: {expectedMoveOutDate:MMM dd, yyyy}");
            });
        }

        /// <summary>
        /// Converts an active fixed-term lease to month-to-month when term expires
        /// without renewal.
        /// </summary>
        public async Task<WorkflowResult> ConvertToMonthToMonthAsync(Guid leaseId, decimal? newMonthlyRent = null)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult.Fail("Lease not found");

                var validStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.Active,
                    ApplicationConstants.LeaseStatuses.Expired
                };

                if (!validStatuses.Contains(lease.Status))
                    return WorkflowResult.Fail(
                        $"Can only convert to month-to-month from Active or Expired status. Current status: {lease.Status}");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = lease.Status;

                // Update lease
                lease.Status = ApplicationConstants.LeaseStatuses.MonthToMonth;
                if (newMonthlyRent.HasValue && newMonthlyRent > 0)
                {
                    lease.MonthlyRent = newMonthlyRent.Value;
                }
                lease.LastModifiedBy = userId;
                lease.LastModifiedOn = DateTime.UtcNow;

                await LogTransitionAsync(
                    "Lease",
                    leaseId,
                    oldStatus,
                    lease.Status,
                    "ConvertToMonthToMonth");

                // send notification about conversion
                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Lease Converted to Month-to-Month for {lease.Tenant?.FullName}",
                    $"Lease for {lease.Tenant?.FullName} property '{lease.Property?.Address}' has been converted to month-to-month tenancy.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    leaseId,
                    ApplicationConstants.EntityTypes.Lease);

                return WorkflowResult.Ok("Lease converted to month-to-month successfully");
            });
        }

        /// <summary>
        /// Creates a lease renewal (extends existing lease with new terms).
        /// Option to update rent, deposit, and end date.
        /// </summary>
        public async Task<WorkflowResult<Lease>> RenewLeaseAsync(
            Guid leaseId,
            LeaseRenewalModel model)
        {
            return await ExecuteWorkflowAsync<Lease>(async () =>
            {
                var existingLease = await GetLeaseAsync(leaseId);
                if (existingLease == null)
                    return WorkflowResult<Lease>.Fail("Lease not found");

                var renewableStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.Active,
                    ApplicationConstants.LeaseStatuses.MonthToMonth,
                    ApplicationConstants.LeaseStatuses.NoticeGiven // Can be cancelled with renewal
                };

                if (!renewableStatuses.Contains(existingLease.Status))
                    return WorkflowResult<Lease>.Fail(
                        $"Lease must be in an active state to renew. Current status: {existingLease.Status}");

                // Validate renewal terms
                if (model.NewEndDate <= existingLease.EndDate)
                    return WorkflowResult<Lease>.Fail("New end date must be after current end date");

                if (model.NewMonthlyRent <= 0)
                    return WorkflowResult<Lease>.Fail("Monthly rent must be greater than zero");

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();
                var oldStatus = existingLease.Status;

                // Create renewal record (new lease linked to existing)
                var renewalLease = new Lease
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    PropertyId = existingLease.PropertyId,
                    TenantId = existingLease.TenantId,
                    PreviousLeaseId = existingLease.Id, // Link to previous lease
                    StartDate = model.NewStartDate ?? existingLease.EndDate.AddDays(1),
                    EndDate = model.NewEndDate,
                    MonthlyRent = model.NewMonthlyRent,
                    SecurityDeposit = model.UpdatedSecurityDeposit ?? existingLease.SecurityDeposit,
                    Terms = model.NewTerms ?? existingLease.Terms,
                    Status = ApplicationConstants.LeaseStatuses.Active,
                    SignedOn = DateTime.Today,
                    RenewalNumber = existingLease.RenewalNumber + 1,
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.Leases.Add(renewalLease);

                // Update existing lease status
                existingLease.Status = ApplicationConstants.LeaseStatuses.Renewed;
                existingLease.LastModifiedBy = userId;
                existingLease.LastModifiedOn = DateTime.UtcNow;

                // Log transitions
                await LogTransitionAsync(
                    "Lease",
                    existingLease.Id,
                    oldStatus,
                    existingLease.Status,
                    "RenewLease");

                await LogTransitionAsync(
                    "Lease",
                    renewalLease.Id,
                    null,
                    renewalLease.Status,
                    "CreateRenewal");

                // Add note about renewal
                var noteContent = $"Lease renewed. New term: {renewalLease.StartDate:MMM dd, yyyy} - {renewalLease.EndDate:MMM dd, yyyy}. Rent: ${renewalLease.MonthlyRent:N2}/month.";
                await _noteService.AddNoteAsync(ApplicationConstants.EntityTypes.Lease, renewalLease.Id, noteContent);

                // Send notification about lease renewal
                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Lease Renewed for {renewalLease.Tenant?.FullName}",
                    $"Lease for {renewalLease.Tenant?.FullName} property '{renewalLease.Property?.Address}' has been renewed for the term {renewalLease.StartDate:MMM dd, yyyy} to {renewalLease.EndDate:MMM dd, yyyy} at ${renewalLease.MonthlyRent:N2}/month.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    renewalLease.Id,
                    ApplicationConstants.EntityTypes.Lease);

                return WorkflowResult<Lease>.Ok(
                    renewalLease,
                    "Lease renewed successfully");
            });
        }

        /// <summary>
        /// Completes the move-out process after tenant vacates.
        /// Updates property to Available status.
        /// </summary>
        public async Task<WorkflowResult> CompleteMoveOutAsync(
            Guid leaseId,
            DateTime actualMoveOutDate,
            MoveOutModel? model = null)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult.Fail("Lease not found");

                var moveOutStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.NoticeGiven,
                    ApplicationConstants.LeaseStatuses.Expired,
                    ApplicationConstants.LeaseStatuses.Active // Emergency move-out
                };

                if (!moveOutStatuses.Contains(lease.Status))
                    return WorkflowResult.Fail(
                        $"Cannot complete move-out for lease in {lease.Status} status");

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();
                var oldStatus = lease.Status;

                // Update lease
                lease.Status = ApplicationConstants.LeaseStatuses.Terminated;
                lease.ActualMoveOutDate = actualMoveOutDate;
                lease.LastModifiedBy = userId;
                lease.LastModifiedOn = DateTime.UtcNow;

                // Update property status to Available (ready for new tenant)
                if (lease.Property != null)
                {
                    lease.Property.Status = ApplicationConstants.PropertyStatuses.Available;
                    lease.Property.LastModifiedBy = userId;
                    lease.Property.LastModifiedOn = DateTime.UtcNow;
                }

                // Deactivate tenant if no other active leases
                if (lease.Tenant != null)
                {
                    var hasOtherActiveLeases = await _context.Leases
                        .AnyAsync(l => l.TenantId == lease.TenantId &&
                                      l.Id != leaseId &&
                                      l.OrganizationId == orgId &&
                                      (l.Status == ApplicationConstants.LeaseStatuses.Active ||
                                       l.Status == ApplicationConstants.LeaseStatuses.MonthToMonth) &&
                                      !l.IsDeleted);

                    if (!hasOtherActiveLeases)
                    {
                        lease.Tenant.IsActive = false;
                        lease.Tenant.LastModifiedBy = userId;
                        lease.Tenant.LastModifiedOn = DateTime.UtcNow;
                    }
                }

                await LogTransitionAsync(
                    "Lease",
                    leaseId,
                    oldStatus,
                    lease.Status,
                    "CompleteMoveOut",
                    model?.Notes);

                // Add note with move-out details
                var noteContent = $"Move-out completed on {actualMoveOutDate:MMM dd, yyyy}.";
                if (model?.FinalInspectionCompleted == true)
                    noteContent += " Final inspection completed.";
                if (model?.KeysReturned == true)
                    noteContent += " Keys returned.";
                if (!string.IsNullOrWhiteSpace(model?.Notes))
                    noteContent += $" Notes: {model.Notes}";

                await _noteService.AddNoteAsync(ApplicationConstants.EntityTypes.Lease, leaseId, noteContent);

                // Send notification about completed move-out
                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Lease Move-Out Completed for {lease.Tenant?.FullName}",
                    $"Lease for {lease.Tenant?.FullName} property '{lease.Property?.Address}' has been marked as moved out effective {actualMoveOutDate:MMM dd, yyyy}.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    leaseId,
                    ApplicationConstants.EntityTypes.Lease);

                return WorkflowResult.Ok("Move-out completed successfully");
            });
        }

        /// <summary>
        /// Early terminates a lease (eviction, breach, mutual agreement).
        /// </summary>
        public async Task<WorkflowResult> EarlyTerminateAsync(
            Guid leaseId,
            string terminationType, // "Eviction", "Breach", "Mutual", "Emergency"
            string reason,
            DateTime effectiveDate)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult.Fail("Lease not found");

                var terminableStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.Active,
                    ApplicationConstants.LeaseStatuses.MonthToMonth,
                    ApplicationConstants.LeaseStatuses.NoticeGiven,
                    ApplicationConstants.LeaseStatuses.Pending
                };

                if (!terminableStatuses.Contains(lease.Status))
                    return WorkflowResult.Fail(
                        $"Cannot terminate lease in {lease.Status} status");

                if (string.IsNullOrWhiteSpace(reason))
                    return WorkflowResult.Fail("Termination reason is required");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = lease.Status;

                // Update lease
                lease.Status = ApplicationConstants.LeaseStatuses.Terminated;
                lease.TerminationReason = $"[{terminationType}] {reason}";
                lease.ActualMoveOutDate = effectiveDate;
                lease.LastModifiedBy = userId;
                lease.LastModifiedOn = DateTime.UtcNow;

                // Update property status
                if (lease.Property != null && effectiveDate <= DateTime.Today)
                {
                    lease.Property.Status = ApplicationConstants.PropertyStatuses.Available;
                    lease.Property.LastModifiedBy = userId;
                    lease.Property.LastModifiedOn = DateTime.UtcNow;
                }

                // Deactivate tenant if no other active leases
                if (lease.Tenant != null)
                {
                    var orgId = await GetActiveOrganizationIdAsync();
                    var hasOtherActiveLeases = await _context.Leases
                        .AnyAsync(l => l.TenantId == lease.TenantId &&
                                      l.Id != leaseId &&
                                      l.OrganizationId == orgId &&
                                      (l.Status == ApplicationConstants.LeaseStatuses.Active ||
                                       l.Status == ApplicationConstants.LeaseStatuses.MonthToMonth) &&
                                      !l.IsDeleted);

                    if (!hasOtherActiveLeases)
                    {
                        lease.Tenant.IsActive = false;
                        lease.Tenant.LastModifiedBy = userId;
                        lease.Tenant.LastModifiedOn = DateTime.UtcNow;
                    }
                }

                await LogTransitionAsync(
                    "Lease",
                    leaseId,
                    oldStatus,
                    lease.Status,
                    "EarlyTerminate",
                    $"[{terminationType}] {reason}");

                // Send notification about early termination
                await _notificationService.NotifyAllUsersAsync(
                    lease.OrganizationId,
                    $"Early Lease Termination for {lease.Tenant?.FullName}",
                    $"Lease for {lease.Tenant?.FullName} property '{lease.Property?.Address}' has been early terminated effective {effectiveDate:MMM dd, yyyy}. Reason: {terminationType} - {reason}",
                    NotificationConstants.Types.Warning,
                    NotificationConstants.Categories.Lease,
                    leaseId,
                    ApplicationConstants.EntityTypes.Lease);

                return WorkflowResult.Ok($"Lease terminated ({terminationType})");
            });
        }

        /// <summary>
        /// Expires leases that have passed their end date without renewal.
        /// Called by ScheduledTaskService.
        /// </summary>
        public async Task<WorkflowResult<int>> ExpireOverdueLeaseAsync()
        {
            var orgId = await GetActiveOrganizationIdAsync();
            return await ExpireOverdueLeaseAsync(orgId);
        }

        /// <summary>
        /// Expires leases that have passed their end date without renewal.
        /// This overload accepts organizationId for background service contexts.
        /// </summary>
        public async Task<WorkflowResult<int>> ExpireOverdueLeaseAsync(Guid organizationId)
        {
            return await ExecuteWorkflowAsync<int>(async () =>
            {
                // Called from background service — no Blazor circuit, use "System" as the actor.
                var userId = await _userContext.GetUserIdAsync() ?? "System";
                
                // Find active leases past their end date
                var expiredLeases = await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => l.OrganizationId == organizationId &&
                               l.Status == ApplicationConstants.LeaseStatuses.Active &&
                               l.EndDate < DateTime.Today &&
                               !l.IsDeleted)
                    .ToListAsync();

                var count = 0;
                var addresses = string.Empty;
                foreach (var lease in expiredLeases)
                {
                    var oldStatus = lease.Status;
                    lease.Status = ApplicationConstants.LeaseStatuses.Expired;
                    lease.LastModifiedBy = userId;
                    lease.LastModifiedOn = DateTime.UtcNow;

                    await LogTransitionAsync(
                        "Lease",
                        lease.Id,
                        oldStatus,
                        lease.Status,
                        "AutoExpire",
                        "Lease end date passed without renewal",
                        organizationId: organizationId);

                    addresses += $"- {lease.Property?.Address} (Tenant: {lease.Tenant?.FullName})\n";

                    count++;
                }

                if (count > 0)
                {
                    await _notificationService.NotifyAllUsersAsync(
                        organizationId,
                        "Expired Lease Notification",
                        $"{count} lease(s) have been automatically expired as of today.\n\n{addresses}",
                        NotificationConstants.Types.Info,
                        NotificationConstants.Categories.Lease,
                        null,
                        ApplicationConstants.EntityTypes.Lease);
                }

                return WorkflowResult<int>.Ok(count, $"{count} lease(s) expired");
            });
        }

        #endregion

        #region Notification Methods

        /// <summary>
        /// Notifies all users in the organization of leases expiring in the specified number of days.
        /// </summary>
        public async Task NotifyLeasesExpiringInAsync(int daysUntilExpiration)
        {
            var targetDate = DateTime.Today.AddDays(daysUntilExpiration);
            var orgId = await GetActiveOrganizationIdAsync();
            
            var expiringLeases = await _context.Leases
                .Where(l => l.EndDate.Date == targetDate 
                        && l.Status == ApplicationConstants.LeaseStatuses.Active
                        && l.OrganizationId == orgId
                        && !l.IsDeleted)
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .ToListAsync();

            var urgency = daysUntilExpiration switch
            {
                <= 30 => NotificationConstants.Types.Warning,
                _ => NotificationConstants.Types.Info
            };

            foreach (var lease in expiringLeases)
            {
                await _notificationService.NotifyAllUsersAsync(
                    orgId,
                    $"Lease Expiring in {daysUntilExpiration} Days - {lease.Property.Address}",
                    $"Lease expires {lease.EndDate:MMM dd, yyyy}. Please review renewal options.",
                    urgency,
                    NotificationConstants.Categories.Lease,
                    lease.Id,
                    ApplicationConstants.EntityTypes.Lease
                );
            }
        }

        #endregion

        #region Security Deposit Workflow Methods

        /// <summary>
        /// Initiates security deposit settlement at end of lease.
        /// Calculates deductions and remaining refund amount.
        /// </summary>
        public async Task<WorkflowResult<SecurityDepositSettlement>> InitiateDepositSettlementAsync(
            Guid leaseId,
            List<DepositDeductionModel> deductions)
        {
            return await ExecuteWorkflowAsync<SecurityDepositSettlement>(async () =>
            {
                var lease = await GetLeaseAsync(leaseId);
                if (lease == null)
                    return WorkflowResult<SecurityDepositSettlement>.Fail("Lease not found");

                var settlementStatuses = new[] {
                    ApplicationConstants.LeaseStatuses.NoticeGiven,
                    ApplicationConstants.LeaseStatuses.Expired,
                    ApplicationConstants.LeaseStatuses.Terminated
                };

                if (!settlementStatuses.Contains(lease.Status))
                    return WorkflowResult<SecurityDepositSettlement>.Fail(
                        "Can only settle deposit for leases in termination status");

                var orgId = await GetActiveOrganizationIdAsync();

                // Get security deposit record
                var deposit = await _context.SecurityDeposits
                    .FirstOrDefaultAsync(sd => sd.LeaseId == leaseId && 
                                              sd.OrganizationId == orgId &&
                                              !sd.IsDeleted);

                if (deposit == null)
                    return WorkflowResult<SecurityDepositSettlement>.Fail("Security deposit record not found");

                if (deposit.Status == "Returned")
                    return WorkflowResult<SecurityDepositSettlement>.Fail("Security deposit has already been settled");

                // Calculate settlement
                var totalDeductions = deductions.Sum(d => d.Amount);
                var refundAmount = deposit.Amount - totalDeductions;

                var settlement = new SecurityDepositSettlement
                {
                    LeaseId = leaseId,
                    TenantId = lease.TenantId,
                    OriginalAmount = deposit.Amount,
                    TotalDeductions = totalDeductions,
                    RefundAmount = Math.Max(0, refundAmount),
                    AmountOwed = Math.Max(0, -refundAmount), // If negative, tenant owes money
                    Deductions = deductions,
                    SettlementDate = DateTime.Today
                };

                // Update deposit record status
                var userId = await GetCurrentUserIdAsync();
                deposit.Status = refundAmount > 0 ? "Pending Return" : "Forfeited";
                deposit.LastModifiedBy = userId;
                deposit.LastModifiedOn = DateTime.UtcNow;

                return WorkflowResult<SecurityDepositSettlement>.Ok(
                    settlement,
                    $"Deposit settlement calculated. Refund amount: ${refundAmount:N2}");
            });
        }

        /// <summary>
        /// Records the security deposit refund payment.
        /// </summary>
        public async Task<WorkflowResult> RecordDepositRefundAsync(
            Guid leaseId,
            decimal refundAmount,
            string paymentMethod,
            string? referenceNumber = null)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var orgId = await GetActiveOrganizationIdAsync();

                var deposit = await _context.SecurityDeposits
                    .FirstOrDefaultAsync(sd => sd.LeaseId == leaseId && 
                                              sd.OrganizationId == orgId &&
                                              !sd.IsDeleted);

                if (deposit == null)
                    return WorkflowResult.Fail("Security deposit record not found");

                if (deposit.Status == "Returned")
                    return WorkflowResult.Fail("Deposit has already been returned");

                var userId = await GetCurrentUserIdAsync();

                deposit.Status = "Refunded";
                deposit.RefundProcessedDate = DateTime.Today;
                deposit.RefundAmount = refundAmount;
                deposit.RefundMethod = paymentMethod;
                deposit.RefundReference = referenceNumber;
                deposit.Notes = $"Refund: ${refundAmount:N2} via {paymentMethod}. Ref: {referenceNumber ?? "N/A"}";
                deposit.LastModifiedBy = userId;
                deposit.LastModifiedOn = DateTime.UtcNow;

                await LogTransitionAsync(
                    "SecurityDeposit",
                    deposit.Id,
                    "Pending Return",
                    "Refunded",
                    "RecordDepositRefund",
                    $"Refunded ${refundAmount:N2}");

                return WorkflowResult.Ok("Security deposit refund recorded");
            });
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Returns a comprehensive view of the lease's workflow state,
        /// including tenant, property, security deposit, and audit history.
        /// </summary>
        public async Task<LeaseWorkflowState> GetLeaseWorkflowStateAsync(Guid leaseId)
        {
            var orgId = await GetActiveOrganizationIdAsync();

            var lease = await _context.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == leaseId && l.OrganizationId == orgId && !l.IsDeleted);

            if (lease == null)
                return new LeaseWorkflowState
                {
                    Lease = null,
                    AuditHistory = new List<WorkflowAuditLog>()
                };

            var securityDeposit = await _context.SecurityDeposits
                .FirstOrDefaultAsync(sd => sd.LeaseId == leaseId && sd.OrganizationId == orgId && !sd.IsDeleted);

            var renewals = await _context.Leases
                .Where(l => l.PreviousLeaseId == leaseId && l.OrganizationId == orgId && !l.IsDeleted)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            var auditHistory = await _context.WorkflowAuditLogs
                .Where(w => w.EntityType == "Lease" && w.EntityId == leaseId && w.OrganizationId == orgId)
                .OrderByDescending(w => w.PerformedOn)
                .ToListAsync();

            return new LeaseWorkflowState
            {
                Lease = lease,
                Tenant = lease.Tenant,
                Property = lease.Property,
                SecurityDeposit = securityDeposit,
                Renewals = renewals,
                AuditHistory = auditHistory,
                DaysUntilExpiration = (lease.EndDate - DateTime.Today).Days,
                IsExpiring = (lease.EndDate - DateTime.Today).Days <= 60,
                CanRenew = lease.Status == ApplicationConstants.LeaseStatuses.Active ||
                          lease.Status == ApplicationConstants.LeaseStatuses.MonthToMonth,
                CanTerminate = lease.Status != ApplicationConstants.LeaseStatuses.Terminated &&
                              lease.Status != ApplicationConstants.LeaseStatuses.Expired
            };
        }

        /// <summary>
        /// Gets leases that are expiring within the specified number of days.
        /// </summary>
        public async Task<List<Lease>> GetExpiringLeasesAsync(int withinDays = 60)
        {
            var orgId = await GetActiveOrganizationIdAsync();
            var cutoffDate = DateTime.Today.AddDays(withinDays);

            return await _context.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .Where(l => l.OrganizationId == orgId &&
                           l.Status == ApplicationConstants.LeaseStatuses.Active &&
                           l.EndDate <= cutoffDate &&
                           l.EndDate >= DateTime.Today &&
                           !l.IsDeleted)
                .OrderBy(l => l.EndDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all leases with termination notices.
        /// </summary>
        public async Task<List<Lease>> GetLeasesWithNoticeAsync()
        {
            var orgId = await GetActiveOrganizationIdAsync();

            return await _context.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .Where(l => l.OrganizationId == orgId &&
                           l.Status == ApplicationConstants.LeaseStatuses.NoticeGiven &&
                           !l.IsDeleted)
                .OrderBy(l => l.ExpectedMoveOutDate)
                .ToListAsync();
        }

        #endregion

        #region Helper Methods

        private async Task<Lease?> GetLeaseAsync(Guid leaseId)
        {
            var orgId = await GetActiveOrganizationIdAsync();
            return await _context.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == leaseId &&
                    l.OrganizationId == orgId &&
                    !l.IsDeleted);
        }

        #endregion
    }

    #region Models

    /// <summary>
    /// Model for lease renewal.
    /// </summary>
    public class LeaseRenewalModel
    {
        public DateTime? NewStartDate { get; set; }
        public DateTime NewEndDate { get; set; }
        public decimal NewMonthlyRent { get; set; }
        public decimal? UpdatedSecurityDeposit { get; set; }
        public string? NewTerms { get; set; }
    }

    /// <summary>
    /// Model for move-out completion.
    /// </summary>
    public class MoveOutModel
    {
        public bool FinalInspectionCompleted { get; set; }
        public bool KeysReturned { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Model for deposit deductions.
    /// </summary>
    public class DepositDeductionModel
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty; // "Cleaning", "Repair", "UnpaidRent", "Other"
    }

    /// <summary>
    /// Result of security deposit settlement calculation.
    /// </summary>
    public class SecurityDepositSettlement
    {
        public Guid LeaseId { get; set; }
        public Guid TenantId { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal AmountOwed { get; set; }
        public List<DepositDeductionModel> Deductions { get; set; } = new();
        public DateTime SettlementDate { get; set; }
    }

    /// <summary>
    /// Aggregated workflow state for a lease.
    /// </summary>
    public class LeaseWorkflowState
    {
        public Lease? Lease { get; set; }
        public Tenant? Tenant { get; set; }
        public Property? Property { get; set; }
        public SecurityDeposit? SecurityDeposit { get; set; }
        public List<Lease> Renewals { get; set; } = new();
        public List<WorkflowAuditLog> AuditHistory { get; set; } = new();
        public int DaysUntilExpiration { get; set; }
        public bool IsExpiring { get; set; }
        public bool CanRenew { get; set; }
        public bool CanTerminate { get; set; }
    }

    #endregion
}
